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

  # CRITICAL: Do NOT apply KubeSecrets/devENV.yml in CI/CD
  # It may contain outdated credentials. Skip in CI/CD, only apply locally.
  if [[ -z "${CI:-}${GITHUB_ACTIONS:-}" && -f "KubeSecrets/devENV.yml" ]]; then
    log_info "Local deployment detected - applying KubeSecrets/devENV.yml"
    kubectl apply -n "$NAMESPACE" -f KubeSecrets/devENV.yml || log_warning "Failed to apply devENV.yml"
  elif [[ -f "KubeSecrets/devENV.yml" ]]; then
    log_info "CI/CD deployment - skipping KubeSecrets/devENV.yml (managed by deployment workflow)"
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

  # Databases are managed by devops-k8s infrastructure
  # Shared PostgreSQL and Redis in 'erp' namespace, RabbitMQ in 'truload' namespace
  log_info "Databases are managed centrally by devops-k8s infrastructure"
  log_info "PostgreSQL and Redis: Shared with ERP apps in 'erp' namespace"
  log_info "RabbitMQ: Dedicated instance in 'truload' namespace"
  
  # Retrieve credentials from K8s secrets and create app environment secret
  if [[ "${SETUP_DATABASES}" == "true" ]]; then
    log_step "Retrieving database credentials from K8s secrets..."
    
    # PostgreSQL (shared with ERP in 'erp' namespace)
    PG_PASS=$(kubectl -n erp get secret postgresql -o jsonpath='{.data.postgres-password}' 2>/dev/null | base64 -d || echo "")
    if [[ -z "$PG_PASS" ]]; then
      log_error "PostgreSQL secret not found in 'erp' namespace"
      log_error "Run devops-k8s provision workflow first to install databases"
      exit 1
    fi
    
    # Redis (shared with ERP in 'erp' namespace)
    REDIS_PASS=$(kubectl -n erp get secret redis -o jsonpath='{.data.redis-password}' 2>/dev/null | base64 -d || echo "")
    if [[ -z "$REDIS_PASS" ]]; then
      log_error "Redis secret not found in 'erp' namespace"
      log_error "Run devops-k8s provision workflow first to install databases"
      exit 1
    fi
    
    # RabbitMQ (dedicated to truload namespace)
    RMQ_PASS=$(kubectl -n "$NAMESPACE" get secret rabbitmq -o jsonpath='{.data.rabbitmq-password}' 2>/dev/null | base64 -d || echo "")
    if [[ -z "$RMQ_PASS" ]]; then
      log_warning "RabbitMQ secret not found in '$NAMESPACE' namespace"
      log_warning "Will be installed by devops-k8s provision workflow"
      RMQ_PASS="${RABBITMQ_PASSWORD:-rabbitmq}"
    fi
    
    log_success "Database credentials retrieved successfully"
    
    # Create app environment secret with verified credentials
    kubectl -n "$NAMESPACE" create secret generic "$ENV_SECRET_NAME" \
      --from-literal=ConnectionStrings__DefaultConnection="Host=postgresql.erp.svc.cluster.local;Port=5432;Database=truload;Username=postgres;Password=${PG_PASS}" \
      --from-literal=Redis__ConnectionString="redis-master.erp.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False" \
      --from-literal=RabbitMQ__Host="rabbitmq.${NAMESPACE}.svc.cluster.local" \
      --from-literal=RabbitMQ__Username="user" \
      --from-literal=RabbitMQ__Password="${RMQ_PASS}" \
      --dry-run=client -o yaml | kubectl apply -f - || log_warning "ENV secret creation failed"
    
    log_success "Environment secret created with verified database credentials"
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


