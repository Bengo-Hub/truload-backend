# Media storage (organisation branding and files)

Uploaded branding images (tenant platform logo, organisation logo, login page image) and files
saved through `IBlobStorageService` are kept in the platform's private R2 bucket, the same object
storage every Codevertex service uses (devops-k8s `docs/object-storage.md`).

## Keys and URLs

| What | Object key | URL stored and returned |
| --- | --- | --- |
| Branding upload (`POST /api/v1/media/upload`) | `truload/media/<folder>/<uuid>.<ext>` | `/media/<folder>/<uuid>.<ext>` |
| `IBlobStorageService.SaveAsync` | `truload/files/<folder>/<uuid>.<ext>` | relative path `<folder>/<uuid>.<ext>` |

The URL did not change with the move to object storage: `/media/...` is still what the database
and the frontend hold (`getMediaUrl` in truload-frontend prefixes it with the API origin). The
backend streams `/media/<path>` from the key `truload/media/<path>`. The bucket stays private:
objects are only ever read through the API. Branding images appear on the login page, so this
route needs no sign-in, as the old static files did. Images bundled with the app (`/images/...`,
`/technical/...`) are served from `wwwroot` as before.

## Configuration

Object storage is switched on by the `OBJECT_STORAGE_*` variables from the namespace's shared
`object-storage` Secret (`objectStorage.enabled: true` in the Helm values):
`OBJECT_STORAGE_ENDPOINT`, `OBJECT_STORAGE_ACCESS_KEY_ID`, `OBJECT_STORAGE_SECRET_ACCESS_KEY`,
`OBJECT_STORAGE_REGION` and `OBJECT_STORAGE_PRIVATE_BUCKET`. No bucket name or credential is in
code or appsettings.

Without them (local development) uploads go to `Media:StoragePath` or `MEDIA_STORAGE_PATH`
(default `wwwroot/media`) and are served from there. A file still present on local disk is always
served first; anything else under `/media` is read from the bucket.

## Moving local files to the bucket

```sh
dotnet truload-backend.dll migrate-media            # report only
dotnet truload-backend.dll migrate-media --apply    # upload
```

It reads `/app/uploads` and the media path (or the directories given after the flags), uploads each
file to `truload/media/<path relative to that directory>`, skips objects that already exist with
the same size, and ends with a count and size check. It exits 0 only when every local file has a
matching object, so it is safe to run again. It does not start the web host.

## History

Until November 2026 production mounted the PVC `truload-backend-media` (10Gi) at `/app/uploads`,
but uploads were actually written to `wwwroot/media` inside the container and lost on every
restart. On 2026-11-18 the PVC held no files and no row in the `truload` or `kuraweigh` database
referenced a `/media/` URL, so there was nothing to move. The PVC was removed that day.
