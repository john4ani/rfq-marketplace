# Milestone 8: AWS deployment

Status: AWS production deployed and verified
Last updated: 2026-08-23

## Outcome

Deliver a repeatable, low-cost AWS deployment for the accepted MVP without Kubernetes, long-lived CI credentials, public database/API container ports, or secrets in source control.

## Implemented scope

- Separate Terraform bootstrap, staging, and production roots with encrypted, versioned S3 state and native S3 lockfiles.
- Private S3/CloudFront Angular hosting with origin access control, security headers, SPA routing, and same-origin `/api/*` and `/health/*` behavior.
- Ubuntu 24.04 Lightsail host with static IP, restricted SSH, automatic security updates, Docker log rotation, and swap.
- Caddy, ASP.NET Core API, and PostgreSQL production Compose runtime with fixed internal addressing and minimal container privileges.
- Exact, immutable API image builds and an explicit EF migration bundle.
- Persisted ASP.NET Core Data Protection keys and PostgreSQL storage.
- GitHub Actions CI, Terraform validation, protected-environment deployment, pre-deploy backup, health verification, and application rollback.
- Daily and on-demand checksum-verified PostgreSQL backups copied to a private encrypted S3 bucket.
- SSM Parameter Store configuration and `SecureString` secret delivery.
- CPU and instance-status alarms through SNS.
- One account-wide `$20/month` AWS cost budget, with direct email alerts at 80% actual, 100% forecasted, and 100% actual spend.

## Verified locally

- Terraform formatting and validation for bootstrap, staging, and production roots.
- GitHub workflow syntax and shell scripts through `actionlint` and ShellCheck.
- Production Docker image build.
- Caddy configuration validation.
- Production Compose configuration.
- A new PostgreSQL database accepted all migrations; a second migration run was a no-op.
- The production API reached a healthy readiness state against the isolated database.
- Recreating the API preserved the same Data Protection key file and returned to healthy readiness.
- An isolated custom-format database backup restored all 23 public tables with an unchanged checksum, and the API returned to healthy readiness.

The full application regression suite must pass again before the implementation is committed. Live verification items below cannot be claimed until AWS resources exist.

## Live production acceptance

- [x] Bootstrap state and GitHub OIDC resources provisioned in `eu-central-1`.
- [x] Production Terraform plan reviewed and applied.
- [ ] SES sender and SNS subscription confirmed. SES is verified; the recreated SNS subscription awaits email confirmation.
- [x] GitHub `production` environment variables configured.
- [x] Deployment workflow completes from the committed `main` SHA.
- [x] CloudFront frontend and `/health/ready` respond over HTTPS.
- [ ] Authentication, dual-role navigation, request publication, offer submission, comparison, messaging, selection, reopen-once, and delivery confirmation smoke-tested.
- [ ] API-unavailable frontend behavior remains useful and recoverable.
- [ ] Backup copied to S3 and restore rehearsal completed. Two live backups are in S3; restore was rehearsed locally against the production runtime, not against live production.
- [x] Restricted SSH, private buckets, alarms, and CloudFront behavior inspected in AWS.

## Live production evidence

- Frontend: `https://d3gmo2aqxxbvqe.cloudfront.net`
- API origin: `https://63-185-35-216.sslip.io` (CloudFront origin, not the user-facing base URL)
- Successful deployment: [GitHub Actions run 30249552658](https://github.com/john4ani/rfq-marketplace/actions/runs/30249552658), commit `d192c319544072b7712966ede90da4ab4ea31db9`
- Successful standalone backup: [GitHub Actions run 30250372063](https://github.com/john4ani/rfq-marketplace/actions/runs/30250372063), commit `6f782d1f02feb1646e9dfe6952937e5ae89384a3`
- Browser smoke: landing page, direct `/sign-in` SPA route, synthetic dual-role registration, authentication, Buyer dashboard, Provider role switch, and Provider empty states passed without console warnings/errors.
- Runtime: API and PostgreSQL report healthy; neither publishes a host port. Caddy is the only public container.
- Security: SSH is restricted to `86.123.72.160/32`; Terraform reports no drift; CloudFront and all S3 buckets are deployed with the documented access controls.
- Cost governance: the pre-existing account budget is adopted into bootstrap Terraform and alerts `john4ani@gmail.com` before and at the approved monthly guardrail.
- Backups: the encrypted/versioned S3 backup bucket contains checksum-paired deployment and standalone-workflow backups.

## Production gate (historical)

The original production gate required a controlled domain/Route 53 hosted zone, protected GitHub environment approval, confirmed SES sending capability, a reviewed acceptance record, and an agreed maintenance/rollback window. This gate has been passed — the current single live environment **is production** (frontend on CloudFront, API origin on Lightsail). A separate Staging environment will be added later as a pre-production test environment.

## Production inputs and costs

Production uses the approved Lightsail bundle and usage-based supporting services; the domain (`compaspot.com`) and ACM certificate are configured for CloudFront.

The production host is Lightsail `small_3_0` in `eu-central-1` (2 GB RAM, 2 vCPU, 60 GB SSD). Its current listed fixed price must be rechecked before any change; CloudFront, S3, email, and data transfer are usage-based.

## Deferred beyond the MVP

- Multi-instance API or managed PostgreSQL high availability.
- KMS-backed Data Protection key encryption.
- ECR image registry and blue/green deployment.
- WAF, multi-region disaster recovery, and automated schema rollback.
- Private S3 attachment delivery remains post-MVP application work.
