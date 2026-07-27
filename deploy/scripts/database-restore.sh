#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || "${CONFIRM_DATABASE_RESTORE:-}" != "RESTORE" ]]; then
  echo "Usage: CONFIRM_DATABASE_RESTORE=RESTORE database-restore.sh <backup.dump>" >&2
  echo "This operation replaces the current marketplace database." >&2
  exit 64
fi

backup_path="$(readlink -f "$1")"
application_root="${APPLICATION_ROOT:-/opt/rfq-marketplace}"
current_release="${application_root}/current"

if [[ ! -s "${backup_path}" || ! -L "${current_release}" ]]; then
  echo "A non-empty backup and an active release are required." >&2
  exit 66
fi

cd "${current_release}"
compose=(docker compose --project-name rfq-marketplace --file compose.production.yaml --env-file .env)

"${compose[@]}" stop api caddy
# These variables intentionally expand inside the PostgreSQL container.
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c 'dropdb --if-exists --maintenance-db=postgres --username "$POSTGRES_USER" "$POSTGRES_DB"'
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c 'createdb --username "$POSTGRES_USER" "$POSTGRES_DB"'
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c \
  'pg_restore --exit-on-error --clean --if-exists --no-owner --no-privileges --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"' \
  < "${backup_path}"
"${compose[@]}" up --detach api caddy

echo "Database restored from ${backup_path}. Run the acceptance smoke tests immediately."
