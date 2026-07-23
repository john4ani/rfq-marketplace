# Milestone 2 — Buyer and Supplier Profiles

Status: Implemented, pending review

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
| GET | `/api/v1/buyers/{buyerId}` | Matching Buyer account |
| PUT | `/api/v1/buyers/{buyerId}` | Matching Buyer account; creates or updates |
| GET | `/api/v1/suppliers/{supplierId}` | Matching Supplier account |
| PUT | `/api/v1/suppliers/{supplierId}` | Matching Supplier account; creates or updates |

Requests for another account ID return `404`. A valid account with the wrong role receives `403`.

## Account switching

The dashboard presents two explicit choices:

- **Cumpărător** — “Hai să economisim bani”
- **Furnizor** — “Hai să facem bani”

Registration allows either or both profiles to be selected. The API returns only the roles owned by the current user, so users with one profile see one top button and users with both see two. Switching changes the active profile in the Angular application immediately. It does not sign the user out, request a password, call a switching endpoint, or replace the authentication token. Both authorized roles remain in the same token.
