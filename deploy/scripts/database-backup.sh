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
backup_path="${backup_directory}/marketplace-${timestamp}.dump"

cd "${current_release}"
docker compose --project-name rfq-marketplace \
  --file compose.production.yaml \
  --env-file .env \
  exec -T postgres \
  sh -c 'pg_dump --format=custom --no-owner --no-privileges --username "$POSTGRES_USER" "$POSTGRES_DB"' \
  > "${backup_path}"

test -s "${backup_path}"
(cd "${backup_directory}" && sha256sum "$(basename "${backup_path}")" > "$(basename "${backup_path}").sha256")
chmod 600 "${backup_path}" "${backup_path}.sha256"
echo "${backup_path}"
