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
kubectl exec postgresql-0 -n infra -- /bin/bash -c "export PGPASSWORD='<ADMIN_PASSWORD>'; psql -h 127.0.0.1 -U admin_user -d postgres -c `"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'truload' AND pid != pg_backend_pid();`""
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

## Known Monitoring Gap: Silent Migration Failures and Inert Background Consumers

Documented 2026-09-04 as a recommendation, not yet built. Two live incidents from the same
initiative both proved the same thing: `kubectl get pods` and the app's own health check can say
everything is fine while the database and background messaging are both silently wrong. Neither
failure class has any alert or dashboard today.

**Evidence 1 - a swallowed EF migration failure.** Migration
`20260903113525_AddTariffRateBasisBillingPeriodAndAccrual` failed on every deploy with
`must be owner of table commercial_tariff_rules`. EF applies migrations as one ordered batch, so
this silently blocked every later migration and every seeder that runs after them. `Program.cs`
catches the migration failure without aborting startup, so the pod reported healthy while
`__EFMigrationsHistory` stayed frozen at an old migration - confirmed only by a direct,
read-only `kubectl exec` query against the live database, not by any CI or health-check signal.

**Evidence 2 - a NATS consumer that's configured but never subscribed.** `AuthDemoSyncService`
never ran in production: the pod logs `NATS auth-demo sync disabled (Nats:Enabled=false)` because
no environment override exists anywhere, so `appsettings.json`'s `false` default wins. The service
was deployed, CI-green, and "done" by every pipeline signal for as long as that flag stayed
unset - again, only a direct pod-log/env inspection surfaced it.

**Recommendation.** Both would be closed by two small, checkable additions rather than a new
subsystem:
- **Migration-currency alert/panel**: compare the latest applied row in `__EFMigrationsHistory`
  against the number of migration files under `Migrations/` in the repo (or against a build-time
  constant recording the expected latest migration name); alert when they disagree for more than
  one deploy cycle. This directly catches the "CI green, schema stale" failure mode Evidence 1
  hit.
- **Explicit consumer-subscription log line or metric**: on startup, `AuthDemoSyncService` (and
  any similar background NATS consumer) should log or emit a metric explicitly stating whether it
  actually subscribed, distinct from the existing "disabled by config" log line, so an operator
  (or an alert rule) can tell "intentionally off" apart from "should be on but silently isn't
  subscribed" at a glance.

This is left as a documented recommendation rather than implemented here - it's a docs-only pass,
and the concrete fix (an alert rule or a startup metric) belongs with whatever this project's
alerting stack actually is, which this pass did not investigate.
