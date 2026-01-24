#!/usr/bin/env bash
# Environment secret setup script for TruLoad Backend (.NET)
# Retrieves DB credentials from existing Helm releases and creates app env secret
#
# PATTERN: Based on auth-api and erpi-api best practices:
#   1. Retrieve credentials from K8s secrets (source of truth)
#   2. Create service database/user idempotently (via kubectl exec, not kubectl run)
#   3. Create app secret with all credentials
#   4. NO unreliable kubectl run verification (causes false failures)
#
# IMPORTANT: This script retrieves credentials from:
#   - PostgreSQL: 'infra' namespace (shared across all services)
#   - Redis: 'infra' namespace (shared across all services)
#   - RabbitMQ: 'infra' namespace (shared across all services)

set -euo pipefail
set +H

# Inherit logging functions from parent script or define minimal ones
log_info() { echo -e "\033[0;34m[INFO]\033[0m $1"; }
log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m $1"; }
log_warning() { echo -e "\033[1;33m[WARNING]\033[0m $1"; }
log_error() { echo -e "\033[0;31m[ERROR]\033[0m $1"; }
log_step() { echo -e "\033[0;35m[STEP]\033[0m $1"; }

# Required environment variables
NAMESPACE=${NAMESPACE:-truload}
ENV_SECRET_NAME=${ENV_SECRET_NAME:-truload-backend-env}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD:-}
REDIS_PASSWORD=${REDIS_PASSWORD:-}
RABBITMQ_PASSWORD=${RABBITMQ_PASSWORD:-}
RABBITMQ_NAMESPACE=${RABBITMQ_NAMESPACE:-infra}

# Service-specific database configuration (like auth-api pattern)
SERVICE_DB_NAME=${SERVICE_DB_NAME:-truload}
SERVICE_DB_USER=${SERVICE_DB_USER:-truload_user}
PG_NAMESPACE=${PG_NAMESPACE:-infra}

log_step "Setting up environment secrets for TruLoad Backend..."
log_info "Namespaces -> app: ${NAMESPACE}, infra(shared): infra, rabbitmq: ${RABBITMQ_NAMESPACE}"

# Ensure kubectl is available
if ! command -v kubectl &> /dev/null; then
    log_error "kubectl is required"
    exit 1
fi

# =============================================================================
# STEP 1: Retrieve PostgreSQL password from K8s secret (source of truth)
# =============================================================================
log_step "Retrieving PostgreSQL password from shared 'infra' namespace..."

APP_DB_PASS=""
if kubectl -n "$PG_NAMESPACE" get secret postgresql >/dev/null 2>&1; then
    # Try admin-user-password first (used by custom PostgreSQL setup)
    APP_DB_PASS=$(kubectl -n "$PG_NAMESPACE" get secret postgresql -o jsonpath='{.data.admin-user-password}' 2>/dev/null | base64 -d 2>/dev/null || true)

    # Fallback to postgres-password (used by Bitnami Helm chart)
    if [[ -z "$APP_DB_PASS" ]]; then
        APP_DB_PASS=$(kubectl -n "$PG_NAMESPACE" get secret postgresql -o jsonpath='{.data.postgres-password}' 2>/dev/null | base64 -d 2>/dev/null || true)
    fi

    if [[ -n "$APP_DB_PASS" ]]; then
        log_success "Retrieved PostgreSQL password from K8s secret (length: ${#APP_DB_PASS} chars)"
    fi
fi

# Fallback to environment variable
if [[ -z "$APP_DB_PASS" && -n "$POSTGRES_PASSWORD" ]]; then
    log_warning "Using POSTGRES_PASSWORD from environment variable"
    APP_DB_PASS="$POSTGRES_PASSWORD"
fi

if [[ -z "$APP_DB_PASS" ]]; then
    log_error "Could not retrieve PostgreSQL password"
    log_error "Ensure PostgreSQL is deployed: kubectl get secret postgresql -n infra"
    exit 1
fi

# =============================================================================
# STEP 2: Retrieve Redis password from K8s secret
# =============================================================================
log_step "Retrieving Redis password from shared 'infra' namespace..."

REDIS_PASS=""
if kubectl -n infra get secret redis >/dev/null 2>&1; then
    REDIS_PASS=$(kubectl -n infra get secret redis -o jsonpath='{.data.redis-password}' 2>/dev/null | base64 -d 2>/dev/null || true)
    if [[ -n "$REDIS_PASS" ]]; then
        log_success "Retrieved Redis password from K8s secret (length: ${#REDIS_PASS} chars)"
    fi
fi

# Fallback to environment variable
if [[ -z "$REDIS_PASS" && -n "$REDIS_PASSWORD" ]]; then
    log_warning "Using REDIS_PASSWORD from environment variable"
    REDIS_PASS="$REDIS_PASSWORD"
fi

if [[ -z "$REDIS_PASS" ]]; then
    log_error "Could not retrieve Redis password"
    log_error "Ensure Redis is deployed: kubectl get secret redis -n infra"
    exit 1
fi

# =============================================================================
# STEP 3: Retrieve RabbitMQ password
# =============================================================================
log_step "Retrieving RabbitMQ password from '${RABBITMQ_NAMESPACE}' namespace..."

RABBITMQ_PASS=""
if kubectl -n "$RABBITMQ_NAMESPACE" get secret rabbitmq >/dev/null 2>&1; then
    # Try different key names used by various RabbitMQ Helm charts
    for key in "rabbitmq-password" "password" "RABBITMQ_PASSWORD"; do
        RABBITMQ_PASS=$(kubectl -n "$RABBITMQ_NAMESPACE" get secret rabbitmq -o jsonpath="{.data.${key}}" 2>/dev/null | base64 -d 2>/dev/null || true)
        if [[ -n "$RABBITMQ_PASS" ]]; then
            log_info "Retrieved RabbitMQ password from secret key: ${key}"
            break
        fi
    done
fi

# Fallback: Try rabbitmq-default-user secret (RabbitMQ Cluster Operator)
if [[ -z "$RABBITMQ_PASS" ]] && kubectl -n "$RABBITMQ_NAMESPACE" get secret rabbitmq-default-user >/dev/null 2>&1; then
    RABBITMQ_PASS=$(kubectl -n "$RABBITMQ_NAMESPACE" get secret rabbitmq-default-user -o jsonpath='{.data.password}' 2>/dev/null | base64 -d 2>/dev/null || true)
    if [[ -n "$RABBITMQ_PASS" ]]; then
        log_info "Retrieved RabbitMQ password from rabbitmq-default-user secret"
    fi
fi

# Final fallback: use env var or default
if [[ -z "$RABBITMQ_PASS" ]]; then
    if [[ -n "$RABBITMQ_PASSWORD" ]]; then
        log_warning "Using RABBITMQ_PASSWORD from environment variable"
        RABBITMQ_PASS="$RABBITMQ_PASSWORD"
    else
        log_warning "RabbitMQ password not found - using default 'guest'"
        RABBITMQ_PASS="guest"
    fi
else
    log_success "RabbitMQ password retrieved (length: ${#RABBITMQ_PASS} chars)"
fi

# =============================================================================
# STEP 4: Create service database and user (IDEMPOTENT - like auth-api)
# =============================================================================
log_step "Creating service database '${SERVICE_DB_NAME}' and user '${SERVICE_DB_USER}'..."

# Wait for PostgreSQL to be ready
log_info "Waiting for PostgreSQL to be ready..."
MAX_RETRIES=30
RETRY_COUNT=0
while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if kubectl -n "$PG_NAMESPACE" get statefulset postgresql >/dev/null 2>&1; then
        READY_REPLICAS=$(kubectl -n "$PG_NAMESPACE" get statefulset postgresql -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo "0")
        READY_REPLICAS=${READY_REPLICAS:-0}
        if [[ "$READY_REPLICAS" -ge 1 ]]; then
            log_success "PostgreSQL is ready"
            break
        fi
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
        sleep 2
    fi
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    log_warning "PostgreSQL readiness check timed out - proceeding anyway"
fi

# Find PostgreSQL pod
PG_POD=$(kubectl -n "$PG_NAMESPACE" get pod -l app=postgresql -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || echo "")
if [[ -z "$PG_POD" ]]; then
    PG_POD=$(kubectl -n "$PG_NAMESPACE" get pod -l app.kubernetes.io/name=postgresql -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || echo "")
fi

if [[ -n "$PG_POD" ]]; then
    log_info "Found PostgreSQL pod: ${PG_POD}"

    # Create database if not exists (idempotent)
    log_info "Creating database '${SERVICE_DB_NAME}' (if not exists)..."
    kubectl -n "$PG_NAMESPACE" exec "$PG_POD" -c postgresql -- \
        env PGPASSWORD="$APP_DB_PASS" \
        psql -h localhost -U postgres -d postgres -tc \
        "SELECT 1 FROM pg_database WHERE datname = '${SERVICE_DB_NAME}'" 2>/dev/null | grep -q 1 || \
    kubectl -n "$PG_NAMESPACE" exec "$PG_POD" -c postgresql -- \
        env PGPASSWORD="$APP_DB_PASS" \
        psql -h localhost -U postgres -d postgres -c "CREATE DATABASE ${SERVICE_DB_NAME};" 2>/dev/null || {
        log_info "Database '${SERVICE_DB_NAME}' may already exist"
    }

    # Create user with master password (idempotent - updates password if user exists)
    log_info "Creating/updating user '${SERVICE_DB_USER}'..."
    kubectl -n "$PG_NAMESPACE" exec "$PG_POD" -c postgresql -- \
        env PGPASSWORD="$APP_DB_PASS" \
        psql -h localhost -U postgres -d postgres -c "
        DO \$\$
        BEGIN
            IF NOT EXISTS (SELECT FROM pg_user WHERE usename = '${SERVICE_DB_USER}') THEN
                CREATE USER ${SERVICE_DB_USER} WITH PASSWORD '${APP_DB_PASS}';
            ELSE
                ALTER USER ${SERVICE_DB_USER} WITH PASSWORD '${APP_DB_PASS}';
            END IF;
        END
        \$\$;" 2>/dev/null || {
        log_info "User '${SERVICE_DB_USER}' setup completed"
    }

    # Grant privileges
    log_info "Granting privileges..."
    kubectl -n "$PG_NAMESPACE" exec "$PG_POD" -c postgresql -- \
        env PGPASSWORD="$APP_DB_PASS" \
        psql -h localhost -U postgres -d postgres -c "
        GRANT ALL PRIVILEGES ON DATABASE ${SERVICE_DB_NAME} TO ${SERVICE_DB_USER};
        ALTER DATABASE ${SERVICE_DB_NAME} OWNER TO ${SERVICE_DB_USER};" 2>/dev/null || true

    kubectl -n "$PG_NAMESPACE" exec "$PG_POD" -c postgresql -- \
        env PGPASSWORD="$APP_DB_PASS" \
        psql -h localhost -U postgres -d "${SERVICE_DB_NAME}" -c "
        GRANT ALL ON SCHEMA public TO ${SERVICE_DB_USER};
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO ${SERVICE_DB_USER};
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO ${SERVICE_DB_USER};" 2>/dev/null || true

    log_success "Database and user setup completed"
else
    log_warning "PostgreSQL pod not found - database creation skipped"
    log_warning "Database should be created via devops-k8s infrastructure provisioning"
fi

# =============================================================================
# STEP 5: Build network configuration for .NET CORS
# =============================================================================
log_step "Building network configuration..."

POD_IPS=$(kubectl get pods -n "$NAMESPACE" -l app=truload-backend-app -o jsonpath='{.items[*].status.podIP}' 2>/dev/null | tr ' ' ',' || true)
SVC_IP=$(kubectl get svc truload-backend -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}' 2>/dev/null || true)
NODE_IPS=$(kubectl get nodes -o jsonpath='{.items[*].status.addresses[?(@.type=="InternalIP")].address}' 2>/dev/null | tr ' ' ',' || true)

ALLOWED_ORIGINS="https://truloadtest.masterspace.co.ke,http://localhost:3000,https://localhost:5001"
[[ -n "$SVC_IP" ]] && ALLOWED_ORIGINS="${ALLOWED_ORIGINS},http://${SVC_IP}:4000"
[[ -n "$NODE_IPS" ]] && ALLOWED_ORIGINS="${ALLOWED_ORIGINS},http://${NODE_IPS}:4000"

log_info "Allowed Origins: ${ALLOWED_ORIGINS}"

# =============================================================================
# STEP 6: Generate secrets if not provided
# =============================================================================
JWT_SECRET=${JWT_SECRET:-$(openssl rand -hex 32)}
ASPNET_SECRET=${ASPNET_SECRET:-$(openssl rand -hex 32)}

# =============================================================================
# STEP 7: Create Kubernetes secret
# =============================================================================
log_step "Creating Kubernetes secret: ${ENV_SECRET_NAME}"

# Delete and recreate to ensure clean state
kubectl -n "$NAMESPACE" delete secret "$ENV_SECRET_NAME" --ignore-not-found >/dev/null 2>&1

# Build connection string - use service user for the app (not postgres superuser)
PG_CONNECTION="Host=postgresql.infra.svc.cluster.local;Port=5432;Database=${SERVICE_DB_NAME};Username=${SERVICE_DB_USER};Password=${APP_DB_PASS}"

kubectl -n "$NAMESPACE" create secret generic "$ENV_SECRET_NAME" \
  --from-literal=ConnectionStrings__DefaultConnection="${PG_CONNECTION}" \
  --from-literal=Redis__ConnectionString="redis-master.infra.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False" \
  --from-literal=RabbitMQ__Host="rabbitmq.${RABBITMQ_NAMESPACE}.svc.cluster.local" \
  --from-literal=RabbitMQ__Port="5672" \
  --from-literal=RabbitMQ__Username="user" \
  --from-literal=RabbitMQ__Password="${RABBITMQ_PASS}" \
  --from-literal=Jwt__Secret="${JWT_SECRET}" \
  --from-literal=Jwt__Issuer="truload-api" \
  --from-literal=Jwt__Audience="truload-frontend" \
  --from-literal=AspNetCore__Secret="${ASPNET_SECRET}" \
  --from-literal=ASPNETCORE_ENVIRONMENT="Production" \
  --from-literal=ASPNETCORE_URLS="http://+:4000" \
  --from-literal=Cors__AllowedOrigins="${ALLOWED_ORIGINS}" \
  --from-literal=Cors__AllowCredentials="true" \
  --from-literal=Logging__LogLevel__Default="Information" \
  --from-literal=Logging__LogLevel__Microsoft.AspNetCore="Warning"

log_success "Environment secret created: ${ENV_SECRET_NAME}"

# =============================================================================
# STEP 8: Update KubeSecrets/devENV.yml for consistency
# =============================================================================
if [[ -d "KubeSecrets" ]] || [[ -f "KubeSecrets/devENV.yml" ]]; then
    log_step "Updating KubeSecrets/devENV.yml..."
    mkdir -p KubeSecrets

    cat > KubeSecrets/devENV.yml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: ${ENV_SECRET_NAME}
  namespace: ${NAMESPACE}
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "${PG_CONNECTION}"
  Redis__ConnectionString: "redis-master.infra.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False"
  RabbitMQ__Host: "rabbitmq.${RABBITMQ_NAMESPACE}.svc.cluster.local"
  RabbitMQ__Port: "5672"
  RabbitMQ__Username: "user"
  RabbitMQ__Password: "${RABBITMQ_PASS}"
  Jwt__Secret: "${JWT_SECRET}"
  Jwt__Issuer: "truload-api"
  Jwt__Audience: "truload-frontend"
  AspNetCore__Secret: "${ASPNET_SECRET}"
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_URLS: "http://+:4000"
  Cors__AllowedOrigins: "${ALLOWED_ORIGINS}"
  Cors__AllowCredentials: "true"
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft.AspNetCore: "Warning"
EOF
    log_success "KubeSecrets/devENV.yml updated"
fi

# =============================================================================
# STEP 9: Export validated credentials for parent script
# =============================================================================
echo "EFFECTIVE_PG_PASS=${APP_DB_PASS}"
echo "EFFECTIVE_REDIS_PASS=${REDIS_PASS}"
echo "EFFECTIVE_RABBITMQ_PASS=${RABBITMQ_PASS}"
echo "VALIDATED_DB_USER=${SERVICE_DB_USER}"
echo "VALIDATED_DB_NAME=${SERVICE_DB_NAME}"

log_success "TruLoad Backend environment secrets configured successfully"
