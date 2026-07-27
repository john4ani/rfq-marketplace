# ADR-0004: Use ASP.NET Core Identity, JWTs, and Rotating Refresh Tokens

Status: Accepted
Date: 2026-07-23

## Context

The SPA needs email/password registration, verification, recovery, roles, revocation, and backend ownership authorization.

## Decision

Use ASP.NET Core Identity for users and password security. Issue short-lived JWT access tokens and opaque rotating refresh tokens. Store access tokens in SPA memory and refresh tokens as hashes server-side, delivered through Secure HttpOnly cookies.

Password-reset requests return the same accepted response whether or not an account exists. Development may return the generated token for local testing. Non-development environments send a reset link through configured SMTP; AWS SES SMTP is the initial production transport.

Buyer and Provider are additive profiles on one identity, not mutually exclusive account types. An authenticated user may add the missing role without creating another email/password identity. The API issues a replacement access/refresh-token pair immediately so authorization reflects the added role; the role itself is retained even before its business profile details are completed.

## Alternatives considered

- Long-lived JWT only: rejected because revocation is weak.
- Browser local storage for refresh tokens: rejected because script access increases token-theft impact.
- Custom password system: rejected as unnecessary security risk.
- Social login: explicitly outside the MVP.

## Consequences

Refresh endpoints require careful cookie, CORS, CSRF, reuse-detection, and token-family handling. Already issued access tokens remain valid until their short expiry. Adding a role rotates the current refresh token and returns a new access token.

## Security and operational implications

Login and recovery endpoints are rate-limited; responses avoid user enumeration; tokens and passwords are never logged.

## Revisit when

Consider a managed identity provider only if operational, federation, compliance, or multi-application requirements outweigh migration cost.
