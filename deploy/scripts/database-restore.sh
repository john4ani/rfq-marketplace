#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 2 || "${CONFIRM_DATABASE_RESTORE:-}" != "RESTORE" ]]; then
  echo "Usage: CONFIRM_DATABASE_RESTORE=RESTORE database-restore.sh <database> <backup.dump>" >&2
  echo "  <database>   compaspot_prod or compaspot_staging" >&2
  echo "This operation replaces the given database." >&2
  exit 64
fi

database="$1"
case "${database}" in
  compaspot_prod|compaspot_staging) ;;
  *) echo "Unknown database '${database}'. Expected compaspot_prod or compaspot_staging." >&2; exit 64 ;;
esac

backup_path="$(readlink -f "$2")"
application_root="${APPLICATION_ROOT:-/opt/rfq-marketplace}"
current_release="${application_root}/current"

if [[ ! -s "${backup_path}" || ! -L "${current_release}" ]]; then
  echo "A non-empty backup and an active release are required." >&2
  exit 66
fi

cd "${current_release}"
compose_file="docker-compose.yml"
if [[ ! -f "${compose_file}" ]]; then
  # Transition safety: the previous topology release still ships compose.production.yaml.
  compose_file="compose.production.yaml"
fi
compose=(docker compose --project-name rfq-marketplace --file "${compose_file}" --env-file .env)

"${compose[@]}" stop backend-prod backend-staging caddy
# These variables intentionally expand inside the PostgreSQL container.
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c 'dropdb --if-exists --maintenance-db=postgres --username "$POSTGRES_USER" "'"${database}"'"'
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c 'createdb --username "$POSTGRES_USER" "'"${database}"'"'
# shellcheck disable=SC2016
"${compose[@]}" exec -T postgres sh -c \
  'pg_restore --exit-on-error --clean --if-exists --no-owner --no-privileges --username "$POSTGRES_USER" --dbname "'"${database}"'"' \
  < "${backup_path}"
"${compose[@]}" up --detach backend-prod backend-staging caddy

echo "Database ${database} restored from ${backup_path}. Run the acceptance smoke tests immediately."
