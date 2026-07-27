# ADR-0016: Use immutable releases, GitHub OIDC, and SSM secrets

Status: Accepted
Date: 2026-07-27

## Context

The MVP needs repeatable AWS deployments without long-lived AWS access keys, secrets in source control, startup-time database migration, or an unrecoverable in-place overwrite.

## Decision

- GitHub Actions assumes an environment-specific, least-privilege AWS role through GitHub OIDC.
- Terraform writes non-secret runtime values to Systems Manager Parameter Store. An operator writes secrets as `SecureString` parameters with the provided PowerShell helper.
- Each backend deployment uploads an immutable release directory and an exact Docker image archive identified by the Git commit SHA.
- The deployment takes and exports a database backup, runs the EF migration bundle once, performs readiness checks, and only then makes the release current.
- A failed application rollout returns the `current` symlink and containers to the previous release. Database migrations must remain backward-compatible because an automatic destructive schema rollback is unsafe.
- Frontend files are synchronized to a private S3 bucket; immutable assets receive long caching and `index.html` receives no-cache headers. CloudFront is invalidated after publication.
- Production uses a protected GitHub environment with manual approval.

## Consequences

Deployments are auditable and reproducible without static AWS credentials. Rollback is fast for application failures, while schema rollback remains an explicit recovery decision. Initial setup requires Terraform bootstrap, environment variables, verified email configuration, and GitHub environment protection.

## Security and operational implications

The deployment role is scoped to the environment resources and trusts the exact environment-qualified GitHub OIDC subject. This repository's subject contains GitHub's immutable owner and repository IDs rather than only the display names. SSH is normally restricted to the administrator CIDR and temporarily replaced with the GitHub runner `/32` only during a job; the restriction is restored in an always-running cleanup step. Secrets do not appear in Terraform state, release archives, or logs.

## Revisit when

Move image delivery to ECR and adopt blue/green or managed database deployment when scale, release frequency, or recovery objectives justify the added cost and complexity.
