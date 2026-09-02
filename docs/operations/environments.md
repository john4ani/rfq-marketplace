# Environments (MVP)

## Decision (2026-08-23, updated)

CompaSpot runs a **single cost-optimized host** (AWS Lightsail VPS) with **two environments that share one
PostgreSQL instance and identical API credentials**:

| Environment | URL | Public | Database | S3/R2 root prefix | Feature flags |
|---|---|---|---|---|---|
| **Prod** | `https://compaspot.com` (+ `www`) | ✅ | `compaspot_prod` | `prod/` | SSM `features-*` (default OFF) |
| **Staging** | `http://<lightsail-static-ip>:8081` | ❌ (admin CIDR only) | `compaspot_staging` | `staging/` | SSM `staging-features-*` (default OFF) |

Rationale (`docs/adr/` + the environment addendum): no duplicate paid API accounts for Staging — isolation is
**configuration/DB-only**: separate databases, separate S3/R2 object prefixes, separate feature toggles.

## Topology (single `deploy/docker-compose.yml`)

- **PostgreSQL 17 + pgvector** — one instance, two databases created idempotently by the `init-db` one-shot
  (`deploy/scripts/init-databases.sh`). The legacy `marketplace` database from the pre-dual era is preserved
  untouched in the same data volume (no data was deleted).
- **`backend-prod` / `backend-staging`** — two parallel API containers (`env_file: .env.prod` / `.env.staging`,
  rendered from AWS SSM `/rfq-marketplace/production/*` by `deploy/scripts/render-environment.sh`).
- **Caddy** — reverse proxy + automatic Let's Encrypt for `compaspot.com` (SPA static files + `/api/*` +
  `/health/*` → `backend-prod`), and plain HTTP `:8081` for Staging (SPA + `/api/*` → `backend-staging`).
  The Lightsail firewall opens `:8081` only to the admin CIDR (see `infra/modules/lightsail-host`).
- **Frontend** — the built Angular SPA is uploaded to `/opt/rfq-marketplace/shared/frontend-{prod,staging}`
  and served by Caddy. CloudFront/S3 for the frontend is no longer used.
- **Storage** — the .NET `IAmazonS3` client targets **Cloudflare R2** when `S3__ServiceUrl` is set
  (`S3Options`; path-style + static credentials). Environments are separated by `S3__RootPrefix`
  (`prod/` vs `staging/`) inside the same bucket. Keys: `prod/seap/...`, `prod/bids/...`, `prod/vault/...`,
  `staging/...` accordingly. When `S3__ServiceUrl` is empty, the default AWS IAM chain is used (backward
  compatible).
- **Backups** — `database-backup.sh` dumps **both** databases; the Deploy and Backup workflows copy them to
  the `backups-bucket` S3 bucket (kept). `database-restore.sh` restores a single named database.

## Secrets & SSM (path `/rfq-marketplace/production`)

Rendered by `render-environment.sh` (all values are Prod + Staging shared, except where noted):

- Required: `api-origin-host` (= `compaspot.com`), `frontend-origin`, `postgres-database`
  (= `compaspot_prod`), `postgres-user`, `postgres-password` (SecureString), `jwt-issuer`,
  `jwt-audience`, `jwt-signing-key` (SecureString), `acme-email`, `instance-name`, `email-*`,
  `admin-cidr`, `backups-bucket`.
- Optional (defaults when absent): `ai-intake-*`, `google-places-*`, `seap-*`, `auto-bidding-*`,
  `stripe-*`, `document-intelligence-*`, `s3-*` (ServiceUrl/AccessKeyId/SecretAccessKey/Region/prefixes),
  `email-username`/`email-password`.
- Feature toggles (separate per env, default OFF): `features-seap-ingestion-enabled`,
  `features-auto-bidding-enabled`, `staging-features-seap-ingestion-enabled`,
  `staging-features-auto-bidding-enabled`.

`render-environment.sh` derives the public URLs from the frontend origin (no SSM parameters needed):
`PasswordReset__ResetPageUrl = <frontend-origin>/reset-password`, `EmailVerification__ConfirmationPageUrl =
<frontend-origin>/verify-email`, and `Stripe__{Success,Cancel}Url = <frontend-origin>/seap/tenders`. Prod
`AllowedHosts` = `compaspot.com;www.compaspot.com;<ip>.sslip.io` (the sslip host is the legacy CloudFront
origin, kept during the cutover window). Staging `AllowedHosts` = `<static-ip>;127.0.0.1` — **hostnames only,
without ports**: ASP.NET Core's host filtering compares the request host *without* the port, so port-bearing
entries never match (the in-container healthcheck probes `127.0.0.1:8080` and external requests carry
`<static-ip>:8081`; both are covered by the bare hostnames).

The API startup validation (`ProductionReadiness`) requires HTTPS origins **only in Production**. Staging is an
internal-only plain-HTTP site, so its HTTP frontend origin and matching reset/verify URLs are accepted; all
completeness and placeholder checks still apply in Staging.

## Production facts (2026-08-23)

| Resource | Value |
|---|---|
| Lightsail instance | `rfq-marketplace-production` (static IP `3.64.174.227`) |
| Deploy role | `arn:aws:iam::513193776478:role/rfq-marketplace-production-github-deploy` |
| SSM path | `/rfq-marketplace/production` |
| SES SMTP user | `rfq-marketplace-production-ses-smtp` |
| Databases | `compaspot_prod`, `compaspot_staging` (shared pgvector:pg17 instance) |
| Staging URL | `http://3.64.174.227:8081` (admin CIDR only) |

## Deploy flow

1. Run the **Deploy** workflow (environment `production`). It renders `.env`/`.env.prod`/`.env.staging`,
   builds the Angular SPA + API image, uploads them to the host, runs `init-db` + `migrate-prod` +
   `migrate-staging`, brings up `postgres backend-prod backend-staging caddy` and verifies both health
   endpoints.
2. Prod is served directly by Caddy → DNS (Cloudflare) must point `compaspot.com` / `www.compaspot.com`
   to the Lightsail static IP `3.64.174.227` (A records; previously a CNAME to CloudFront).
3. Staging: `http://<static-ip>:8081` from the admin IP only.

## Post-deploy smoke tests on Staging (Playwright)

After every **Staging** deployment the workflow runs the Playwright smoke suite
(`src/marketplace-web/e2e/`, script `pnpm run test:e2e:staging`):

- **Golden path** — loads `STAGING_URL`, signs in with a test account, asserts no console errors.
- **Live SEAP search (Suceava)** — runs „Caută în SEAP" with the county filter and requires at least
  one tender result, validating real connectivity to e-licitatie.ro and PostgreSQL reads.

Setup required once:

- GitHub **repository secrets** `E2E_TEST_EMAIL` / `E2E_TEST_PASSWORD` — the Staging test account.
  The account must exist (register through the UI) and should have a registered company so the
  advanced county filter is available (otherwise the SEAP test falls back to an unfiltered search).
- Staging must be able to reach `https://e-licitatie.ro` from the API container.

See `src/marketplace-web/e2e/README.md` for local run instructions.

## Cutover / cleanup of the CloudFront era

After the new topology is verified live on `compaspot.com` via Caddy:

- [ ] Cloudflare DNS: `A` (not CNAME) → `3.64.174.227` for `compaspot.com` and `www`.
- [ ] `terraform apply` to drop the obsolete `module.frontend` (CloudFront distribution + frontend bucket)
      and `module.storage` attachments/frontend buckets (keep the backups bucket), and remove the
      `attachments-bucket` / `cloudfront-distribution-id` / `frontend-bucket` SSM params.
- [ ] Delete the obsolete `api` service references and old `compose.production.yaml` from older releases.

> The old `marketplace` database and the `d282vqj7an62u8.cloudfront.net` distribution remain intact until the
> cutover checklist above is executed; they are simply no longer referenced.

## Granting Administrator access

The `Administrator` Identity role gates the operational workspace. Assignment is an explicit, audited
database action (ADR-0021); the application never elevates a configured e-mail automatically. The account
must already exist — register it through the UI first if needed. The deploy pipeline uploads
`deploy/scripts/grant-admin.sh` to every release; run it on the host from a release directory:

```bash
cd /opt/rfq-marketplace/releases/<sha>
bash scripts/grant-admin.sh john4ani@gmail.com          # compaspot_prod
bash scripts/grant-admin.sh john4ani@gmail.com staging  # compaspot_staging
```

The script is idempotent (re-running is a no-op), matches the account by normalized e-mail, and exits
with a clear message when the account or the role is missing.

## Cloudflare R2 storage setup (optional, for V3 storage)
The .NET S3 client targets R2 when `S3__ServiceUrl` is set; until then it stays on the AWS IAM chain / OFF.
Create an R2 bucket (e.g. `compaspot-data`) and an API token (Object Read & Write for that bucket), then store
the credentials under the Prod SSM path (Staging reads the same values):

```powershell
$P = '/rfq-marketplace/production'
aws ssm put-parameter --name "$P/s3-service-url"          --type String      --value "https://<ACCOUNT_ID>.r2.cloudflarestorage.com"
aws ssm put-parameter --name "$P/s3-access-key-id"        --type String      --value "<R2_ACCESS_KEY_ID>"
aws ssm put-parameter --name "$P/s3-secret-access-key"    --type SecureString --value "<R2_SECRET_ACCESS_KEY>"
aws ssm put-parameter --name "$P/s3-region"               --type String      --value "auto"
aws ssm put-parameter --name "$P/seap-storage-bucket"     --type String      --value "compaspot-data"
aws ssm put-parameter --name "$P/seap-storage-enabled"    --type String      --value "false"
aws ssm put-parameter --name "$P/auto-bidding-documents-bucket" --type String --value "compaspot-data"
```

Object keys are automatically separated per environment: `prod/seap/...`, `prod/bids/...`, `prod/vault/...`
versus `staging/...` (see `S3Options`). Toggle `seap-storage-enabled`/`features-seap-ingestion-enabled` only
when the module is ready.
