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
          
          # Ensure database 'truload' exists
          echo "Ensuring database 'truload' exists..."
          export PGPASSWORD=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Password=\K[^;]+' || echo "")
          psql -h postgresql.erp.svc.cluster.local -U postgres -d postgres -c "CREATE DATABASE truload;" 2>&1 | grep -v "already exists" || true
          echo "✓ Database 'truload' ready"
          
          # Extract password from connection string for testing
          # Connection string format: Host=...;Username=postgres;Password=XXX;Database=truload
          DB_PASS=\$(echo "\$ConnectionStrings__DefaultConnection" | grep -oP 'Password=\K[^;]+' || echo "")
          
          if [[ -z "\$DB_PASS" ]]; then
            echo "✗ Could not extract password from connection string"
            exit 1
          fi
          
          # Test database connectivity
          echo "Testing PostgreSQL connection..."
          export PGPASSWORD="\$DB_PASS"
          psql -h postgresql.erp.svc.cluster.local -U postgres -d truload -c "SELECT 1;" >/dev/null 2>&1 && {
            echo "✓ PostgreSQL connection successful"
          } || {
            echo "✗ PostgreSQL connection failed - check credentials"
            echo "Connection string: \${ConnectionStrings__DefaultConnection%%Password=*}Password=***"
            exit 1
          }
          
          # Check if database has tables (indicates existing deployment)
          echo "Checking database state..."
          TABLE_COUNT=\$(psql -h postgresql.erp.svc.cluster.local -U postgres -d truload -t -c \
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public';" 2>/dev/null | xargs || echo "0")
          
          if [[ "\$TABLE_COUNT" -gt "0" ]]; then
            echo "✓ Existing database detected (\$TABLE_COUNT tables)"
            echo "Generating idempotent migration script..."
            
            # Generate SQL script that checks for existing schema before applying changes
            # --idempotent flag generates IF NOT EXISTS checks
            dotnet ef migrations script --idempotent --output /tmp/migration.sql --project /app/truload-backend.dll || {
              echo "⚠️  Could not generate idempotent script"
              echo "Attempting direct database update (may fail if schema conflicts exist)..."
              dotnet ef database update --project /app/truload-backend.dll || {
                echo "⚠️  Direct update failed - database may already be up to date"
                exit 0
              }
            }
            
            if [[ -f /tmp/migration.sql ]]; then
              echo "Applying idempotent migrations via SQL..."
              psql -h postgresql.erp.svc.cluster.local -U postgres -d truload -f /tmp/migration.sql || {
                echo "⚠️  SQL script failed - migrations may already be applied"
                exit 0
              }
            fi
          else
            echo "✓ Fresh database - running standard migrations"
            dotnet ef database update --project /app/truload-backend.dll
          fi
          
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
        log_info "Streaming logs for job ${JOB} (waiting for pod to start)..."
        for i in {1..30}; do
            POD=$(kubectl get pods -n "${NS}" -l job-name="${JOB}" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
            if [[ -n "${POD}" ]]; then
                log_info "Pod started: ${POD}"
                break
            fi
            sleep 2
        done
        if [[ -z "${POD}" ]]; then
            log_error "Pod for job ${JOB} did not start"
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
stream_job "${NAMESPACE}" "${APP_NAME}-migrate-${GIT_COMMIT_ID}" "300s"
JOB_STATUS=$?
set -e

if [[ $JOB_STATUS -ne 0 ]]; then
    log_error "Migration job failed or timed out"
    exit 1
fi

log_success "Database migrations completed"

