#!/usr/bin/env bash
set -Eeuo pipefail

# Grants the Administrator Identity role to the account with the given e-mail (idempotent).
#
# Usage: grant-admin.sh <email> [prod|staging]
#
# Run on the host from a release directory (where docker-compose.yml and .env live), e.g.:
#   cd /opt/rfq-marketplace/releases/<sha>
#   bash scripts/grant-admin.sh john4ani@gmail.com
#
# The account must already exist: Administrator assignment is an explicit operational Identity
# action (ADR-0021); the application does not elevate a configured e-mail automatically. If the
# account is missing, register it through the UI first, then re-run this script.

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: grant-admin.sh <email> [prod|staging]" >&2
  exit 64
fi

email="$1"
target="${2:-prod}"
case "${target}" in
  prod)    database="compaspot_prod" ;;
  staging) database="compaspot_staging" ;;
  *) echo "Unknown target '${target}'; use prod or staging." >&2; exit 64 ;;
esac

postgres_user="$(grep -E '^POSTGRES_USER=' .env 2>/dev/null | head -n 1 | cut -d= -f2- | tr -d '"')"
postgres_user="${postgres_user:-marketplace}"
# Escape single quotes so the e-mail is safe inside the SQL literal.
escaped_email="${email//\'/\'\'}"

psql() {
  docker compose --project-name rfq-marketplace \
    --file docker-compose.yml \
    --env-file .env \
    exec -T postgres \
    psql -U "${postgres_user}" -d "${database}" -v ON_ERROR_STOP=1 -tA -c "$1"
}

user_id="$(psql "SELECT u.\"Id\" FROM \"AspNetUsers\" u WHERE u.\"NormalizedEmail\" = upper('${escaped_email}') OR u.\"Email\" ILIKE '${escaped_email}' LIMIT 1;")"
if [[ -z "${user_id}" ]]; then
  echo "No account found for '${email}' in ${database}. Register the account first (CompaSpot > Create account), then re-run this script." >&2
  exit 66
fi

role_id="$(psql "SELECT r.\"Id\" FROM \"AspNetRoles\" r WHERE r.\"Name\" = 'Administrator' LIMIT 1;")"
if [[ -z "${role_id}" ]]; then
  echo "The Administrator role is missing in ${database} (roles are seeded at API startup)." >&2
  exit 66
fi

psql "INSERT INTO \"AspNetUserRoles\" (\"UserId\", \"RoleId\") VALUES ('${user_id}', '${role_id}') ON CONFLICT DO NOTHING;"
echo "Administrator role granted to ${email} in ${database}."
