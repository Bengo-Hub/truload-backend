#!/usr/bin/env bash
# Environment secret setup script for TruLoad Backend (.NET)
# Retrieves DB credentials from existing Helm releases and creates app env secret
#
# IMPORTANT: This script retrieves credentials from:
#   - PostgreSQL: 'infra' namespace (shared across all services)
#   - Redis: 'infra' namespace (shared across all services)
#   - RabbitMQ: 'truload' namespace (dedicated to TruLoad)
#
# DO NOT look in 'erp' namespace - databases are in 'infra'

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

log_step "Setting up environment secrets for TruLoad Backend..."

# Ensure kubectl is available
if ! command -v kubectl &> /dev/null; then
    log_error "kubectl is required"
    exit 1
fi

# Get PostgreSQL password from shared 'infra' namespace - ALWAYS use the password from the live database
log_info "Retrieving PostgreSQL password from shared 'infra' namespace..."
if kubectl -n infra get secret postgresql >/dev/null 2>&1; then
    EXISTING_PG_PASS=$(kubectl -n infra get secret postgresql -o jsonpath='{.data.postgres-password}' 2>/dev/null | base64 -d || true)
    if [[ -n "$EXISTING_PG_PASS" ]]; then
        log_info "Retrieved PostgreSQL password from database secret (source of truth)"
        APP_DB_PASS="$EXISTING_PG_PASS"
        
        # Verify it matches env var if provided (for validation)
        if [[ -n "$POSTGRES_PASSWORD" && "$POSTGRES_PASSWORD" != "$EXISTING_PG_PASS" ]]; then
            log_warning "POSTGRES_PASSWORD env var differs from database secret"
            log_warning "Using database secret password (must match actual DB)"
        fi
    else
        log_error "Could not retrieve PostgreSQL password from Kubernetes secret"
        log_error ""
        log_error "TROUBLESHOOTING:"
        log_error "1. Verify PostgreSQL secret exists:"
        log_error "   kubectl get secret postgresql -n infra"
        log_error ""
        log_error "2. Check secret data:"
        log_error "   kubectl get secret postgresql -n infra -o jsonpath='{.data}'"
        log_error ""
        log_error "3. Re-run provisioning to create secrets:"
        log_error "   gh workflow run provision.yml --repo Bengo-Hub/devops-k8s"
        exit 1
    fi
else
    log_error "PostgreSQL secret not found in 'infra' namespace"
    log_error ""
    log_error "TROUBLESHOOTING:"
    log_error "1. Check what secrets exist:"
    log_error "   kubectl get secret -n infra | grep -E 'postgres|postgresql'"
    log_error ""
    log_error "2. Verify infra namespace exists:"
    log_error "   kubectl get ns infra"
    log_error ""
    log_error "3. If PostgreSQL is not deployed, run provision:"
    log_error "   gh workflow run provision.yml --repo Bengo-Hub/devops-k8s"
    log_error ""
    log_error "4. Expected secret location: kubectl get secret postgresql -n infra"
    exit 1
fi

log_info "PostgreSQL password retrieved and verified (length: ${#APP_DB_PASS} chars)"

# Get Redis password from shared 'infra' namespace - ALWAYS use the password from the live database
log_info "Retrieving Redis password from shared 'infra' namespace..."
if kubectl -n infra get secret redis >/dev/null 2>&1; then
    REDIS_PASS=$(kubectl -n infra get secret redis -o jsonpath='{.data.redis-password}' 2>/dev/null | base64 -d || true)
    if [[ -n "$REDIS_PASS" ]]; then
        log_info "Retrieved Redis password from database secret (source of truth)"
        
        # Verify it matches env var if provided (for validation)
        if [[ -n "$REDIS_PASSWORD" && "$REDIS_PASSWORD" != "$REDIS_PASS" ]]; then
            log_warning "REDIS_PASSWORD env var differs from database secret"
            log_warning "Using database secret password (must match actual DB)"
        fi
    else
        log_error "Could not retrieve Redis password from Kubernetes secret"
        exit 1
    fi
else
    log_error "Redis secret not found in 'infra' namespace"
    log_error ""
    log_error "TROUBLESHOOTING:"
    log_error "1. Check what secrets exist:"
    log_error "   kubectl get secret -n infra | grep redis"
    log_error ""
    log_error "2. Verify infra namespace exists:"
    log_error "   kubectl get ns infra"
    log_error ""
    log_error "3. If Redis is not deployed, run provision:"
    log_error "   gh workflow run provision.yml --repo Bengo-Hub/devops-k8s"
    log_error ""
    log_error "4. Expected secret location: kubectl get secret redis -n infra"
    exit 1
fi

log_info "Redis password retrieved and verified (length: ${#REDIS_PASS} chars)"

# Get RabbitMQ password from dedicated 'truload' namespace
log_info "Retrieving RabbitMQ password from 'truload' namespace..."
if kubectl -n "$NAMESPACE" get secret rabbitmq >/dev/null 2>&1; then
    RABBITMQ_PASS=$(kubectl -n "$NAMESPACE" get secret rabbitmq -o jsonpath='{.data.rabbitmq-password}' 2>/dev/null | base64 -d || true)
    if [[ -n "$RABBITMQ_PASS" ]]; then
        log_info "Retrieved RabbitMQ password from secret (source of truth)"
        
        # Verify it matches env var if provided (for validation)
        if [[ -n "$RABBITMQ_PASSWORD" && "$RABBITMQ_PASSWORD" != "$RABBITMQ_PASS" ]]; then
            log_warning "RABBITMQ_PASSWORD env var differs from secret"
            log_warning "Using secret password (must match actual RabbitMQ)"
        fi
    else
        log_error "Could not retrieve RabbitMQ password from Kubernetes secret"
        exit 1
    fi
else
    log_warning "RabbitMQ secret not found in '$NAMESPACE' namespace"
    log_warning "Using fallback password or will be installed by provisioning"
    RABBITMQ_PASS="${RABBITMQ_PASSWORD:-rabbitmq}"
fi

log_info "RabbitMQ password retrieved (length: ${#RABBITMQ_PASS} chars)"

log_info "Database credentials retrieved: PostgreSQL (shared), Redis (shared), RabbitMQ (dedicated)"

# CRITICAL: Test database connectivity to verify password is correct
log_step "Verifying PostgreSQL password by testing connection..."

# Clean up any existing test pod first
kubectl delete pod -n "$NAMESPACE" pg-test-conn --ignore-not-found >/dev/null 2>&1

# Run connection test with detailed error capture (use 'postgres' db which always exists)
log_info "Testing connection to postgresql.erp.svc.cluster.local:5432..."
TEST_OUTPUT=$(mktemp)
set +e
kubectl run -n "$NAMESPACE" pg-test-conn --rm -i --restart=Never --image=postgres:15-alpine --timeout=30s \
  --env="PGPASSWORD=$APP_DB_PASS" \
  --command -- psql -h postgresql.erp.svc.cluster.local -U postgres -d postgres -c "SELECT 1;" >$TEST_OUTPUT 2>&1
TEST_RC=$?
set -e

if [[ $TEST_RC -eq 0 ]]; then
    log_success "✓ PostgreSQL password verified - connection successful"
    rm -f $TEST_OUTPUT
else
    log_error "✗ PostgreSQL password verification FAILED (exit code: $TEST_RC)"
    log_error ""
    log_error "Test output:"
    cat $TEST_OUTPUT || true
    rm -f $TEST_OUTPUT
    log_error ""
    log_error "DIAGNOSIS: Password mismatch or connectivity issue"
    log_error "- Secret password length: ${#APP_DB_PASS} chars"
    log_error "- Database host: postgresql.erp.svc.cluster.local:5432"
    log_error "- Test database: postgres"
    log_error ""
    log_error "FIX OPTIONS:"
    log_error "Option A: Reset PostgreSQL password to match the K8s secret (recommended)"
    log_error "  kubectl exec -n erp postgresql-0 -- psql -U postgres -c \"ALTER USER postgres WITH PASSWORD '\$APP_DB_PASS';\""
    log_error ""
    log_error "Option B: Re-run provision workflow to sync passwords"
    log_error "  https://github.com/Bengo-Hub/devops-k8s/actions/workflows/provision.yml"
    log_error ""
    exit 1
fi

# Get cluster IPs for comprehensive network access (for .NET CORS/Host filtering)
log_step "Retrieving cluster IPs for network configuration..."
POD_IPS=$(kubectl get pods -n "$NAMESPACE" -l app=truload-backend-app -o jsonpath='{.items[*].status.podIP}' 2>/dev/null | tr ' ' ',' || true)
SVC_IP=$(kubectl get svc truload-backend -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}' 2>/dev/null || true)
NODE_IPS=$(kubectl get nodes -o jsonpath='{.items[*].status.addresses[?(@.type=="InternalIP")].address}' 2>/dev/null | tr ' ' ',' || true)

# Build comprehensive allowed origins (for .NET CORS)
ALLOWED_ORIGINS="https://truloadtest.masterspace.co.ke,http://localhost:3000,https://localhost:5001"
[[ -n "$SVC_IP" ]] && ALLOWED_ORIGINS="${ALLOWED_ORIGINS},http://${SVC_IP}:8080"
[[ -n "$NODE_IPS" ]] && ALLOWED_ORIGINS="${ALLOWED_ORIGINS},http://${NODE_IPS}:8080"

log_info "Allowed Origins configured: ${ALLOWED_ORIGINS}"

# Generate JWT secret if not provided
JWT_SECRET=${JWT_SECRET:-$(openssl rand -hex 32)}
ASPNET_SECRET=${ASPNET_SECRET:-$(openssl rand -hex 32)}

# Create/update environment secret
log_info "Creating/updating environment secret: ${ENV_SECRET_NAME}"
log_info "Secret will include: PostgreSQL, Redis, RabbitMQ, .NET secrets, CORS config"

# CRITICAL: Delete and recreate to ensure clean state (prevents stale password issues)
kubectl -n "$NAMESPACE" delete secret "$ENV_SECRET_NAME" --ignore-not-found

kubectl -n "$NAMESPACE" create secret generic "$ENV_SECRET_NAME" \
  --from-literal=ConnectionStrings__DefaultConnection="Host=postgresql.erp.svc.cluster.local;Port=5432;Database=truload;Username=postgres;Password=${APP_DB_PASS}" \
  --from-literal=Redis__ConnectionString="redis-master.erp.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False" \
  --from-literal=RabbitMQ__Host="rabbitmq.${NAMESPACE}.svc.cluster.local" \
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

log_success "Environment secret created/updated with production configuration"

# Update KubeSecrets/devENV.yml with verified credentials for consistency
# This ensures local deployments and Helm values use the same verified credentials
if [[ -f "KubeSecrets/devENV.yml" ]]; then
    log_step "Updating KubeSecrets/devENV.yml with verified credentials..."
    
    # Backup existing file
    cp KubeSecrets/devENV.yml KubeSecrets/devENV.yml.bak
    
    # Create updated devENV.yml with verified credentials
    cat > KubeSecrets/devENV.yml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: ${ENV_SECRET_NAME}
  namespace: ${NAMESPACE}
type: Opaque
stringData:
  # Database credentials (verified from K8s secrets)
  ConnectionStrings__DefaultConnection: "Host=postgresql.erp.svc.cluster.local;Port=5432;Database=truload;Username=postgres;Password=${APP_DB_PASS}"
  
  # Redis credentials (verified from K8s secrets)
  Redis__ConnectionString: "redis-master.erp.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False"
  
  # RabbitMQ credentials (verified from K8s secrets)
  RabbitMQ__Host: "rabbitmq.${NAMESPACE}.svc.cluster.local"
  RabbitMQ__Port: "5672"
  RabbitMQ__Username: "user"
  RabbitMQ__Password: "${RABBITMQ_PASS}"
  
  # JWT configuration
  Jwt__Secret: "${JWT_SECRET}"
  Jwt__Issuer: "truload-api"
  Jwt__Audience: "truload-frontend"
  
  # ASP.NET Core secrets
  AspNetCore__Secret: "${ASPNET_SECRET}"
  
  # Environment configuration
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_URLS: "http://+:8080"
  
  # CORS configuration
  Cors__AllowedOrigins: "${ALLOWED_ORIGINS}"
  Cors__AllowCredentials: "true"
  
  # Logging configuration
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft.AspNetCore: "Warning"
EOF

    log_success "✓ KubeSecrets/devENV.yml updated with verified credentials"
    log_info "Backup saved to KubeSecrets/devENV.yml.bak"
else
    log_warning "KubeSecrets/devENV.yml not found - creating new file"
    mkdir -p KubeSecrets
    
    # Create new devENV.yml with verified credentials
    cat > KubeSecrets/devENV.yml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: ${ENV_SECRET_NAME}
  namespace: ${NAMESPACE}
type: Opaque
stringData:
  # Database credentials (verified from K8s secrets)
  ConnectionStrings__DefaultConnection: "Host=postgresql.erp.svc.cluster.local;Port=5432;Database=truload;Username=postgres;Password=${APP_DB_PASS}"
  
  # Redis credentials (verified from K8s secrets)
  Redis__ConnectionString: "redis-master.erp.svc.cluster.local:6379,password=${REDIS_PASS},ssl=False,abortConnect=False"
  
  # RabbitMQ credentials (verified from K8s secrets)
  RabbitMQ__Host: "rabbitmq.${NAMESPACE}.svc.cluster.local"
  RabbitMQ__Port: "5672"
  RabbitMQ__Username: "user"
  RabbitMQ__Password: "${RABBITMQ_PASS}"
  
  # JWT configuration
  Jwt__Secret: "${JWT_SECRET}"
  Jwt__Issuer: "truload-api"
  Jwt__Audience: "truload-frontend"
  
  # ASP.NET Core secrets
  AspNetCore__Secret: "${ASPNET_SECRET}"
  
  # Environment configuration
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_URLS: "http://+:8080"
  
  # CORS configuration
  Cors__AllowedOrigins: "${ALLOWED_ORIGINS}"
  Cors__AllowCredentials: "true"
  
  # Logging configuration
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft.AspNetCore: "Warning"
EOF

    log_success "✓ KubeSecrets/devENV.yml created with verified credentials"
fi

# Export validated credentials for use by parent script
echo "EFFECTIVE_PG_PASS=${APP_DB_PASS}"
echo "EFFECTIVE_REDIS_PASS=${REDIS_PASS}"
echo "EFFECTIVE_RABBITMQ_PASS=${RABBITMQ_PASS}"

