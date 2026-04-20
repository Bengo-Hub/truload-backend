#!/usr/bin/env bash
# Database migration script for TruLoad Backend (.NET/EF Core)
#
# EF Core migrations are applied automatically at application startup via
# db.Database.Migrate() in Program.cs using a direct PostgreSQL connection
# (bypassing PgBouncer). This script is a no-op kept for build pipeline
# compatibility.

log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m $1"; }
log_info()    { echo -e "\033[0;34m[INFO]\033[0m $1"; }

log_info "EF Core migrations are applied at application startup (Program.cs)."
log_info "The application connects directly to PostgreSQL (bypassing PgBouncer)"
log_info "and calls db.Database.Migrate() before serving traffic."
log_success "Migration step complete — no pre-deploy action needed."
