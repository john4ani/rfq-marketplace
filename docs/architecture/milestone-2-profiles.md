# Milestone 2 — Buyer and Supplier Profiles

Status: Implemented

## Decisions and assumptions

- The first market and first product language are Romanian.
- One email and password identify one user. That user may have a Buyer profile, a Supplier profile, or both.
- A profile ID equals its one-to-one Identity account ID. This supports explicit routes without a hidden `/me` lookup.
- Every profile request compares the route ID with the authenticated JWT subject.
- Supplier approval starts as `Pending`; approval administration is a later milestone.
- Supplier categories, delivery radii, and delivery areas remain Milestone 3.
- Coordinates are optional; no geocoding provider is required yet.

## API

| Method | Route | Authorization |
|---|---|---|
| POST | `/api/v1/auth/profiles/{accountType}` | Authenticated account; adds Buyer or Supplier role |
| GET | `/api/v1/buyers/{buyerId}` | Matching Buyer account |
| PUT | `/api/v1/buyers/{buyerId}` | Matching Buyer account; creates or updates |
| GET | `/api/v1/suppliers/{supplierId}` | Matching Supplier account |
| PUT | `/api/v1/suppliers/{supplierId}` | Matching Supplier account; creates or updates |

Requests for another account ID return `404`. A valid account with the wrong role receives `403`.

## Account switching

The dashboard presents two explicit choices:

- **Cumpărător** — “Hai să economisim bani”
- **Furnizor** — “Hai să facem bani”

Registration allows either or both profiles to be selected. A single-role account can add the missing Buyer or Supplier role later from **Contul meu**; the API then rotates the refresh token and returns an access token containing both roles. The newly added role opens its own profile form so business details can be completed.

The API returns only the roles owned by the current user, so users with one profile see their active role plus a clear add-profile action, while users with both see two role buttons. Switching between roles already owned by the account remains local to Angular and does not replace the authentication token.

Starting a first request requires completed Buyer details because location and market radius are request defaults and matching inputs. If they are missing, the request entry point opens the Buyer profile form with an explanation and automatically resumes the request after the profile is saved.
