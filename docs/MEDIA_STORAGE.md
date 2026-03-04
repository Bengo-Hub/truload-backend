# Media storage (organisation branding)

Uploaded files (tenant platform logo, organisation logo, login page image) are stored on the backend.

## Configuration

- **Development**: Files are stored under `wwwroot/media` (relative to content root). No extra config.
- **Production**: Use a persistent volume so uploads survive pod restarts.

### Environment

Set one of:

- **`MEDIA_STORAGE_PATH`** (env): Absolute path where uploads are stored (e.g. `/mnt/tuload-backend-media`).
- **`Media:StoragePath`** (appsettings / config): Same as above.

### Production (K8s / devops-k8s)

1. **Create a PVC** (e.g. `tuload-backend-media`) in the cluster and mount it into the backend deployment at a path such as `/mnt/tuload-backend-media`.
2. **Set env** on the backend deployment:
   - `MEDIA_STORAGE_PATH=/mnt/tuload-backend-media`
3. In **build.sh** / **deploy.yml** (or in devops-k8s `apps/truload-backend/values.yaml`), add the volume and volumeMount for the backend pod, and the env var above.

Example (Helm values):

```yaml
# apps/truload-backend/values.yaml (in devops-k8s)
env:
  - name: MEDIA_STORAGE_PATH
    value: /mnt/tuload-backend-media
volumeMounts:
  - name: media
    mountPath: /mnt/tuload-backend-media
volumes:
  - name: media
    persistentVolumeClaim:
      claimName: truload-backend-media
```

The backend serves uploaded files at **`/media/*`**. Ensure ingress/proxy allows access to `/media` if the frontend loads images from the API origin.

## Production permissions (avoid upload permission errors)

The container runs as non-root user **appuser (uid 1001)**. Mounted volumes must be writable by this user.

**Option A – Recommended: use Pod securityContext `fsGroup`** so the volume is writable by the process:

```yaml
securityContext:
  runAsUser: 1001
  runAsGroup: 1001
  fsGroup: 1001
```

**Option B – Init container to fix ownership** (if the volume is created with root ownership):

```yaml
initContainers:
  - name: fix-media-permissions
    image: busybox
    command: ["sh", "-c", "chown -R 1001:1001 /mnt/tuload-backend-media"]
    volumeMounts:
      - name: media
        mountPath: /mnt/tuload-backend-media
```

**Option C – PVC with correct storageClass** so the provisioned volume is created with mode `0777` or ownership that allows uid 1001 to write (depends on your storage driver).

At startup the app checks that the media directory exists and is writable; if not, it logs a warning and uploads will fail until permissions are fixed.
