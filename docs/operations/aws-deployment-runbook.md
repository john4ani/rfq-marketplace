# AWS deployment runbook

## Safety rules

- Run Terraform with the intended AWS account and `eu-central-1` selected; verify identity before every apply.
- Review every plan. Never apply a saved plan produced from a different commit or account.
- Do not place SMTP credentials, JWT keys, database passwords, `.tfvars`, plans, or rendered `.env` files in Git.

> **Environment naming (2026-08-23):** the current single live environment is **PRODUCTION**. It is managed
> by the legacy Terraform root `infra/environments/staging/` (the Lightsail host and buckets are physically
> named `rfq-marketplace-staging-*`; renaming them would destroy the host and its PostgreSQL data). The
> GitHub deploy/backup workflows now target `production`; before the first `production` deploy, apply the
> Terraform role-trust change and run `scripts/migrate-env-staging-to-production.ps1`. A separate Staging
> environment will be added later. See [environments.md](environments.md).

The current single live environment is **production** and has passed the Milestone 8 acceptance gate.

## 1. Verify prerequisites

Required tools: AWS CLI v2, Terraform, Git, Docker, .NET 10, Node/pnpm, and access to `john4ani/rfq-marketplace`.

```powershell
aws sts get-caller-identity
aws configure get region
terraform version
docker version
```

The expected AWS account must be confirmed before continuing. The deployment defaults to `eu-central-1`; a different local CLI default is harmless because Terraform and workflows set the region explicitly.

## 2. Bootstrap remote state and GitHub OIDC once

Choose a globally unique state bucket name. Set `budget_alert_email` to the account owner/operations email and `monthly_budget_limit_usd` to the approved account-wide monthly guardrail. From `infra/bootstrap`:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
terraform init
terraform plan -out bootstrap.tfplan
terraform apply bootstrap.tfplan
terraform output
```

After the first apply, copy `backend.hcl.example` to the ignored `backend.hcl`, set the real bucket name, and migrate the bootstrap state:

```powershell
terraform init -migrate-state -backend-config=backend.hcl
terraform plan
```

Do not destroy this root casually. Its own state now lives under `bootstrap/terraform.tfstate`. The state bucket uses `prevent_destroy`, versioning, encryption, public-access blocking, and lifecycle retention.

The bootstrap root also owns the single account-wide AWS cost budget. For the current production baseline it is `$20/month`, with direct email alerts at 80% actual spend, 100% forecasted spend, and 100% actual spend. Budget notifications are advisory rather than a hard service shutdown, and AWS billing data can lag usage.

## 3. Configure production inputs

Copy:

- `infra/environments/staging/backend.hcl.example` to `backend.hcl`;
- `infra/environments/staging/terraform.tfvars.example` to `terraform.tfvars`.

Set:

- the bootstrap bucket name in `backend.hcl`;
- `admin_cidr` to the administrator's current public IPv4 `/32`;
- `acme_email` to an owned operations email;
- `smtp_from_address` to an email that can complete SES verification.

The current production environment uses the `compaspot.com` domain and its ACM certificate for CloudFront. A future domainless Staging would use the generated CloudFront hostname and `<static-ip>.sslip.io` for the Caddy origin.

## 4. Plan and apply production (legacy root `infra/environments/staging`)

```powershell
Set-Location infra/environments/staging
terraform init -backend-config=backend.hcl
terraform validate
terraform plan -out production.tfplan
terraform show production.tfplan
terraform apply production.tfplan
terraform output -json deployment
```

Confirm the email sent by SNS. Confirm the SES sender identity separately. An SES sandbox account can send only to verified recipients until AWS approves production access.

## 5. Store deployment secrets

After Terraform creates the environment-specific SMTP user, generate region-specific SES SMTP credentials and store them directly in SSM:

```powershell
.\scripts\Initialize-SesSmtpCredentials.ps1 `
  -EnvironmentName production `
  -IamUserName (terraform -chdir=infra/environments/staging output -json deployment |
    ConvertFrom-Json |
    Select-Object -ExpandProperty ses_smtp_user_name)
```

> Before the first `production` deploy, migrate the existing parameters from `/rfq-marketplace/staging/*`
> to `/rfq-marketplace/production/*` with `scripts/migrate-env-staging-to-production.ps1`, and apply the
> Terraform role-trust change (`github_environment = "production"`).

The helper creates at most one new IAM access key, derives the region-specific SMTP password, generates strong PostgreSQL and JWT secrets, and stores all four values as SSM `SecureString` parameters. It prints no credentials and deletes the new access key if secret storage fails.

## 6. Configure GitHub environment

Create a GitHub environment named `production`. Add environment variables from Terraform output and the AWS account:

| Variable | Value |
|---|---|
| `AWS_REGION` | `eu-central-1` |
| `AWS_ACCOUNT_ID` | expected 12-digit AWS account |
| `AWS_DEPLOY_ROLE_ARN` | `deployment.github_deploy_role_arn` |

The `production` GitHub environment is the current single live environment; add any required manual-approval protection rules. Do not share environment-specific role ARNs.
After production is deployed and its first manual backup succeeds, create the repository-level variable `PRODUCTION_BACKUP_ENABLED=true` to enable the daily production schedule. Manual staging and production backup runs do not depend on that flag.

## 7. Deploy

Commit and push a fully verified SHA to `main`, then manually run the `Deploy` workflow for `production`.

The workflow:

1. assumes the environment role through OIDC;
2. builds the Angular application and exact API image;
3. temporarily opens SSH only to the current GitHub runner;
4. copies an off-instance pre-deploy backup when a database exists;
5. executes the migration bundle;
6. activates and health-checks the release;
7. publishes the frontend and invalidates CloudFront;
8. verifies frontend and readiness endpoints;
9. restores administrator-only SSH access.

## 8. Smoke test and inspect

Use the `frontend_origin` Terraform output. Exercise the live acceptance checklist in [Milestone 8](../milestones/milestone-8-aws-deployment.md) and record the SHA, time, tester, and result.

Also inspect:

- Lightsail ports: only 80/443 public and 22 for the administrator CIDR;
- S3 public-access blocks and CloudFront origin access;
- `/health/live` and `/health/ready`;
- SNS alarm subscription;
- CloudFront security headers and same-origin API requests;
- no secrets in workflow logs or release artifacts.

## Rollback

`host-deploy.sh` automatically restores the previous application release when deployment or readiness fails. It intentionally does not reverse a database migration. Schema changes must be backward-compatible with the previous application.

For a manual application rollback, SSH from the configured administrator CIDR, point `/opt/rfq-marketplace/current` to the prior release, and start `postgres api caddy` with that release's Compose file. Inspect compatibility before doing so.

Use the database restore runbook only for a confirmed data/schema recovery event.

## Staging (future)

When a separate Staging test environment is needed, provision a new environment with its own resources and
state key (for example a new `infra/environments/staging` root), then repeat the environment process using
the `staging` name and run the `Deploy` / `Database backup` workflows with the `staging` choice.

### Cloudflare or another external DNS provider

Cloudflare remains the DNS authority; the application remains behind the existing CloudFront distribution. Do not create a duplicate public host or expose the API directly.

1. Request an ACM public certificate in `us-east-1` for the canonical frontend domain and aliases. ACM certificates for CloudFront are free.
2. Add each ACM DNS-validation CNAME as DNS-only in the external DNS provider and wait for the certificate to become `ISSUED`.
3. Set `frontend_domain`, `frontend_aliases`, and `frontend_certificate_arn` in the environment's ignored `terraform.tfvars`. Leave `hosted_zone_id` unset. The certificate ARN must be in `us-east-1`.
4. Run `terraform plan`, inspect it, then apply. Terraform updates CloudFront aliases and the deployment health-check origin without attempting Route 53 records.
5. Add DNS-only CNAME records for the canonical domain and aliases pointing to the distribution hostname. Verify both `/` and `/health/ready` through the custom domain.

For the current staging site, `compaspot.com` is canonical, `www.compaspot.com` is an alias, and the API continues to use same-origin CloudFront routes.
