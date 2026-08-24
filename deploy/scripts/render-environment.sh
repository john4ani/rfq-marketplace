#!/usr/bin/env bash
set -Eeuo pipefail

# Usage: render-environment.sh <parameter-path> <api-image> <output-dir>
#
# Renders the three files consumed by deploy/docker-compose.yml into <output-dir>:
#   .env           — compose interpolation (API_IMAGE, POSTGRES_USER/PASSWORD, ACME_EMAIL, PROD_SITE).
#   .env.prod      — backend-prod container environment (DB compaspot_prod, S3 prefix prod/, compaspot.com).
#   .env.staging   — backend-staging container environment (SAME shared API credentials as Prod, isolated by
#                    DB compaspot_staging + S3 prefix staging/ + feature flags OFF; internal-only origin).
#
# Every secret comes from AWS SSM under ${parameter_path}. Staging deliberately shares the Prod
# credentials — per the environment addendum, isolation is configuration/DB-only, no duplicate paid accounts.

if [[ $# -ne 3 ]]; then
  echo "Usage: render-environment.sh <parameter-path> <api-image> <output-dir>" >&2
  exit 64
fi

parameter_path="${1%/}"
api_image="$2"
output_dir="$3"

mkdir -p "${output_dir}"

get_parameter() {
  local name="$1"
  local decrypt="${2:-false}"
  local arguments=(ssm get-parameter --name "${parameter_path}/${name}" --query Parameter.Value --output text)
  if [[ "${decrypt}" == "true" ]]; then
    arguments+=(--with-decryption)
  fi
  aws "${arguments[@]}"
}

get_optional_parameter() {
  local name="$1"
  local decrypt="${2:-false}"
  local default="${3:-}"
  local value
  value="$(get_parameter "${name}" "${decrypt}" 2>/dev/null || true)"
  if [[ -z "${value}" || "${value}" == "None" ]]; then
    printf '%s' "${default}"
  else
    printf '%s' "${value}"
  fi
}

required_parameter() {
  local value_name="$1"
  if [[ -z "${!value_name}" || "${!value_name}" == "None" ]]; then
    echo "Required deployment parameter is missing: ${value_name}" >&2
    exit 78
  fi
}

api_origin_host="$(get_parameter api-origin-host)"
frontend_origin="$(get_parameter frontend-origin)"
postgres_database="$(get_parameter postgres-database)"
postgres_user="$(get_parameter postgres-user)"
postgres_password="$(get_parameter postgres-password true)"
jwt_issuer="$(get_parameter jwt-issuer)"
jwt_audience="$(get_parameter jwt-audience)"
jwt_signing_key="$(get_parameter jwt-signing-key true)"
acme_email="$(get_parameter acme-email)"
instance_name="$(get_parameter instance-name)"
email_enabled="$(get_parameter email-enabled)"
email_host="$(get_parameter email-host)"
email_port="$(get_parameter email-port)"
email_use_tls="$(get_parameter email-use-tls)"
email_from_address="$(get_parameter email-from-address)"
email_from_name="$(get_parameter email-from-name)"
email_reply_to_address="$(get_parameter email-reply-to-address)"

for value_name in \
  api_origin_host frontend_origin postgres_database postgres_user \
  postgres_password jwt_issuer jwt_audience jwt_signing_key \
  acme_email instance_name email_enabled email_host email_port email_use_tls \
  email_from_address email_from_name email_reply_to_address; do
  required_parameter "${value_name}"
done

case "${email_enabled,,}" in
  true|false) ;;
  *)
    echo "Required deployment parameter email-enabled must be exactly true or false." >&2
    exit 78
    ;;
esac

if [[ "${email_enabled,,}" == "true" ]]; then
  for value_name in email_host email_port email_use_tls email_from_address email_from_name email_reply_to_address; do
    required_parameter "${value_name}"
  done
fi


# Shared V3/API credentials (identical for Prod and Staging — see addendum).
ai_intake_provider="$(get_optional_parameter ai-intake-provider "" "OpenAi")"
ai_intake_api_key="$(get_optional_parameter ai-intake-api-key true)"
ai_intake_endpoint="$(get_optional_parameter ai-intake-responses-endpoint "" "https://api.openai.com/v1/responses")"
ai_intake_model="$(get_optional_parameter ai-intake-model "" "gpt-5.6-luna")"
google_places_api_key="$(get_optional_parameter google-places-api-key true)"
google_places_text_search_endpoint="$(get_optional_parameter google-places-text-search-endpoint "" "https://places.googleapis.com/v1/places:searchText")"
google_places_nearby_search_endpoint="$(get_optional_parameter google-places-nearby-search-endpoint "" "https://places.googleapis.com/v1/places:searchNearby")"
google_places_geocoding_endpoint="$(get_optional_parameter google-places-geocoding-endpoint "" "https://maps.googleapis.com/maps/api/geocode/json")"

# V3 toggles: Prod reads its own SSM flags; Staging has independent toggles (default OFF, cost guardrail).
features_seap_enabled="$(get_optional_parameter features-seap-ingestion-enabled "" "false")"
features_auto_bidding_enabled="$(get_optional_parameter features-auto-bidding-enabled "" "false")"
staging_features_seap_enabled="$(get_optional_parameter staging-features-seap-ingestion-enabled "" "false")"
staging_features_auto_bidding_enabled="$(get_optional_parameter staging-features-auto-bidding-enabled "" "false")"

seap_enabled="$(get_optional_parameter seap-enabled "" "false")"
seap_base_url="$(get_optional_parameter seap-base-url "" "https://e-licitatie.ro")"
seap_notices_path="$(get_optional_parameter seap-notices-path "" "api-pub/NoticeCommon/GetCNoticeList/")"
seap_notice_type_id="$(get_optional_parameter seap-notice-type-id "" "2")"
seap_notices_page_size="$(get_optional_parameter seap-notices-page-size "" "20")"
seap_polling_interval_seconds="$(get_optional_parameter seap-polling-interval-seconds "" "3600")"
seap_target_cpv_prefix="$(get_optional_parameter seap-target-cpv-prefix)"
seap_max_notices_per_poll="$(get_optional_parameter seap-max-notices-per-poll "" "20")"
seap_max_new_notices_per_hour="$(get_optional_parameter seap-max-new-notices-per-hour "" "5")"
seap_request_timeout_seconds="$(get_optional_parameter seap-request-timeout-seconds "" "60")"
seap_storage_enabled="$(get_optional_parameter seap-storage-enabled "" "false")"
seap_storage_bucket="$(get_optional_parameter seap-storage-bucket)"
seap_storage_region="$(get_optional_parameter seap-storage-region "" "eu-central-1")"

auto_bidding_shadow_mode="$(get_optional_parameter auto-bidding-is-shadow-mode "" "true")"
auto_bidding_model="$(get_optional_parameter auto-bidding-model "" "claude-3-5-sonnet")"
auto_bidding_api_key="$(get_optional_parameter auto-bidding-api-key true)"
auto_bidding_api_url="$(get_optional_parameter auto-bidding-api-url "" "https://api.anthropic.com/v1/messages")"
auto_bidding_max_request_tokens="$(get_optional_parameter auto-bidding-max-request-tokens "" "4000")"
auto_bidding_top_chunks="$(get_optional_parameter auto-bidding-top-chunks "" "10")"
auto_bidding_embedding_model="$(get_optional_parameter auto-bidding-embedding-model "" "text-embedding-3-small")"
auto_bidding_embedding_endpoint="$(get_optional_parameter auto-bidding-embedding-endpoint "" "https://api.openai.com/v1/embeddings")"
auto_bidding_embedding_api_key="$(get_optional_parameter auto-bidding-embedding-api-key true)"
auto_bidding_documents_bucket="$(get_optional_parameter auto-bidding-documents-bucket)"
auto_bidding_documents_region="$(get_optional_parameter auto-bidding-documents-region "" "eu-central-1")"

stripe_enabled="$(get_optional_parameter stripe-enabled "" "false")"
stripe_api_key="$(get_optional_parameter stripe-api-key true)"
stripe_webhook_secret="$(get_optional_parameter stripe-webhook-secret true)"

document_intelligence_enabled="$(get_optional_parameter document-intelligence-enabled "" "false")"
document_intelligence_endpoint="$(get_optional_parameter document-intelligence-endpoint)"
document_intelligence_api_key="$(get_optional_parameter document-intelligence-api-key true)"

s3_service_url="$(get_optional_parameter s3-service-url)"
s3_access_key_id="$(get_optional_parameter s3-access-key-id)"
s3_secret_access_key="$(get_optional_parameter s3-secret-access-key true)"
s3_region="$(get_optional_parameter s3-region "" "eu-central-1")"
s3_root_prefix_prod="$(get_optional_parameter s3-root-prefix-prod "" "prod")"
s3_root_prefix_staging="$(get_optional_parameter s3-root-prefix-staging "" "staging")"

email_username="$(get_optional_parameter email-username true)"
email_password="$(get_optional_parameter email-password true)"

# Staging origin: the AWS-provided Lightsail URL (static IP + :8081). Staging is NOT public.
instance_ip="$(aws lightsail get-instance --instance-name "${instance_name}" --query instance.publicIpAddress --output text)"
staging_api_origin_host="${instance_ip}:8081"
staging_frontend_origin="http://${instance_ip}:8081"
# Legacy CloudFront API origin hostname — accepted by Prod during the cutover window (see deploy/Caddyfile).
legacy_api_origin="${instance_ip//./-}.sslip.io"


umask 077

{
  printf 'API_IMAGE=%s\n' "${api_image}"
  printf 'POSTGRES_USER=%s\n' "${postgres_user}"
  printf 'POSTGRES_PASSWORD=%s\n' "${postgres_password}"
  printf 'ACME_EMAIL=%s\n' "${acme_email}"
  printf 'PROD_SITE=%s\n' "${api_origin_host}"
} > "${output_dir}/.env"

emit_backend_env() {
  local output="$1"
  local database="$2"
  local root_prefix="$3"
  local api_host="$4"
  local frontend_origin_value="$5"
  local allowed_hosts="$6"
  local seap_flag="$7"
  local auto_bidding_flag="$8"

  {
    printf 'API_ORIGIN_HOST=%s\n' "${api_host}"
    printf 'Frontend__Origin=%s\n' "${frontend_origin_value}"
    printf 'PasswordReset__ResetPageUrl=%s/reset-password\n' "${frontend_origin_value%/}"
    printf 'EmailVerification__ConfirmationPageUrl=%s/verify-email\n' "${frontend_origin_value%/}"
    printf 'Stripe__SuccessUrl=%s/seap/tenders\n' "${frontend_origin_value%/}"
    printf 'Stripe__CancelUrl=%s/seap/tenders\n' "${frontend_origin_value%/}"
    printf 'AllowedHosts=%s\n' "${allowed_hosts}"
    printf 'ConnectionStrings__MarketplaceDatabase=Host=postgres;Port=5432;Database=%s;Username=%s;Password=%s\n' \
      "${database}" "${postgres_user}" "${postgres_password}"
    printf 'DataProtection__KeysPath=/var/lib/marketplace/data-protection-keys\n'
    printf 'Jwt__Issuer=%s\n' "${jwt_issuer}"
    printf 'Jwt__Audience=%s\n' "${jwt_audience}"
    printf 'Jwt__SigningKey=%s\n' "${jwt_signing_key}"
    printf 'AiIntake__Enabled=true\n'
    printf 'AiIntake__Provider=%s\n' "${ai_intake_provider}"
    printf 'AiIntake__ApiKey=%s\n' "${ai_intake_api_key}"
    printf 'AiIntake__Endpoint=%s\n' "${ai_intake_endpoint}"
    printf 'AiIntake__Model=%s\n' "${ai_intake_model}"
    printf 'GooglePlaces__Enabled=true\n'
    printf 'GooglePlaces__ApiKey=%s\n' "${google_places_api_key}"
    printf 'GooglePlaces__TextSearchEndpoint=%s\n' "${google_places_text_search_endpoint}"
    printf 'GooglePlaces__NearbySearchEndpoint=%s\n' "${google_places_nearby_search_endpoint}"
    printf 'GooglePlaces__GeocodingEndpoint=%s\n' "${google_places_geocoding_endpoint}"
    printf 'Email__Enabled=%s\n' "${email_enabled}"
    printf 'Email__Host=%s\n' "${email_host}"
    printf 'Email__Port=%s\n' "${email_port}"
    printf 'Email__UseTls=%s\n' "${email_use_tls}"
    printf 'Email__Username=%s\n' "${email_username}"
    printf 'Email__Password=%s\n' "${email_password}"
    printf 'Email__FromAddress=%s\n' "${email_from_address}"
    printf 'Email__FromName=%s\n' "${email_from_name}"
    printf 'Email__ReplyToAddress=%s\n' "${email_reply_to_address}"
    printf 'Features__SeapIngestionEnabled=%s\n' "${seap_flag}"
    printf 'Features__AutoBiddingEnabled=%s\n' "${auto_bidding_flag}"
    printf 'Seap__Enabled=%s\n' "${seap_enabled}"
    printf 'Seap__BaseUrl=%s\n' "${seap_base_url}"
    printf 'Seap__NoticesPath=%s\n' "${seap_notices_path}"
    printf 'Seap__NoticeTypeIds__0=%s\n' "${seap_notice_type_id}"
    printf 'Seap__NoticesPageSize=%s\n' "${seap_notices_page_size}"
    printf 'Seap__PollingIntervalSeconds=%s\n' "${seap_polling_interval_seconds}"
    printf 'Seap__TargetCpvPrefixes__0=%s\n' "${seap_target_cpv_prefix}"
    printf 'Seap__MaxNoticesPerPoll=%s\n' "${seap_max_notices_per_poll}"
    printf 'Seap__MaxNewNoticesPerHour=%s\n' "${seap_max_new_notices_per_hour}"
    printf 'Seap__RequestTimeoutSeconds=%s\n' "${seap_request_timeout_seconds}"
    printf 'Seap__Storage__Enabled=%s\n' "${seap_storage_enabled}"
    printf 'Seap__Storage__Bucket=%s\n' "${seap_storage_bucket}"
    printf 'Seap__Storage__Region=%s\n' "${seap_storage_region}"
    printf 'AutoBidding__IsShadowMode=%s\n' "${auto_bidding_shadow_mode}"
    printf 'AutoBidding__Model=%s\n' "${auto_bidding_model}"
    printf 'AutoBidding__ApiKey=%s\n' "${auto_bidding_api_key}"
    printf 'AutoBidding__ApiUrl=%s\n' "${auto_bidding_api_url}"
    printf 'AutoBidding__MaxRequestTokens=%s\n' "${auto_bidding_max_request_tokens}"
    printf 'AutoBidding__TopChunks=%s\n' "${auto_bidding_top_chunks}"
    printf 'AutoBidding__EmbeddingModel=%s\n' "${auto_bidding_embedding_model}"
    printf 'AutoBidding__EmbeddingEndpoint=%s\n' "${auto_bidding_embedding_endpoint}"
    printf 'AutoBidding__EmbeddingApiKey=%s\n' "${auto_bidding_embedding_api_key}"
    printf 'AutoBidding__DocumentsBucket=%s\n' "${auto_bidding_documents_bucket}"
    printf 'AutoBidding__DocumentsRegion=%s\n' "${auto_bidding_documents_region}"
    printf 'Stripe__Enabled=%s\n' "${stripe_enabled}"
    printf 'Stripe__ApiKey=%s\n' "${stripe_api_key}"
    printf 'Stripe__WebhookSecret=%s\n' "${stripe_webhook_secret}"
    printf 'DocumentIntelligence__Enabled=%s\n' "${document_intelligence_enabled}"
    printf 'DocumentIntelligence__Endpoint=%s\n' "${document_intelligence_endpoint}"
    printf 'DocumentIntelligence__ApiKey=%s\n' "${document_intelligence_api_key}"
    printf 'S3__ServiceUrl=%s\n' "${s3_service_url}"
    printf 'S3__AccessKeyId=%s\n' "${s3_access_key_id}"
    printf 'S3__SecretAccessKey=%s\n' "${s3_secret_access_key}"
    printf 'S3__Region=%s\n' "${s3_region}"
    printf 'S3__RootPrefix=%s\n' "${root_prefix}"
    printf 'ReverseProxy__KnownProxies__0=172.28.0.2\n'
  } > "${output}"
}

emit_backend_env "${output_dir}/.env.prod" \
  "${postgres_database}" "${s3_root_prefix_prod}" \
  "${api_origin_host}" "${frontend_origin}" "${api_origin_host};www.${api_origin_host};${legacy_api_origin}" \
  "${features_seap_enabled}" "${features_auto_bidding_enabled}"

emit_backend_env "${output_dir}/.env.staging" \
  "compaspot_staging" "${s3_root_prefix_staging}" \
  "${staging_api_origin_host}" "${staging_frontend_origin}" "${instance_ip};127.0.0.1" \
  "${staging_features_seap_enabled}" "${staging_features_auto_bidding_enabled}"

chmod 600 "${output_dir}/.env" "${output_dir}/.env.prod" "${output_dir}/.env.staging"
echo "Rendered environment files in ${output_dir}: .env, .env.prod, .env.staging (staging origin ${staging_frontend_origin})."
