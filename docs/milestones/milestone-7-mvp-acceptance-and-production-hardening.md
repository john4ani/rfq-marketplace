# Milestone 7 — MVP Acceptance and Production Hardening

Status: Complete

## Outcome

The implemented Buyer–Provider journey has a repeatable acceptance gate, production configuration fails safely when required values are missing, password recovery works in development and has an AWS SES-compatible production delivery path, and the API can operate safely behind a trusted reverse proxy.

AWS resource provisioning, application containers, CI/CD, backups, monitoring, staging, and production deployment belong to Milestone 8.

## Implemented scope

- Complete password-recovery UX from sign-in through reset confirmation.
- Enumeration-safe password-reset requests: the public response does not disclose whether an account exists.
- Reset links expire after 30 minutes, use a no-referrer browser policy, and are removed from the address bar after success.
- Development-only reset token handoff for local testing.
- SMTP password-reset delivery compatible with the AWS SES SMTP endpoint.
- Production startup validation for:
  - PostgreSQL connection configuration;
  - HTTPS frontend and reset-page URLs;
  - non-placeholder JWT signing key;
  - production `AllowedHosts`;
  - password-reset SMTP configuration;
  - explicit trusted Caddy proxy IP.
- Forwarded host, scheme, and client IP processing from explicitly trusted proxies only.
- Bounded PostgreSQL retries for transient connection failures.
- Authentication before endpoint rate limiting so authenticated partitions are available.
- Provider category creation limited to five attempts per ten minutes and recorded in structured audit logs.
- General requests use one all-inclusive total; legacy general offers that stored the total as delivery cost are normalized in the Provider and Buyer views, and new itemized offers without positions are rejected.
- Production routing decision: the Angular application keeps relative `/api/*` URLs and CloudFront routes those paths to the API origin.
- Milestone 4 formally accepted and architecture assumptions reconciled with the dual-role MVP.

## Production configuration contract

Production uses environment variables or AWS Parameter Store values. At minimum:

```text
ConnectionStrings__MarketplaceDatabase
Frontend__Origin
Jwt__SigningKey
AllowedHosts
PasswordReset__ResetPageUrl
PasswordReset__SmtpHost
PasswordReset__SmtpPort
PasswordReset__SmtpUsername
PasswordReset__SmtpPassword
PasswordReset__FromAddress
ReverseProxy__KnownProxies__0
```

The API deliberately refuses to start outside Development when this contract is incomplete or still contains known placeholder values.

## Category governance for the controlled MVP pilot

Providers may add a missing leaf category because a complete cross-industry catalogue does not yet exist. Duplicate and similarity confirmation remains the first safeguard. Milestone 7 adds rate limiting and structured creation logs.

An administrator moderation queue, category merge/rename tooling, and a public-launch abuse policy remain post-pilot work. Before public self-service registration is enabled, this decision must be revisited.

## Verification gate

The detailed, repeatable gate is in [the MVP acceptance checklist](../operations/mvp-acceptance-checklist.md).

Current automated verification:

- .NET build: passed with zero warnings.
- Domain tests: 18 passed.
- Application tests: 5 passed.
- Database-backed integration tests: 13 passed, including 2 production-configuration tests.
- Angular tests: 29 passed.
- Angular production build: passed with the existing component-style budget warnings.
- API Release publish: passed.
- Clean-database migration: passed; the resulting schema contains 23 application tables.
- Preserved-database migration: passed without deleting or recreating the database; no migration was pending.
- Idempotent deployment migration script: generated successfully and reviewed as a release artifact.
- Rendered offline and recovery UX: passed.
- Live Buyer, Provider, and dual-role browser journey: passed against the running API and preserved PostgreSQL database, including offer notification, comparison, selection, private two-way messaging, one-time reopening with a reason, final reselection, and Provider acceptance status.
- Delivery-included browser journey: passed from an explicit Buyer transport requirement and Provider commitment through Provider delivery reporting, Buyer receipt confirmation, notifications, and the final `Delivered` state.

The preserved database was backed up before the gate. Acceptance records remain available for manual confirmation; temporary integration-test categories were removed after verification.
