# ADR-0014: Define the MVP Production-Hardening Boundary

Status: Accepted
Date: 2026-07-25

## Context

The marketplace workflow is implemented through private offer messaging, but an AWS launch also needs safe configuration, password recovery, reverse-proxy correctness, a repeatable acceptance gate, and a clear boundary between application hardening and infrastructure deployment.

The Angular client already uses relative `/api/*` URLs. Providers may create missing categories because the catalogue is intentionally cross-industry and incomplete during the controlled pilot.

## Decision

- Keep browser API calls same-origin. CloudFront will serve the Angular application and route `/api/*` and `/health/*` to the Caddy/API origin.
- Validate required non-development configuration at API startup and reject placeholders.
- Trust forwarded host, scheme, and client IP headers only from explicitly configured proxy IPs.
- Deliver password-reset links through configurable SMTP; AWS SES SMTP is the initial production transport.
- Preserve enumeration-safe reset responses and expose reset tokens only in Development.
- Allow Provider-created leaf categories during the controlled pilot, with duplicate/similarity confirmation, per-identity rate limiting, and structured creation logs.
- Require the automated, migration, browser-role, privacy, and operational checks in the MVP acceptance checklist before release approval.

## Consequences

CloudFront needs an API origin and ordered path behaviours in Milestone 8. Caddy uses a stable private container IP that is passed to the API as a trusted proxy.

Production cannot start until database, JWT, host, password-reset, and proxy configuration are supplied. This converts silent misconfiguration into an explicit deployment failure.

SMTP credentials are secrets and must be loaded through the deployment configuration, never committed. SES identity verification and resource provisioning remain Milestone 8.

Provider category creation is suitable only for a controlled pilot. Administrator moderation, merge/rename tooling, and abuse controls must be revisited before broad public self-service onboarding.

## Not included

Terraform, containers, CI/CD, backups, monitoring, staging, attachments, ratings, payments, and post-award fulfilment are not part of this decision.
