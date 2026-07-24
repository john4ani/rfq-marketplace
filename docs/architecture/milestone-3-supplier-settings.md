# Milestone 3 — Categories and Supplier Delivery Settings

Status: Implemented, pending review

## Decisions and assumptions

- Product and UX copy remain Romanian; engineering documentation remains English.
- Categories are hierarchical and seeded with the initial construction-material groups.
- Full category administration is intentionally deferred to the administration milestone. A Supplier may add a missing leaf category manually; it becomes immediately available in the shared MVP category list.
- Suppliers select leaf categories only. Parent categories organize the hierarchy.
- Delivery coverage uses Romanian counties plus normal and maximum straight-line radii.
- Distance calculation, geocoding, maps, and supplier-to-RFQ matching belong to the supplier-discovery milestone.
- A supplier must save its company profile before saving category or delivery settings.
- Nationwide delivery replaces county selection; stored county rows are cleared when it is enabled.
- Minimum order value is optional and stored in Romanian lei for the initial market.
- The public landing page is called “Piața Live”. It lists completed Buyer and active Supplier profile names without exposing email addresses or contact details.
- A lightweight authenticated heartbeat marks the whole user identity online. Both profiles appear online when a dual-profile user is connected.
- Presence expires after two minutes without a heartbeat. Angular sends a heartbeat every 30 seconds and the landing page refreshes every 15 seconds.
- SignalR, WebSockets, Redis, and a separate presence service are intentionally unnecessary for this MVP.

## API

| Method | Route | Access |
|---|---|---|
| GET | `/api/v1/categories` | Authenticated user |
| POST | `/api/v1/categories` | Supplier; add or reuse a missing leaf category |
| GET/PUT | `/api/v1/suppliers/{supplierId}/categories` | Owning Supplier |
| GET/PUT | `/api/v1/suppliers/{supplierId}/delivery-settings` | Owning Supplier |
| GET | `/api/v1/piata-live` | Public |
| POST/DELETE | `/api/v1/piata-live/presence` | Authenticated user |

## UX

Supplier settings appear below the company profile. Categories are presented as selectable cards. Delivery coverage explains the difference between normal and maximum radius, supports an optional minimum order value, and offers either nationwide delivery or explicit county selection.

Validation failures explain the field that needs attention. If the supplier profile does not exist, the UI asks the user to save the company profile first.
