# Database Maintenance Procedures

This document records operational procedures for maintaining the TruLoad database within the Kubernetes cluster.

## Database Reset and pgvector Enablement

These commands were used on February 26, 2026, to drop and recreate the `truload` database and enable the `pgvector` extension.

### 1. Identify Resources
- **PostgreSQL Pod:** `postgresql-0` (Namespace: `infra`)
- **Backend Deployment:** `truload-backend` (Namespace: `truload`)

### 2. Preparation: Scale Down Backend
To ensure no active connections to the database:
```powershell
kubectl scale deployment truload-backend -n truload --replicas=0
```

### 3. Terminate Active Sessions
If the database is still being accessed, terminate sessions from the `postgres` or `admin_user` context:
```powershell
kubectl exec postgresql-0 -n infra -- /bin/bash -c "export PGPASSWORD='<ADMIN_PASSWORD>'; psql -h 127.0.0.1 -U admin_user -d postgres -c \"SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = 'truload' AND pid <> pg_backend_pid();\""
```

### 4. Drop and Recreate Database
```powershell
kubectl exec postgresql-0 -n infra -- /bin/bash -c "export PGPASSWORD='<ADMIN_PASSWORD>'; dropdb -h 127.0.0.1 -U admin_user truload --if-exists; createdb -h 127.0.0.1 -U admin_user truload"
```

### 5. Fix Database Ownership
When creating a database with a superuser, you must transfer ownership to the application user:
```powershell
kubectl exec postgresql-0 -n infra -- /bin/bash -c "export PGPASSWORD='<ADMIN_PASSWORD>'; psql -h 127.0.0.1 -U admin_user -d postgres -c 'ALTER DATABASE truload OWNER TO truload_user;'"
```

### 6. Enable pgvector Extension
```powershell
kubectl exec postgresql-0 -n infra -- /bin/bash -c "export PGPASSWORD='<ADMIN_PASSWORD>'; psql -h 127.0.0.1 -U admin_user -d truload -c 'CREATE EXTENSION IF NOT EXISTS vector;'"
```

### 7. Reconcile PVC Binding (If Pods are Pending)
If pods are stuck in `Pending` due to `unbound immediate PersistentVolumeClaims`, the PVC might have a stale `volumeName`. Recreate it:
1. Scale down: `kubectl scale deployment truload-backend -n truload --replicas=0`
2. Delete PVC: `kubectl delete pvc truload-backend-media -n truload`
3. Wait for recreation (ArgoCD) or recreate manually.
4. Scale up: `kubectl scale deployment truload-backend -n truload --replicas=2`

### 8. Restore Backend Deployment
```powershell
kubectl rollout restart deployment truload-backend -n truload
```

### 7. Verification
Check if the pods are running and healthy:
```powershell
kubectl get pods -n truload
```
