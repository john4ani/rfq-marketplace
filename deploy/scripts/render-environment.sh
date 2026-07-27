#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: render-environment.sh <parameter-path> <api-image> <output-file>" >&2
  exit 64
fi

parameter_path="${1%/}"
api_image="$2"
output_file="$3"

get_parameter() {
  local name="$1"
  local decrypt="${2:-false}"
  local arguments=(ssm get-parameter --name "${parameter_path}/${name}" --query Parameter.Value --output text)
  if [[ "${decrypt}" == "true" ]]; then
    arguments+=(--with-decryption)
  fi
  aws "${arguments[@]}"
}

api_origin_host="$(get_parameter api-origin-host)"
frontend_origin="$(get_parameter frontend-origin)"
postgres_database="$(get_parameter postgres-database)"
postgres_user="$(get_parameter postgres-user)"
postgres_password="$(get_parameter postgres-password true)"
jwt_signing_key="$(get_parameter jwt-signing-key true)"
smtp_username="$(get_parameter smtp-username true)"
smtp_password="$(get_parameter smtp-password true)"

for value_name in \
  api_origin_host frontend_origin postgres_database postgres_user \
  postgres_password jwt_signing_key smtp_username smtp_password; do
  if [[ -z "${!value_name}" || "${!value_name}" == "None" ]]; then
    echo "Required deployment parameter is missing: ${value_name}" >&2
    exit 78
  fi
done

umask 077
{
  printf 'API_IMAGE=%s\n' "${api_image}"
  printf 'API_ORIGIN_HOST=%s\n' "${api_origin_host}"
  printf 'FRONTEND_ORIGIN=%s\n' "${frontend_origin}"
  printf 'ACME_EMAIL=%s\n' "$(get_parameter acme-email)"
  printf 'DATA_PROTECTION_KEYS_PATH=/opt/rfq-marketplace/shared/data-protection-keys\n'
  printf 'POSTGRES_DB=%s\n' "${postgres_database}"
  printf 'POSTGRES_USER=%s\n' "${postgres_user}"
  printf 'POSTGRES_PASSWORD=%s\n' "${postgres_password}"
  printf 'MARKETPLACE_DATABASE_CONNECTION=Host=postgres;Port=5432;Database=%s;Username=%s;Password=%s\n' \
    "${postgres_database}" "${postgres_user}" "${postgres_password}"
  printf 'JWT_ISSUER=%s\n' "$(get_parameter jwt-issuer)"
  printf 'JWT_AUDIENCE=%s\n' "$(get_parameter jwt-audience)"
  printf 'JWT_SIGNING_KEY=%s\n' "${jwt_signing_key}"
  printf 'SMTP_HOST=%s\n' "$(get_parameter smtp-host)"
  printf 'SMTP_PORT=%s\n' "$(get_parameter smtp-port)"
  printf 'SMTP_USERNAME=%s\n' "${smtp_username}"
  printf 'SMTP_PASSWORD=%s\n' "${smtp_password}"
  printf 'SMTP_FROM_ADDRESS=%s\n' "$(get_parameter smtp-from-address)"
  printf 'SMTP_FROM_NAME=%s\n' "$(get_parameter smtp-from-name)"
} > "${output_file}"

chmod 600 "${output_file}"
