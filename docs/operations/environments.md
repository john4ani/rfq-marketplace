# Environments (MVP)

## Decision (2026-08-23)

CompaSpot MVP runs a **single live environment, and that environment is PRODUCTION**. A separate **Staging**
(pre-production test) environment will be added later when it is needed.

## Current state (2026-08-23) — clean recreation complete

The environment was **fully recreated clean**: the legacy `rfq-marketplace-staging-*` resources (Lightsail
host, buckets, CloudFront, IAM, SSM) were destroyed and a fresh **`rfq-marketplace-production-*`** environment
was provisioned from `infra/environments/production` with state key `production/terraform.tfstate` and SSM path
`/rfq-marketplace/production`. The obsolete `infra/environments/staging` Terraform root was removed; a real
Staging environment will be created from scratch when it is needed.

Production facts (from `terraform output -json deployment`):

| Resource | Value |
|---|---|
| Lightsail instance | `rfq-marketplace-production` (static IP `3.64.174.227`) |
| Buckets | `rfq-marketplace-production-eu-central-1-{frontend,attachments,backups}` |
| CloudFront distribution | `E1GKUUITW7X8T8` → `https://d282vqj7an62u8.cloudfront.net` (custom domain pending DNS repoint) |
| Deploy role | `arn:aws:iam::513193776478:role/rfq-marketplace-production-github-deploy` |
| SSM path | `/rfq-marketplace/production` |
| SES SMTP user | `rfq-marketplace-production-ses-smtp` |

## Remaining steps before the first `production` deploy (user-executed)

1. **Secrets — DONE (2026-08-23)**: all parameters were copied from `/rfq-marketplace/staging/*` to
   `/rfq-marketplace/production/*` via `scripts/migrate-env-staging-to-production.ps1` (AI intake, Google
   Places, PostgreSQL password, JWT key). The old SES SMTP credentials were **not** copied (their IAM user was
   destroyed); fresh credentials for `rfq-marketplace-production-ses-smtp` were generated and stored as both
   `email-username`/`email-password` (what `render-environment.sh` reads) and `smtp-username`/`smtp-password`.
   The legacy `/rfq-marketplace/staging/*` path was left empty.
2. **GitHub**: create a `production` environment with `AWS_REGION=eu-central-1`, `AWS_ACCOUNT_ID=513193776478`
   and `AWS_DEPLOY_ROLE_ARN=arn:aws:iam::513193776478:role/rfq-marketplace-production-github-deploy`;
   remove the old `staging` environment.
3. **SES**: verify the new email identity (`john4ani@gmail.com`) and confirm the SNS subscription on the
   `rfq-marketplace-production-alerts` topic.
4. **DNS (Cloudflare)**: point `compaspot.com` and `www.compaspot.com` to `d282vqj7an62u8.cloudfront.net`,
   then re-enable the custom domain in `infra/environments/production/terraform.tfvars`
   (`frontend_domain = "compaspot.com"`, `frontend_aliases = ["www.compaspot.com"]`,
   `frontend_certificate_arn = "<the us-east-1 ACM ARN>"`) and re-apply Terraform.
5. **Deploy**: run the `Deploy` workflow with `production`.

## Why some names still say "staging"

The production environment was provisioned before this decision using the legacy "staging" identifiers.
The following AWS-side names are load-bearing — renaming them is a coordinated migration, not a text
replacement:

| What | Current value |
|---|---|
| Lightsail instance / S3 buckets / IAM role | `rfq-marketplace-staging-*` |
| SSM Parameter Store path | `/rfq-marketplace/staging/*` |
| GitHub Actions OIDC trust | `repo:…:environment:staging` |
| Terraform state key | `staging/terraform.tfstate` |
| Deploy/backup workflow choice | `staging` — this deploys the live production environment |

## Migrating the naming to "production" — status

The repository side is done (2026-08-23):

- `.github/workflows/deploy.yml` and `.github/workflows/backup.yml` now list `production` (default) first and reserve `staging` for the future test environment.
- `scripts/Set-DeploymentSecrets.ps1` and `scripts/Initialize-SesSmtpCredentials.ps1` accept `production` first (they write under `/rfq-marketplace/production/…`).
- Terraform: the environment module now trusts the GitHub `production` environment (`github_environment = "production"`) and the deploy role may read parameters under `/rfq-marketplace/*`; `infra/environments/staging/main.tf` documents that it manages production with legacy resource names.
- `scripts/migrate-env-staging-to-production.ps1` copies every SSM parameter from `/rfq-marketplace/staging/*` to `/rfq-marketplace/production/*` (idempotent).

The workflows read SSM parameters by environment name and the GitHub OIDC role must trust `environment:production`,
so before the first `production` deploy, run the AWS-side steps in this order:

1. **Terraform apply** in `infra/environments/staging`: updates the deploy role trust to accept the GitHub `production` environment and broadens SSM read access to `/rfq-marketplace/*` (no resource recreation).
2. **SSM migration**: run `scripts/migrate-env-staging-to-production.ps1` with AWS administrator credentials.
3. **Deploy**: run the `Deploy` workflow with `production` (default).

> This is a cutover: after step 1, the legacy `staging` deploy choice no longer authenticates (the role trusts
> only `environment:production`), so the migration should be completed in one session. Deploying as `staging`
> remains only for the future separate Staging environment.

Terraform root and state key still use the legacy `staging` name because renaming the Lightsail host/buckets in
place would be destructive (the host holds the PostgreSQL data volume); that rename is deferred to when a real
Staging environment is introduced.
