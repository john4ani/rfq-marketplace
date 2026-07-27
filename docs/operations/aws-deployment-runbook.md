# AWS deployment runbook

## Safety rules

- Run Terraform with the intended AWS account and `eu-central-1` selected; verify identity before every apply.
- Review every plan. Never apply a saved plan produced from a different commit or account.
- Do not place SMTP credentials, JWT keys, database passwords, `.tfvars`, plans, or rendered `.env` files in Git.
- Provision staging first. Production is allowed only after the Milestone 8 live acceptance gate passes.

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

Choose a globally unique state bucket name. From `infra/bootstrap`:

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

## 3. Configure staging inputs

Copy:

- `infra/environments/staging/backend.hcl.example` to `backend.hcl`;
- `infra/environments/staging/terraform.tfvars.example` to `terraform.tfvars`.

Set:

- the bootstrap bucket name in `backend.hcl`;
- `admin_cidr` to the administrator's current public IPv4 `/32`;
- `acme_email` to an owned operations email;
- `smtp_from_address` to an email that can complete SES verification.

Staging does not require a domain. It uses the generated CloudFront hostname and `<static-ip>.sslip.io` for the Caddy origin.

## 4. Plan and apply staging

```powershell
Set-Location infra/environments/staging
terraform init -backend-config=backend.hcl
terraform validate
terraform plan -out staging.tfplan
terraform show staging.tfplan
terraform apply staging.tfplan
terraform output -json deployment
```

Confirm the email sent by SNS. Confirm the SES sender identity separately. An SES sandbox account can send only to verified recipients until AWS approves production access.

## 5. Store deployment secrets

After Terraform creates the environment-specific SMTP user, generate region-specific SES SMTP credentials and store them directly in SSM:

```powershell
.\scripts\Initialize-SesSmtpCredentials.ps1 `
  -EnvironmentName staging `
  -IamUserName (terraform -chdir=infra/environments/staging output -json deployment |
    ConvertFrom-Json |
    Select-Object -ExpandProperty ses_smtp_user_name)
```

The helper creates at most one new IAM access key, derives the region-specific SMTP password, generates strong PostgreSQL and JWT secrets, and stores all four values as SSM `SecureString` parameters. It prints no credentials and deletes the new access key if secret storage fails.

## 6. Configure GitHub environment

Create a GitHub environment named `staging`. Add environment variables from Terraform output and the AWS account:

| Variable | Value |
|---|---|
| `AWS_REGION` | `eu-central-1` |
| `AWS_ACCOUNT_ID` | expected 12-digit AWS account |
| `AWS_DEPLOY_ROLE_ARN` | `deployment.github_deploy_role_arn` |

Create `production` separately and require manual approval. Do not share environment-specific role ARNs.
After production is deployed and its first manual backup succeeds, create the repository-level variable `PRODUCTION_BACKUP_ENABLED=true` to enable the daily production schedule. Manual staging and production backup runs do not depend on that flag.

## 7. Deploy

Commit and push a fully verified SHA to `main`, then manually run the `Deploy` workflow for `staging`.

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

## Production

Repeat the environment process under `infra/environments/production` with an owned Route 53 zone and domain. Review the full Terraform plan, require GitHub environment approval, and execute the production acceptance and backup checks during the agreed window.
