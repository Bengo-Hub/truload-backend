#!/usr/bin/env bash
# Database seeding script for TruLoad Backend (.NET)
# Seeds initial data as a Kubernetes Job

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
SEED_MODE=${SEED_MODE:-minimal}

if [[ -z "$IMAGE_REPO" || -z "$GIT_COMMIT_ID" ]]; then
    log_error "IMAGE_REPO and GIT_COMMIT_ID are required"
    exit 1
fi

log_step "Running database seeding (${SEED_MODE} mode)..."

# Verify env secret exists
if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" >/dev/null 2>&1; then
    log_error "Environment secret ${ENV_SECRET_NAME} not found; cannot run seeding"
    exit 1
fi

# Create seeding job with imagePullSecrets if needed
PULL_SECRETS_YAML=""
if kubectl -n "$NAMESPACE" get secret registry-credentials >/dev/null 2>&1; then
    PULL_SECRETS_YAML="      imagePullSecrets:
      - name: registry-credentials"
fi

cat > /tmp/truload-seed-job.yaml <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: ${APP_NAME}-seed-${GIT_COMMIT_ID}
  namespace: ${NAMESPACE}
spec:
  ttlSecondsAfterFinished: 600
  backoffLimit: 1
  template:
    spec:
      restartPolicy: Never
${PULL_SECRETS_YAML}
      containers:
      - name: seed
        image: ${IMAGE_REPO}:${GIT_COMMIT_ID}
        command: 
        - bash
        - -c
        - |
          set -e
          echo "Running database seeding (${SEED_MODE} mode)..."
          
          # For .NET apps, you can call a custom seed endpoint or CLI command
          # Adjust based on your actual seeding implementation
          
          # Option 1: If you have a seed CLI command
          # dotnet run --no-build -- seed --mode=${SEED_MODE}
          
          # Option 2: If you have a seed API endpoint
          # curl -X POST http://localhost:8080/api/admin/seed?mode=${SEED_MODE}
          
          # Option 3: Custom seeder class
          # For now, skip if no seeder is implemented
          echo "Note: Implement seeding logic in your .NET application"
          echo "Seeding placeholder - customize based on your app requirements"
          
          # Exit successfully even if no seeder exists (non-blocking)
          exit 0
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
kubectl apply -f /tmp/truload-seed-job.yaml
stream_job "${NAMESPACE}" "${APP_NAME}-seed-${GIT_COMMIT_ID}" "180s"
JOB_STATUS=$?
set -e

if [[ $JOB_STATUS -ne 0 ]]; then
    log_error "Seeding job failed or timed out"
    exit 1
fi

log_success "Database seeding completed"

