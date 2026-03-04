# Backup storage

Database backups (pg_dump) are written to a configurable path. Configure via setting **`backup.storage_path`** (e.g. in System Config or database ApplicationSettings). Default: `./backups` (relative to app directory) or `/mnt/data/backups` in production.

## Production permissions (avoid backup permission errors)

The container runs as non-root user **appuser (uid 1001)**. The backup directory (and any mounted volume) must be writable by this user.

### 1. Use a dedicated PVC for backups

In Helm values (e.g. devops-k8s `apps/truload-backend/values.yaml`):

```yaml
env:
  - name: backup__storage_path
    value: /mnt/backups
volumeMounts:
  - name: backups
    mountPath: /mnt/backups
volumes:
  - name: backups
    persistentVolumeClaim:
      claimName: truload-backend-backups
```

Then set the application setting **`backup.storage_path`** to `/mnt/backups` (via System Config → Backup, or seed/default).

### 2. Make the volume writable by uid 1001

**Option A – Pod securityContext (recommended):**

```yaml
securityContext:
  runAsUser: 1001
  runAsGroup: 1001
  fsGroup: 1001
```

**Option B – Init container to fix ownership:**

```yaml
initContainers:
  - name: fix-backup-permissions
    image: busybox
    command: ["sh", "-c", "mkdir -p /mnt/backups && chown -R 1001:1001 /mnt/backups"]
    volumeMounts:
      - name: backups
        mountPath: /mnt/backups
```

### 3. Dockerfile defaults

The image creates `/app/backups` and sets ownership to `appuser:appuser`. If you do not mount a volume, backups use this directory (and are lost on container restart). For production, always mount a PVC and set `backup.storage_path` to that path.
