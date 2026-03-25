#!/usr/bin/env bash
# =============================================================================
# TruLoad Backend - Build & Deploy Script
# =============================================================================
# Pattern: Matches auth-api deployment approach
# - Uses devops-k8s scripts for database and secret creation
# - Migrations handled separately (EF Core)
# =============================================================================

set -euo pipefail
set +H

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

info() { echo -e "${BLUE}[INFO]${NC} $1"; }
success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; }

APP_NAME=${APP_NAME:-"truload-backend"}
NAMESPACE=${NAMESPACE:-"truload"}
ENV_SECRET_NAME=${ENV_SECRET_NAME:-"truload-backend-env"}
DEPLOY=${DEPLOY:-true}
SETUP_DATABASES=${SETUP_DATABASES:-true}
DB_TYPES=${DB_TYPES:-postgres,redis}
# Per-service database configuration (matches auth-api pattern)
SERVICE_DB_NAME=${SERVICE_DB_NAME:-truload}
SERVICE_DB_USER=${SERVICE_DB_USER:-truload_user}

REGISTRY_SERVER=${REGISTRY_SERVER:-docker.io}
REGISTRY_NAMESPACE=${REGISTRY_NAMESPACE:-codevertex}
IMAGE_REPO="${REGISTRY_SERVER}/${REGISTRY_NAMESPACE}/${APP_NAME}"

DEVOPS_REPO=${DEVOPS_REPO:-"Bengo-Hub/devops-k8s"}
DEVOPS_DIR=${DEVOPS_DIR:-"$HOME/devops-k8s"}
VALUES_FILE_PATH=${VALUES_FILE_PATH:-"apps/${APP_NAME}/values.yaml"}

GIT_EMAIL=${GIT_EMAIL:-"dev@truload.io"}
GIT_USER=${GIT_USER:-"TruLoad Bot"}
TRIVY_ECODE=${TRIVY_ECODE:-0}

if [[ -z ${GITHUB_SHA:-} ]]; then
  GIT_COMMIT_ID=$(git rev-parse --short=8 HEAD || echo "localbuild")
else
  GIT_COMMIT_ID=${GITHUB_SHA::8}
fi

info "Service : ${APP_NAME}"
info "Namespace: ${NAMESPACE}"
info "Image   : ${IMAGE_REPO}:${GIT_COMMIT_ID}"

for tool in git docker trivy; do
  command -v "$tool" >/dev/null || { error "$tool is required"; exit 1; }
done
if [[ ${DEPLOY} == "true" ]]; then
  for tool in kubectl helm yq jq; do
    command -v "$tool" >/dev/null || { error "$tool is required"; exit 1; }
  done
fi
success "Prerequisite checks passed"

# =============================================================================
# Auto-sync secrets from devops-k8s
# =============================================================================
if [[ ${DEPLOY} == "true" ]]; then
  info "Checking and syncing required secrets from devops-k8s..."
  SYNC_SCRIPT=$(mktemp)
  if curl -fsSL https://raw.githubusercontent.com/Bengo-Hub/devops-k8s/main/scripts/tools/check-and-sync-secrets.sh -o "$SYNC_SCRIPT" 2>/dev/null; then
    source "$SYNC_SCRIPT"
    # Note: GH_PAT is passed directly from workflow, not synced from devops-k8s
    check_and_sync_secrets "REGISTRY_USERNAME" "REGISTRY_PASSWORD" "POSTGRES_PASSWORD" "REDIS_PASSWORD" "RABBITMQ_PASSWORD" "KUBE_CONFIG" || warn "Secret sync failed - continuing with existing secrets"
    rm -f "$SYNC_SCRIPT"
  else
    warn "Unable to download secret sync script - continuing with existing secrets"
  fi
fi

info "Running Trivy filesystem scan"
trivy fs . --exit-code "$TRIVY_ECODE" --format table \
  --skip-files "*.pem" --skip-files "*.key" --skip-files "*.crt" || true

# Compute the next release version from git tags so it can be baked into the image.
# The release job will create the same tag after successful deployment.
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v1.0.0")
TAG_VERSION=${LATEST_TAG#v}
TAG_MAJOR=$(echo "$TAG_VERSION" | cut -d. -f1)
TAG_MINOR=$(echo "$TAG_VERSION" | cut -d. -f2)
TAG_PATCH=$(echo "$TAG_VERSION" | cut -d. -f3)
APP_VERSION="${TAG_MAJOR}.${TAG_MINOR}.$((TAG_PATCH + 1))"
info "Version : ${APP_VERSION} (from tag ${LATEST_TAG})"

# Export so deploy.yml release job can reuse the same version for the git tag
echo "APP_VERSION=${APP_VERSION}" >> "${GITHUB_ENV:-/dev/null}" 2>/dev/null || true

info "Building Docker image"
DOCKER_BUILDKIT=1 docker build . -t "${IMAGE_REPO}:${GIT_COMMIT_ID}" \
  --build-arg APP_VERSION="${APP_VERSION}"
success "Docker build complete"

if [[ ${DEPLOY} != "true" ]]; then
  warn "DEPLOY=false -> skipping push/deploy"
  exit 0
fi

if [[ -n ${REGISTRY_USERNAME:-} && -n ${REGISTRY_PASSWORD:-} ]]; then
  echo "$REGISTRY_PASSWORD" | docker login "$REGISTRY_SERVER" -u "$REGISTRY_USERNAME" --password-stdin
fi

docker push "${IMAGE_REPO}:${GIT_COMMIT_ID}"
success "Image pushed"

if [[ -n ${KUBE_CONFIG:-} ]]; then
  mkdir -p ~/.kube
  echo "$KUBE_CONFIG" | base64 -d > ~/.kube/config
  chmod 600 ~/.kube/config
  export KUBECONFIG=~/.kube/config
fi

kubectl get ns "$NAMESPACE" >/dev/null 2>&1 || kubectl create ns "$NAMESPACE"

if [[ -z ${CI:-}${GITHUB_ACTIONS:-} && -f KubeSecrets/devENV.yml ]]; then
  info "Applying local dev secrets"
  kubectl apply -n "$NAMESPACE" -f KubeSecrets/devENV.yml || warn "Failed to apply devENV.yml"
fi

if [[ -n ${REGISTRY_USERNAME:-} && -n ${REGISTRY_PASSWORD:-} ]]; then
  kubectl -n "$NAMESPACE" create secret docker-registry registry-credentials \
    --docker-server="$REGISTRY_SERVER" \
    --docker-username="$REGISTRY_USERNAME" \
    --docker-password="$REGISTRY_PASSWORD" \
    --dry-run=client -o yaml | kubectl apply -f - || warn "registry secret creation failed"
fi

# Create per-service database if SETUP_DATABASES is enabled (matches auth-api pattern)
if [[ "$SETUP_DATABASES" == "true" && -n "${KUBE_CONFIG:-}" ]]; then
  # Wait for PostgreSQL to be ready in infra namespace
  if kubectl -n infra get statefulset postgresql >/dev/null 2>&1; then
    info "Waiting for PostgreSQL to be ready..."
    kubectl -n infra rollout status statefulset/postgresql --timeout=180s || warn "PostgreSQL not fully ready"

    # Create service database using devops-k8s script
    if [[ -d "$DEVOPS_DIR" ]] || [[ -n "${DEVOPS_REPO:-}" ]]; then
      # Ensure devops repo is cloned
      if [[ ! -d "$DEVOPS_DIR" ]]; then
        TOKEN="${GH_PAT:-${GIT_SECRET:-${GIT_TOKEN:-}}}"
        CLONE_URL="https://github.com/${DEVOPS_REPO}.git"
        [[ -n $TOKEN ]] && CLONE_URL="https://x-access-token:${TOKEN}@github.com/${DEVOPS_REPO}.git"
        git clone "$CLONE_URL" "$DEVOPS_DIR" || { warn "Unable to clone devops repo for database setup"; }
      fi

      if [[ -d "$DEVOPS_DIR" && -f "$DEVOPS_DIR/scripts/infrastructure/create-service-database.sh" ]]; then
        info "Creating database '${SERVICE_DB_NAME}' for service ${APP_NAME}..."
        SERVICE_DB_NAME="$SERVICE_DB_NAME" \
        SERVICE_DB_USER="$SERVICE_DB_USER" \
        APP_NAME="$APP_NAME" \
        NAMESPACE="$NAMESPACE" \
        ENABLE_PGVECTOR="true" \
        bash "$DEVOPS_DIR/scripts/infrastructure/create-service-database.sh" || warn "Database creation failed or already exists"
      else
        warn "create-service-database.sh not found - database should be created via devops-k8s infrastructure"
      fi
    fi
  else
    warn "PostgreSQL not found in infra namespace - skipping database creation"
  fi
fi

# Create service secrets using devops-k8s script if not exists
# This matches auth-api pattern - uses standard keys (postgresUrl, REDIS_PASSWORD)
if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" >/dev/null 2>&1; then
  if [[ -d "$DEVOPS_DIR" && -f "$DEVOPS_DIR/scripts/infrastructure/create-service-secrets.sh" ]]; then
    info "Creating secrets for ${APP_NAME} using devops-k8s script..."
    SERVICE_NAME="$APP_NAME" \
    NAMESPACE="$NAMESPACE" \
    DB_NAME="$SERVICE_DB_NAME" \
    DB_USER="$SERVICE_DB_USER" \
    SECRET_NAME="$ENV_SECRET_NAME" \
    bash "$DEVOPS_DIR/scripts/infrastructure/create-service-secrets.sh" || warn "Secret creation failed or already exists"
  else
    warn "Secret $ENV_SECRET_NAME not found and create-service-secrets.sh not available"
    warn "Please create the secret manually or ensure devops-k8s repo is cloned"
  fi
fi

# TruLoad-specific: Ensure .NET connection string format in secret
# The devops-k8s script creates postgresUrl, but .NET needs ConnectionStrings__DefaultConnection
if kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" >/dev/null 2>&1; then
  # Check if ConnectionStrings__DefaultConnection exists
  if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" -o jsonpath='{.data.ConnectionStrings__DefaultConnection}' 2>/dev/null | grep -q .; then
    info "Adding .NET connection string format to secret..."
    # Get the postgresUrl and convert to .NET format
    PG_URL=$(kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" -o jsonpath='{.data.postgresUrl}' 2>/dev/null | base64 -d || echo "")
    if [[ -n "$PG_URL" ]]; then
      # Convert postgresql://user:pass@host:port/db to .NET format
      # Extract components from URL
      DB_USER_FROM_URL=$(echo "$PG_URL" | sed -n 's|postgresql://\([^:]*\):.*|\1|p')
      DB_PASS_FROM_URL=$(echo "$PG_URL" | sed -n 's|postgresql://[^:]*:\([^@]*\)@.*|\1|p')
      DB_HOST_FROM_URL=$(echo "$PG_URL" | sed -n 's|postgresql://[^@]*@\([^:]*\):.*|\1|p')
      DB_PORT_FROM_URL=$(echo "$PG_URL" | sed -n 's|postgresql://[^@]*@[^:]*:\([0-9]*\)/.*|\1|p')
      DB_NAME_FROM_URL=$(echo "$PG_URL" | sed -n 's|postgresql://[^/]*/\([^?]*\).*|\1|p')

      DOTNET_CONN="Host=${DB_HOST_FROM_URL};Port=${DB_PORT_FROM_URL};Database=${DB_NAME_FROM_URL};Username=${DB_USER_FROM_URL};Password=${DB_PASS_FROM_URL}"

      kubectl -n "$NAMESPACE" patch secret "$ENV_SECRET_NAME" -p "{\"stringData\":{\"ConnectionStrings__DefaultConnection\":\"$DOTNET_CONN\"}}" || warn "Failed to add .NET connection string"
      success "Added ConnectionStrings__DefaultConnection to secret"
    fi
  fi

  # Add JWT_SECRET if provided and not already in secret
  if [[ -n "${JWT_SECRET:-}" ]]; then
    if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" -o jsonpath='{.data.JWT__Secret}' 2>/dev/null | grep -q .; then
      info "Adding JWT_SECRET to secret..."
      kubectl -n "$NAMESPACE" patch secret "$ENV_SECRET_NAME" -p "{\"stringData\":{\"JWT__Secret\":\"$JWT_SECRET\"}}" || warn "Failed to add JWT_SECRET"
      success "Added JWT__Secret to secret"
    fi
  fi
fi

# Export variables for migration/seeding scripts
export APP_NAME IMAGE_REPO GIT_COMMIT_ID NAMESPACE ENV_SECRET_NAME

# Run database migrations if enabled and scripts exist
if [[ "${SETUP_DATABASES}" == "true" && -f "scripts/run_migrations.sh" ]]; then
  info "Running database migrations..."
  chmod +x scripts/run_migrations.sh
  ./scripts/run_migrations.sh || { error "Migration failed"; exit 1; }
fi

# Update Helm values using centralized script
# Update Helm values using centralized script. Try local copy first,
# otherwise fetch from devops-k8s main branch so builds outside CI still work.
UPDATE_SCRIPT_PATH="${DEVOPS_DIR}/scripts/helm/update-values.sh"
TEMP_UPDATE_SCRIPT="$(mktemp)"
if [[ -f "$UPDATE_SCRIPT_PATH" ]]; then
  source "$UPDATE_SCRIPT_PATH"
else
  if curl -fsSL "https://raw.githubusercontent.com/${DEVOPS_REPO}/main/scripts/helm/update-values.sh" -o "$TEMP_UPDATE_SCRIPT"; then
    source "$TEMP_UPDATE_SCRIPT"
    rm -f "$TEMP_UPDATE_SCRIPT"
  else
    warn "Centralized helm update script not available"
  fi
fi

if declare -f update_helm_values >/dev/null 2>&1; then
  update_helm_values "$APP_NAME" "$GIT_COMMIT_ID" "$IMAGE_REPO"
else
  warn "update_helm_values function not available - helm values not updated"
fi

info "Deployment summary"
echo "  Image      : ${IMAGE_REPO}:${GIT_COMMIT_ID}"
echo "  Namespace  : ${NAMESPACE}"
echo "  Databases  : ${SETUP_DATABASES} (${DB_TYPES})"
