#!/usr/bin/env bash
# Creates the prod and staging databases on the single shared PostgreSQL 17 instance.
# Idempotent: safe to run on every deploy, including against pre-existing data volumes
# (the postgres image only honours POSTGRES_DB on a brand-new data directory).
set -Eeuo pipefail

export PGHOST="${PGHOST:-postgres}"
export PGPORT="${PGPORT:-5432}"
export PGUSER="${POSTGRES_USER:?POSTGRES_USER is required}"
export PGPASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"

for database in compaspot_prod compaspot_staging; do
  if [[ "$(psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '${database}'")" == "1" ]]; then
    echo "Database ${database} already exists."
  else
    psql -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"${database}\""
    echo "Created database ${database}."
  fi
done

echo "Database provisioning complete."
