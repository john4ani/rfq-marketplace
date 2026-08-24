#!/usr/bin/env bash
set -Eeuo pipefail

application_root="${APPLICATION_ROOT:-/opt/rfq-marketplace}"
current_release="${application_root}/current"
backup_directory="${application_root}/backups"

if [[ ! -L "${current_release}" ]]; then
  echo "FIRST_DEPLOYMENT"
  exit 0
fi

mkdir -p "${backup_directory}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"

cd "${current_release}"
compose_file="docker-compose.yml"
if [[ ! -f "${compose_file}" ]]; then
  # Transition safety: the previous topology release still ships compose.production.yaml.
  compose_file="compose.production.yaml"
fi

for database in compaspot_prod compaspot_staging; do
  # The first dual-environment deploy may find databases that do not exist yet
  # (init-db creates them later in host-deploy.sh). Skip them; only real backup
  # paths may be echoed to stdout (the workflow scps every stdout line).
  if ! docker compose --project-name rfq-marketplace \
      --file "${compose_file}" \
      --env-file .env \
      exec -T postgres \
      sh -c "psql -U \"\$POSTGRES_USER\" -d postgres -tAc \"SELECT 1 FROM pg_database WHERE datname = '${database}'\"" | grep -q 1; then
    echo "Database ${database} does not exist yet — backup skipped." >&2
    continue
  fi

  backup_path="${backup_directory}/${database}-${timestamp}.dump"

  docker compose --project-name rfq-marketplace \
    --file "${compose_file}" \
    --env-file .env \
    exec -T postgres \
    sh -c "pg_dump --format=custom --no-owner --no-privileges --username \"\$POSTGRES_USER\" ${database}" \
    > "${backup_path}"

  test -s "${backup_path}"
  (cd "${backup_directory}" && sha256sum "$(basename "${backup_path}")" > "$(basename "${backup_path}").sha256")
  chmod 600 "${backup_path}" "${backup_path}.sha256"
  echo "${backup_path}"
done
