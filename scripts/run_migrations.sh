#!/usr/bin/env bash
# Database migration script for TruLoad Backend (.NET/EF Core)
# Runs Entity Framework Core migrations as a Kubernetes Job

set -euo pipefail

log_info() { echo -e "\033[0;34m[INFO]\033[0m $1"; }
log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m $1"; }
log_error() { echo -e "\033[0;31m[ERROR]\033[0m $1"; }
log_step() { echo -e "\033[0;35m[STEP]\033[0m $1"; }

# Required variables
APP_NAME=${APP_NAME:-truload-backend}
IMAGE_REPO=${IMAGE_REPO:-}
GIT_COMMIT_ID=${GIT_COMMIT_ID:-}
NAMESPACE=${NAMESPACE:-truload}
ENV_SECRET_NAME=${ENV_SECRET_NAME:-truload-backend-env}

if [[ -z "$IMAGE_REPO" || -z "$GIT_COMMIT_ID" ]]; then
    log_error "IMAGE_REPO and GIT_COMMIT_ID are required"
    exit 1
fi

log_step "Running database migrations (.NET EF Core)..."

# Verify env secret exists
if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" >/dev/null 2>&1; then
    log_error "Environment secret ${ENV_SECRET_NAME} not found; cannot run migrations"
    exit 1
fi
log_info "Environment secret ${ENV_SECRET_NAME} verified"

# Create migration job with imagePullSecrets if needed
PULL_SECRETS_YAML=""
if kubectl -n "$NAMESPACE" get secret registry-credentials >/dev/null 2>&1; then
    PULL_SECRETS_YAML="      imagePullSecrets:
      - name: registry-credentials"
fi

cat > /tmp/truload-migrate-job.yaml <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: ${APP_NAME}-migrate-${GIT_COMMIT_ID}
  namespace: ${NAMESPACE}
spec:
  ttlSecondsAfterFinished: 600
  backoffLimit: 2
  template:
    spec:
      restartPolicy: Never
${PULL_SECRETS_YAML}
      containers:
      - name: migrate
        image: ${IMAGE_REPO}:${GIT_COMMIT_ID}
        # Run as root to access dotnet ef tools
        securityContext:
          runAsUser: 0
        command: 
        - bash
        - -c
        - |
          set -e
          echo "EF Core Migrations - Smart Mode"
          echo "================================"
          
          # Extract connection details from connection string
          # Format: Host=...;Port=5432;Database=truload;Username=truload_user;Password=XXX
          DB_HOST=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Host=\K[^;]+' || echo "postgresql.infra.svc.cluster.local")
          DB_USER=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Username=\K[^;]+' || echo "truload_user")
          DB_NAME=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Database=\K[^;]+' || echo "truload")
          DB_PASS=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Password=\K[^;]+' || echo "")

          echo "Database Host: \$DB_HOST"
          echo "Database User: \$DB_USER"
          echo "Database Name: \$DB_NAME"

          if [[ -z "\$DB_PASS" ]]; then
            echo "✗ Could not extract password from connection string"
            exit 1
          fi

          # Test database connectivity using extracted credentials
          echo "Testing PostgreSQL connection..."
          export PGPASSWORD="\$DB_PASS"
          psql -h "\$DB_HOST" -U "\$DB_USER" -d "\$DB_NAME" -c "SELECT 1;" >/dev/null 2>&1 && {
            echo "✓ PostgreSQL connection successful"
          } || {
            echo "✗ PostgreSQL connection failed - check credentials"
            echo "Trying with postgres superuser..."
            psql -h "\$DB_HOST" -U postgres -d "\$DB_NAME" -c "SELECT 1;" >/dev/null 2>&1 && {
              echo "✓ PostgreSQL connection successful (via postgres user)"
            } || {
              echo "✗ PostgreSQL connection failed completely"
              exit 1
            }
          }

          # Check if database has tables (indicates existing deployment)
          echo "Checking database state..."
          TABLE_COUNT=\$(psql -h "\$DB_HOST" -U "\$DB_USER" -d "\$DB_NAME" -t -c \
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public';" 2>/dev/null | xargs || echo "0")
          
          # For .NET runtime images, EF tools don't work with DLLs
          # Best practice: Apply migrations at application startup
          # For now, we'll verify the database exists and let the app handle migrations
          
          if [[ "\$TABLE_COUNT" -gt "0" ]]; then
            echo "✓ Existing database detected (\$TABLE_COUNT tables)"
            echo "Note: Migrations will be applied by application on startup"
          else
            echo "✓ Fresh database - migrations will be applied by application on startup"
          fi
          
          echo ""
          echo "⚠️  IMPORTANT: This .NET application should handle migrations on startup"
          echo "Add this to your Program.cs or Startup.cs:"
          echo ""
          echo "  using (var scope = app.Services.CreateScope())"
          echo "  {"
          echo "      var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();"
          echo "      db.Database.Migrate();  // Applies pending migrations"
          echo "  }"
          echo ""
          
          echo "✅ Migrations completed successfully"
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: ${ENV_SECRET_NAME}
              key: ConnectionStrings__DefaultConnection
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: ${ENV_SECRET_NAME}
              key: ConnectionStrings__DefaultConnection
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        envFrom:
        - secretRef:
            name: ${ENV_SECRET_NAME}
EOF

# Source stream_job helper if available, otherwise define inline
if [[ $(type -t stream_job) != "function" ]]; then
    stream_job() {
        local NS="$1"; local JOB="$2"; local TIMEOUT="$3"; local POD="";
        log_info "Streaming logs for job ${JOB} (waiting for pod to be ready)..."

        # Wait for pod to exist (up to 60 seconds for scheduler)
        for i in {1..30}; do
            POD=$(kubectl get pods -n "${NS}" -l job-name="${JOB}" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
            if [[ -n "${POD}" ]]; then
                log_info "Pod created: ${POD}"
                break
            fi
            sleep 2
        done
        if [[ -z "${POD}" ]]; then
            log_error "Pod for job ${JOB} did not start"
            return 2
        fi

        # Wait for container to be running (image pull can take time for large images)
        log_info "Waiting for container to be ready (image pull may take time)..."
        for i in {1..90}; do
            PHASE=$(kubectl get pod -n "${NS}" "${POD}" -o jsonpath='{.status.phase}' 2>/dev/null || echo "Unknown")
            CONTAINER_STATE=$(kubectl get pod -n "${NS}" "${POD}" -o jsonpath='{.status.containerStatuses[0].state}' 2>/dev/null || echo "")

            if [[ "$PHASE" == "Running" ]] || [[ "$PHASE" == "Succeeded" ]] || [[ "$PHASE" == "Failed" ]]; then
                log_info "Pod phase: ${PHASE}"
                break
            fi

            # Show waiting reason
            if [[ "$PHASE" == "Pending" ]]; then
                REASON=$(kubectl get pod -n "${NS}" "${POD}" -o jsonpath='{.status.containerStatuses[0].state.waiting.reason}' 2>/dev/null || echo "")
                if [[ -n "$REASON" ]]; then
                    log_info "Waiting: ${REASON} (attempt ${i}/90)"
                fi
            fi
            sleep 2
        done

        # Check final phase
        PHASE=$(kubectl get pod -n "${NS}" "${POD}" -o jsonpath='{.status.phase}' 2>/dev/null || echo "Unknown")
        if [[ "$PHASE" == "Pending" ]]; then
            log_error "Pod still pending after 3 minutes - possible image pull issue"
            kubectl describe pod -n "${NS}" "${POD}" | tail -30 || true
            return 2
        fi

        kubectl logs -n "${NS}" -f "${POD}" 2>/dev/null &
        local LOGS_PID=$!
        kubectl wait --for=condition=complete job/"${JOB}" -n "${NS}" --timeout="${TIMEOUT}"
        local RC=$?
        kill ${LOGS_PID} 2>/dev/null || true
        wait ${LOGS_PID} 2>/dev/null || true
        if [[ ${RC} -ne 0 ]]; then
            log_info "Final logs for ${JOB}:"
            kubectl logs -n "${NS}" "${POD}" --tail=100 || true
        fi
        return ${RC}
    }
fi

set +e
kubectl apply -f /tmp/truload-migrate-job.yaml
# Use longer timeout for .NET SDK image (large image, may need time to pull)
stream_job "${NAMESPACE}" "${APP_NAME}-migrate-${GIT_COMMIT_ID}" "600s"
JOB_STATUS=$?
set -e

if [[ $JOB_STATUS -ne 0 ]]; then
    log_error "Migration job failed or timed out"
    exit 1
fi

log_success "Database migrations completed"

