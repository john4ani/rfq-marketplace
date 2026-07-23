# Milestone 1 — Identity and Authentication

Status: Implemented, pending review

## Scope

- ASP.NET Core Identity with PostgreSQL.
- Buyer and supplier self-registration; administrator self-registration is prohibited.
- One email address and password identify one user. The user may own the Buyer role, Supplier role, or both. Identity uniqueness is the normalized email address.
- Password recovery token flow. Email verification is intentionally deferred for the MVP.
- Fifteen-minute JWT access tokens with Buyer, Supplier, or Administrator role claims.
- Opaque refresh tokens stored only as SHA-256 hashes and rotated on every use.
- Refresh-token reuse revokes the remaining token family.
- Secure HttpOnly, SameSite=Strict refresh-token cookie.
- Authentication endpoint rate limiting.
- Angular in-memory access token, HTTP interceptor, route guard, sign-in, and registration pages.

## MVP email assumption

Milestone 1 does not require email verification before login. This keeps registration easy to test while the marketplace hypothesis is still being validated. Login and password recovery require only the email and password because both profiles belong to the same identity. Password-reset requests return the generated token only in Development; other environments return `202 Accepted` without revealing whether the account exists. SES delivery and optional email verification can be added in a later notification/deployment milestone.

The first product language is Romanian and the first market is Romania. Our development documentation remains in English. UI text is kept outside domain rules so a formal localization mechanism can be introduced later.

## Security boundaries

- Passwords are handled only by ASP.NET Core Identity.
- Refresh tokens and JWTs must not be logged.
- Access tokens are held in Angular memory rather than local storage.
- The API remains authoritative for role and future ownership checks.
- Production supplies `Jwt__SigningKey` through external secret configuration.

## Main endpoints

| Method | Route | Result |
|---|---|---|
| POST | `/api/v1/auth/register/buyer` | Create a Buyer identity |
| POST | `/api/v1/auth/register/supplier` | Create a Supplier identity |
| POST | `/api/v1/auth/login` | Return access token and set refresh cookie |
| POST | `/api/v1/auth/refresh` | Rotate refresh token and return access token |
| POST | `/api/v1/auth/logout` | Revoke refresh token and delete cookie |
| POST | `/api/v1/auth/request-password-reset` | Start password recovery |
| POST | `/api/v1/auth/reset-password` | Set a new password |
| GET | `/api/v1/auth/me` | Return authenticated identity claims |
