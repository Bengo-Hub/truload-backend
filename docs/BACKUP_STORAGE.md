# Backup storage

TruLoad backs up its database with `pg_dump` on the schedule in **`backup.schedule_cron`**
(default `0 2 * * *`, UTC) and from **System Config, Backup**. Backups are kept at a backup
destination, not on the server's disk.

## Where backups go

1. **A destination chosen by an administrator.** Security setup, Backup destination (S3, MinIO,
   R2, OneDrive, Google Drive, WebDAV, SFTP or SMB). It is stored in the setting
   `backup.destination` with its secrets encrypted, and used through rclone.
2. **Otherwise the platform's private R2 bucket.** When no destination is chosen and the pod has
   the shared `object-storage` Secret (`OBJECT_STORAGE_*`), backups go to the private bucket under
   `truload/backups/<database>/`, for example `truload/backups/truload/truload_backup_full_20261118_020000.sql`.
   The credentials stay in the Secret; nothing is written to the database. This is how production
   runs.
3. **No destination at all** (local development): files stay in `backup.storage_path` and every
   run logs a warning.

## How a backup runs

1. `pg_dump` writes `truload_backup_<type>_<yyyyMMdd_HHmmss>.sql` into `backup.storage_path`
   (`/app/backups/truload`). In production that path is an `emptyDir` volume used only for staging.
2. The file is uploaded to the destination. When the upload succeeds the local file is deleted, so
   the destination holds the only copy.
3. When the upload fails the run is reported as failed, the newest dump stays in staging (older
   staged dumps are dropped so the volume cannot fill) and the next successful run uploads it.
4. The same job then deletes backups older than **`backup.retention_days`** at the destination.

The backup list, download, validate and restore all read from the destination. Download and
restore fetch the file into staging first; the copy is removed when the download stream closes or
the restore finishes. Only file names of the form above are accepted, so a request cannot reach
other objects in the bucket.

## Production settings (devops-k8s `apps/truload-backend/values.yaml`)

```yaml
objectStorage:
  enabled: true            # mounts the object-storage Secret (OBJECT_STORAGE_*)
volumeMounts:
  - name: backup-staging
    mountPath: /app/backups/truload
volumes:
  - name: backup-staging
    emptyDir:
      sizeLimit: 2Gi
```

The pod's `fsGroup: 1001` makes the `emptyDir` writable by the app user. Keep the size limit above
twice the size of one dump (a restore can download while a staged dump waits); a full dump was
about 77 MB in November 2026.

## Checking backups

From a backend pod, without printing credentials:

```sh
export RCLONE_CONFIG_R2_TYPE=s3 RCLONE_CONFIG_R2_PROVIDER=Cloudflare \
  RCLONE_CONFIG_R2_ENDPOINT="$OBJECT_STORAGE_ENDPOINT" \
  RCLONE_CONFIG_R2_ACCESS_KEY_ID="$OBJECT_STORAGE_ACCESS_KEY_ID" \
  RCLONE_CONFIG_R2_SECRET_ACCESS_KEY="$OBJECT_STORAGE_SECRET_ACCESS_KEY" \
  RCLONE_CONFIG_R2_NO_CHECK_BUCKET=true RCLONE_CONFIG_R2_NO_HEAD=true
rclone --config /dev/null lsl "r2:$OBJECT_STORAGE_PRIVATE_BUCKET/truload/backups/truload/"
```

`NO_HEAD` matters with the rclone in the image (1.60): without it R2 answers the check that
follows each upload with 501 Not Implemented and the copy is reported as failed even though the
object was stored.

The platform's nightly `pg_dumpall` of the whole PostgreSQL cluster (devops-k8s
`docs/object-storage.md`, Platform database backup) is a second, independent copy that also
covers the `kuraweigh` database.

## History

Until November 2026 dumps were written to the PVC `truload-backups` (20Gi) and kept there. That
PVC was removed on 2026-11-18 after its last contents were archived to the private bucket under
`truload/pvc-archive/2026-11-18/truload-backups/`.
