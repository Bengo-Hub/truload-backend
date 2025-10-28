#!/usr/bin/env bash

# =============================================================================
# TruLoad Backend - Production Build & Deploy Script
# =============================================================================
# - Security scan (Trivy)
# - Docker build & push
# - Optional DB setup (PostgreSQL, Redis, RabbitMQ)
# - K8s secrets apply and JWT bootstrap
# - Update centralized devops-k8s Helm values (if app path exists)
# - Works locally or in CI (uses KUBE_CONFIG when provided)
# =============================================================================

set -euo pipefail
set +H

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error()   { echo -e "${RED}[ERROR]${NC} $1"; }
log_step()    { echo -e "${PURPLE}[STEP]${NC} $1"; }

# =============================================================================
# Configuration
# =============================================================================

APP_NAME=${APP_NAME:-"truload-backend"}
NAMESPACE=${NAMESPACE:-"truload"}
ENV_SECRET_NAME=${ENV_SECRET_NAME:-"truload-backend-env"}
DEPLOY=${DEPLOY:-true}
SETUP_DATABASES=${SETUP_DATABASES:-true}
DB_TYPES=${DB_TYPES:-postgres,redis,rabbitmq}

# Registry
REGISTRY_SERVER=${REGISTRY_SERVER:-docker.io}
REGISTRY_NAMESPACE=${REGISTRY_NAMESPACE:-codevertex}
IMAGE_REPO="${REGISTRY_SERVER}/${REGISTRY_NAMESPACE}/${APP_NAME}"

# Central devops repo
DEVOPS_REPO=${DEVOPS_REPO:-"Bengo-Hub/devops-k8s"}
DEVOPS_DIR=${DEVOPS_DIR:-"$HOME/devops-k8s"}
VALUES_FILE_PATH=${VALUES_FILE_PATH:-"apps/${APP_NAME}/values.yaml"}

# Git identity for cross-repo values.yaml bump
GIT_EMAIL=${GIT_EMAIL:-"dev@truload.io"}
GIT_USER=${GIT_USER:-"TruLoad Bot"}

# Trivy exit code relaxed by default
TRIVY_ECODE=${TRIVY_ECODE:-0}

# Resolve commit id
if [[ -z ${GITHUB_SHA:-} ]]; then
  GIT_COMMIT_ID=$(git rev-parse --short=8 HEAD || echo "localbuild")
else
  GIT_COMMIT_ID=${GITHUB_SHA::8}
fi

log_info "Service: ${APP_NAME}"
log_info "Namespace: ${NAMESPACE}"
log_info "Image: ${IMAGE_REPO}:${GIT_COMMIT_ID}"

# =============================================================================
# Prerequisites
# =============================================================================

check_command() { command -v "$1" &>/dev/null || { log_error "$1 is required"; exit 1; }; }

for cmd in git docker trivy; do
  check_command "$cmd"
done
if [[ "${DEPLOY}" == "true" ]]; then
  for cmd in kubectl helm yq jq; do check_command "$cmd"; done
fi

log_success "Prerequisite checks passed"

# =============================================================================
# Security scan (filesystem)
# =============================================================================
log_step "Security scan (filesystem)"
trivy fs . --exit-code "$TRIVY_ECODE" --format table \
  --skip-files "*.pem" --skip-files "*.key" --skip-files "*.crt" || true

# =============================================================================
# Docker build
# =============================================================================
log_step "Building Docker image"
DOCKER_BUILDKIT=1 docker build . -t "${IMAGE_REPO}:${GIT_COMMIT_ID}"
log_success "Docker build complete"

# =============================================================================
# Push & Deploy
# =============================================================================
if [[ "${DEPLOY}" != "true" ]]; then
  log_info "DEPLOY=false; skipping push and deploy"
else
  # Registry login (optional)
  if [[ -n "${REGISTRY_USERNAME:-}" && -n "${REGISTRY_PASSWORD:-}" ]]; then
    echo "$REGISTRY_PASSWORD" | docker login "$REGISTRY_SERVER" -u "$REGISTRY_USERNAME" --password-stdin
  fi

  log_step "Pushing image"
  docker push "${IMAGE_REPO}:${GIT_COMMIT_ID}"
  log_success "Image pushed"

  # Kubeconfig setup
  if [[ -n "${KUBE_CONFIG:-}" ]]; then
    mkdir -p ~/.kube
    echo "$KUBE_CONFIG" | base64 -d > ~/.kube/config
    chmod 600 ~/.kube/config
    export KUBECONFIG=~/.kube/config
  fi

  # Namespace
  kubectl get ns "$NAMESPACE" >/dev/null 2>&1 || kubectl create ns "$NAMESPACE"

  # Apply environment secrets manifest if provided
  if [[ -f "KubeSecrets/devENV.yml" ]]; then
    log_step "Applying KubeSecrets/devENV.yml"
    kubectl apply -n "$NAMESPACE" -f KubeSecrets/devENV.yml || log_warning "Failed to apply devENV.yml"
  fi

  # Ensure JWT secret present
  if ! kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" -o jsonpath='{.data.JWT_SECRET}' >/dev/null 2>&1; then
    JWT_SECRET=$(openssl rand -hex 32)
    if kubectl -n "$NAMESPACE" get secret "$ENV_SECRET_NAME" >/dev/null 2>&1; then
      kubectl -n "$NAMESPACE" patch secret "$ENV_SECRET_NAME" -p "{\"stringData\":{\"JWT_SECRET\":\"$JWT_SECRET\"}}"
    else
      kubectl -n "$NAMESPACE" create secret generic "$ENV_SECRET_NAME" --from-literal=JWT_SECRET="$JWT_SECRET"
    fi
    log_success "JWT secret ensured"
  fi

  # Optional: image pull secret
  if [[ -n "${REGISTRY_USERNAME:-}" && -n "${REGISTRY_PASSWORD:-}" ]]; then
    kubectl -n "$NAMESPACE" create secret docker-registry registry-credentials \
      --docker-server="$REGISTRY_SERVER" \
      --docker-username="$REGISTRY_USERNAME" \
      --docker-password="$REGISTRY_PASSWORD" \
      --dry-run=client -o yaml | kubectl apply -f - || log_warning "Pull secret creation failed"
  fi

  # Optional database setup
  if [[ "${SETUP_DATABASES}" == "true" ]]; then
    log_step "Setting up databases: ${DB_TYPES}"
    helm repo add bitnami https://charts.bitnami.com/bitnami >/dev/null 2>&1 || true
    helm repo update >/dev/null 2>&1 || true

    IFS=',' read -r -a types <<<"$DB_TYPES"
    for raw in "${types[@]}"; do
      db=$(echo "$raw" | xargs)
      case "$db" in
        postgres)
          log_info "Installing/Upgrading PostgreSQL..."
          helm upgrade --install postgresql bitnami/postgresql -n "$NAMESPACE" \
            --set global.postgresql.auth.postgresPassword="${POSTGRES_PASSWORD:-postgres}" \
            --set global.postgresql.auth.database="truload" \
            --wait --timeout=300s || log_warning "PostgreSQL install warning"
          ;;
        redis)
          log_info "Installing/Upgrading Redis..."
          helm upgrade --install redis bitnami/redis -n "$NAMESPACE" \
            --set global.redis.password="${REDIS_PASSWORD:-redis}" \
            --wait --timeout=300s || log_warning "Redis install warning"
          ;;
        rabbitmq)
          log_info "Installing/Upgrading RabbitMQ..."
          helm upgrade --install rabbitmq bitnami/rabbitmq -n "$NAMESPACE" \
            --set auth.username="${RABBITMQ_USERNAME:-user}" \
            --set auth.password="${RABBITMQ_PASSWORD:-rabbitmq}" \
            --wait --timeout=300s || log_warning "RabbitMQ install warning"
          ;;
        *) log_warning "Unknown DB type: $db";;
      esac
    done

    # Seed a minimal env secret for app connection strings
    PG_PASS="${POSTGRES_PASSWORD:-postgres}"
    RD_PASS="${REDIS_PASSWORD:-redis}"
    RMQ_USER="${RABBITMQ_USERNAME:-user}"
    RMQ_PASS="${RABBITMQ_PASSWORD:-rabbitmq}"
    kubectl -n "$NAMESPACE" create secret generic "$ENV_SECRET_NAME" \
      --from-literal=ConnectionStrings__DefaultConnection="Host=postgresql.${NAMESPACE}.svc.cluster.local;Port=5432;Database=truload;Username=postgres;Password=${PG_PASS}" \
      --from-literal=Redis__ConnectionString="redis-master.${NAMESPACE}.svc.cluster.local:6379,password=${RD_PASS},ssl=False,abortConnect=False" \
      --from-literal=RabbitMQ__Host="rabbitmq.${NAMESPACE}.svc.cluster.local" \
      --from-literal=RabbitMQ__Username="${RMQ_USER}" \
      --from-literal=RabbitMQ__Password="${RMQ_PASS}" \
      --dry-run=client -o yaml | kubectl apply -f - || log_warning "ENV secret seed failed"
  fi

  # Update Helm values in centralized devops repo (if path exists)
  if [[ -n "${KUBE_CONFIG:-}" ]]; then
    log_step "Updating devops-k8s values (if app manifest exists)"
    TOKEN="${GH_PAT:-${GITHUB_SECRET:-${GITHUB_TOKEN:-}}}"
    CLONE_URL="https://github.com/${DEVOPS_REPO}.git"
    [[ -n "$TOKEN" ]] && CLONE_URL="https://x-access-token:${TOKEN}@github.com/${DEVOPS_REPO}.git"

    if [[ ! -d "$DEVOPS_DIR" ]]; then
      git clone "$CLONE_URL" "$DEVOPS_DIR" || { log_warning "Cannot clone devops repo"; DEVOPS_DIR=""; }
    fi
    if [[ -n "$DEVOPS_DIR" && -d "$DEVOPS_DIR" ]]; then
      pushd "$DEVOPS_DIR" >/dev/null || true
      git config user.name "$GIT_USER"; git config user.email "$GIT_EMAIL" || true
      git fetch origin main || true
      git checkout main || git checkout -b main || true
      if [[ -f "$VALUES_FILE_PATH" ]]; then
        IMAGE_REPO_ENV="$IMAGE_REPO" IMAGE_TAG_ENV="$GIT_COMMIT_ID" \
          yq e -i '.image.repository = env(IMAGE_REPO_ENV) | .image.tag = env(IMAGE_TAG_ENV)' "$VALUES_FILE_PATH"
        git add "$VALUES_FILE_PATH" && git commit -m "${APP_NAME}:${GIT_COMMIT_ID} released" || true
        if [[ -n "$TOKEN" ]]; then
          git push origin HEAD:main || log_warning "devops-k8s push failed"
        else
          log_warning "No GH token; skipped pushing to devops-k8s"
        fi
        log_success "Updated ${VALUES_FILE_PATH}"
      else
        log_warning "${VALUES_FILE_PATH} not found in devops-k8s; create app manifests to enable ArgoCD auto-sync"
      fi
      popd >/dev/null || true
    fi
  fi
fi

# =============================================================================
# Summary
# =============================================================================
log_step "Deployment Summary"
echo "Service: ${APP_NAME}"
echo "Image  : ${IMAGE_REPO}:${GIT_COMMIT_ID}"
echo "Deploy : ${DEPLOY}"
echo "DB     : ${SETUP_DATABASES} (${DB_TYPES})"

exit 0


