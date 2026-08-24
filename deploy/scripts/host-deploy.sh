#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: host-deploy.sh <release-id> <api-image-tar> <frontend-tar>" >&2
  exit 64
fi

release_id="$1"
image_tar="$2"
frontend_tar="$3"
application_root="${APPLICATION_ROOT:-/opt/rfq-marketplace}"
release_directory="${application_root}/releases/${release_id}"
current_link="${application_root}/current"
previous_release=""

if [[ ! "${release_id}" =~ ^[0-9a-f]{7,40}$ ]]; then
  echo "Release id must be a Git commit SHA." >&2
  exit 64
fi

for required_file in \
  "${release_directory}/docker-compose.yml" \
  "${release_directory}/Caddyfile" \
  "${release_directory}/.env" \
  "${release_directory}/.env.prod" \
  "${release_directory}/.env.staging" \
  "${image_tar}" \
  "${frontend_tar}"; do
  if [[ ! -f "${required_file}" ]]; then
    echo "Required deployment file is missing: ${required_file}" >&2
    exit 66
  fi
done

if [[ -L "${current_link}" ]]; then
  previous_release="$(readlink -f "${current_link}")"
fi

# Invoked indirectly by the ERR trap.
# shellcheck disable=SC2329
rollback() {
  exit_code=$?
  if [[ ${exit_code} -eq 0 ]]; then
    return
  fi

  echo "Deployment failed (exit ${exit_code}); rolling back to the previous release." >&2
  dump_diagnostics
  if [[ -n "${previous_release}" && -d "${previous_release}" ]]; then
    ln -sfn "${previous_release}" "${current_link}"
    cd "${previous_release}"
    local compose_file
    local services
    if [[ -f docker-compose.yml ]]; then
      # Current dual-environment topology.
      compose_file="docker-compose.yml"
      services="postgres backend-prod backend-staging caddy"
    else
      # Pre-dual-environment releases used compose.production.yaml with a single `api` service.
      compose_file="compose.production.yaml"
      services="postgres api caddy"
    fi
    docker compose --project-name rfq-marketplace \
      --file "${compose_file}" \
      --env-file .env \
      up --detach --remove-orphans ${services} || true
  else
    rm -f "${current_link}"
    cd "${release_directory}"
    docker compose --project-name rfq-marketplace \
      --file docker-compose.yml \
      --env-file .env \
      stop backend-prod backend-staging caddy || true
  fi
  exit "${exit_code}"
}
trap rollback ERR

# Best-effort capture of the failing containers before the previous release is restored.
# shellcheck disable=SC2329
dump_diagnostics() {
  {
    echo '--- docker compose ps ---'
    docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env ps || true
    echo '--- backend-prod state ---'
    docker inspect rfq-marketplace-backend-prod-1 --format '{{json .State}}' 2>/dev/null || true
    echo '--- backend-prod logs (tail 100) ---'
    docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env logs --tail=100 backend-prod || true
    echo '--- backend-staging logs (tail 40) ---'
    docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env logs --tail=40 backend-staging || true
  } >&2 || true
}

# Shared state: per-environment Data Protection keys (isolated cookie/data-protection realms) and the
# SPA static files served by Caddy. Frontend dirs are never replaced as a whole (bind mounts keep
# their inode); files are swapped in-place instead.
sudo install -d -m 0700 -o 1654 -g 1654 "${application_root}/shared/data-protection-keys-prod"
sudo install -d -m 0700 -o 1654 -g 1654 "${application_root}/shared/data-protection-keys-staging"
sudo install -d -m 0755 -o 1654 -g 1654 "${application_root}/shared/frontend-prod"
sudo install -d -m 0755 -o 1654 -g 1654 "${application_root}/shared/frontend-staging"

docker load --input "${image_tar}"

staging_tmp="$(mktemp -d)"
tar -xzf "${frontend_tar}" -C "${staging_tmp}"
for frontend_dir in "${application_root}/shared/frontend-prod" "${application_root}/shared/frontend-staging"; do
  sudo find "${frontend_dir}" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
  sudo cp -a "${staging_tmp}/." "${frontend_dir}/"
done
rm -rf "${staging_tmp}"

cd "${release_directory}"
docker compose --project-name rfq-marketplace \
  --file docker-compose.yml \
  --env-file .env \
  config --quiet

# Create the two databases (idempotent), then apply migrations to each.
docker compose --project-name rfq-marketplace \
  --file docker-compose.yml \
  --env-file .env \
  --profile tools \
  run --rm init-db

docker compose --project-name rfq-marketplace \
  --file docker-compose.yml \
  --env-file .env \
  --profile tools \
  run --rm migrate-prod

docker compose --project-name rfq-marketplace \
  --file docker-compose.yml \
  --env-file .env \
  --profile tools \
  run --rm migrate-staging

ln -sfn "${release_directory}" "${current_link}"

docker compose --project-name rfq-marketplace \
  --file docker-compose.yml \
  --env-file .env \
  up --detach --remove-orphans postgres backend-prod backend-staging caddy

for attempt in {1..24}; do
  if docker compose --project-name rfq-marketplace \
      --file docker-compose.yml \
      --env-file .env \
      exec -T backend-prod \
      sh -c 'curl --fail --silent --show-error --header "Host: $API_ORIGIN_HOST" http://127.0.0.1:8080/health/ready' >/dev/null \
    && docker compose --project-name rfq-marketplace \
      --file docker-compose.yml \
      --env-file .env \
      exec -T backend-staging \
      sh -c 'curl --fail --silent --show-error http://127.0.0.1:8080/health/ready' >/dev/null; then
    trap - ERR
    echo "Release ${release_id} is ready (Prod + Staging)."
    find "${application_root}/releases" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' \
      | sort -nr \
      | tail -n +4 \
      | cut -d' ' -f2- \
      | xargs --no-run-if-empty rm -rf
    docker image prune --force --filter "until=168h" >/dev/null
    exit 0
  fi
  echo "Readiness attempt ${attempt}/24 has not passed yet."
  sleep 5
done

echo "Release ${release_id} did not become ready within 120 seconds." >&2
{
  echo '--- docker compose ps ---'
  docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env ps || true
  echo '--- backend-prod logs (tail 60) ---'
  docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env logs --tail=60 backend-prod || true
  echo '--- backend-staging logs (tail 60) ---'
  docker compose --project-name rfq-marketplace --file docker-compose.yml --env-file .env logs --tail=60 backend-staging || true
} >&2
exit 1
