#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: host-deploy.sh <release-id> <api-image-tar>" >&2
  exit 64
fi

release_id="$1"
image_tar="$2"
application_root="${APPLICATION_ROOT:-/opt/rfq-marketplace}"
release_directory="${application_root}/releases/${release_id}"
current_link="${application_root}/current"
previous_release=""

if [[ ! "${release_id}" =~ ^[0-9a-f]{7,40}$ ]]; then
  echo "Release id must be a Git commit SHA." >&2
  exit 64
fi

for required_file in \
  "${release_directory}/compose.production.yaml" \
  "${release_directory}/Caddyfile" \
  "${release_directory}/.env" \
  "${image_tar}"; do
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

  echo "Deployment failed. Returning to the previous application release." >&2
  if [[ -n "${previous_release}" && -d "${previous_release}" ]]; then
    ln -sfn "${previous_release}" "${current_link}"
    cd "${previous_release}"
    docker compose --project-name rfq-marketplace \
      --file compose.production.yaml \
      --env-file .env \
      up --detach --remove-orphans postgres api caddy || true
  fi
  exit "${exit_code}"
}
trap rollback ERR

sudo install -d -m 0700 -o 1654 -g 1654 "${application_root}/shared/data-protection-keys"
docker load --input "${image_tar}"

cd "${release_directory}"
docker compose --project-name rfq-marketplace \
  --file compose.production.yaml \
  --env-file .env \
  config --quiet

docker compose --project-name rfq-marketplace \
  --file compose.production.yaml \
  --env-file .env \
  --profile tools \
  run --rm migrate

ln -sfn "${release_directory}" "${current_link}"

docker compose --project-name rfq-marketplace \
  --file compose.production.yaml \
  --env-file .env \
  up --detach --remove-orphans postgres api caddy

for attempt in {1..24}; do
  if docker compose --project-name rfq-marketplace \
      --file compose.production.yaml \
      --env-file .env \
      exec -T api \
      curl --fail --silent --show-error http://127.0.0.1:8080/health/ready >/dev/null; then
    trap - ERR
    echo "Release ${release_id} is ready."
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
exit 1
