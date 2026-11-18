# Database maintenance

The production database runbook for this service names cluster namespaces, the database admin role
and pod names, so it lives in the private operations docs: `codevertex-private-docs`
`shared-docs-internal/operations/service-runbooks/truload-backend-database-maintenance.md`, next to
the platform-wide `shared-docs-internal/operations/database-maintenance.md`.

Backups are described in `BACKUP_STORAGE.md`. For local development, run PostgreSQL yourself and
point `ConnectionStrings__DefaultConnection` at it.
