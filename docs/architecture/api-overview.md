# REST API Overview

Status: Implemented through Milestone 4; later endpoint groups remain proposed

Base path: `/api/v1`  
Format: JSON  
Time: UTC ISO 8601  
Errors: RFC 7807 Problem Details

## Endpoint groups

| Area | Main endpoints | Access |
|---|---|---|
| Authentication | `/auth/register/*`, `/auth/login`, `/auth/refresh`, `/auth/logout`, verification/reset | Public or authenticated self |
| Buyer profile | `GET/PUT /buyers/{buyerId}` | Owning Buyer |
| Supplier profile | `GET/PUT /suppliers/{supplierId}` | Owning Supplier |
| Categories | `GET /categories` | Authenticated; public exposure may be allowed |
| Piața Live | `GET /piata-live`, presence heartbeat | Public directory; authenticated presence |
| Buyer RFQs | `/rfqs`, `/rfqs/{id}`, items, publish, cancel, close | Owning buyer |
| Supplier discovery | `/supplier/rfqs` | Approved active supplier |
| Offers | `/rfqs/{id}/offers`, `/offers/{id}`, submit, revise, withdraw | Resource-dependent |
| Comparison | `/rfqs/{id}/offer-comparison` | Owning buyer only |
| Acceptance | `/rfqs/{rfqId}/offers/{offerId}/accept` | Owning buyer only |
| Messaging | `/offers/{id}/conversation`, `/conversations/{id}/messages` | Buyer and that offer's supplier |
| Attachments | upload request, complete, download URL, delete | Authorized parent participant |
| Administration | `/admin/users`, suppliers, categories, inspection, reports, metrics | Administrator |
| Health | `/health/live`, `/health/ready` | Operational policy |

## Authorization matrix

| Resource/action | Buyer | Supplier | Administrator |
|---|---:|---:|---:|
| Edit own profile | Yes | No | Inspect only |
| Edit supplier profile | No | Own | Inspect/status only |
| Create/edit draft RFQ | Own | No | Inspect only |
| Discover published RFQ | Own view | Relevant approved suppliers | Inspect |
| Create/edit offer | No | Own and eligible | Inspect only |
| View all offers for RFQ | Owning buyer | Never | Inspect with audit |
| View one conversation | Owning buyer | Owning supplier | Moderation with audit |
| Accept offer | Owning buyer | No | No |
| Approve/suspend supplier | No | No | Yes |
| Manage categories | No | No | Yes |

Every resource action applies backend ownership checks. A role alone is insufficient.

## Conventions

- API models are separate from EF Core entities.
- Validation errors include field paths in Problem Details extensions.
- Lists return `items`, page/cursor metadata, and allowed sort keys.
- Filtering and sorting use explicit allowlists.
- Updates use an integer version represented through ETag/`If-Match` or a request field.
- Critical commands accept `Idempotency-Key`: RFQ publication, offer submission, acceptance, and upload initialization.
- Cancellation tokens propagate through request handling and database/AWS calls.
- Authentication, upload, and public endpoints have rate and request-size limits.
- Sensitive resources may return `404` instead of revealing that an unauthorized object exists.

## Representative routes

```text
POST   /api/v1/auth/register/buyer
POST   /api/v1/auth/register/supplier
POST   /api/v1/auth/register
POST   /api/v1/auth/verify-email
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout
GET    /api/v1/auth/profiles

GET    /api/v1/buyers/{buyerId}
PUT    /api/v1/buyers/{buyerId}
GET    /api/v1/suppliers/{supplierId}
PUT    /api/v1/suppliers/{supplierId}
GET    /api/v1/suppliers/{supplierId}/categories
PUT    /api/v1/suppliers/{supplierId}/categories
GET    /api/v1/suppliers/{supplierId}/delivery-settings
PUT    /api/v1/suppliers/{supplierId}/delivery-settings

GET    /api/v1/categories
POST   /api/v1/categories
GET    /api/v1/piata-live?profileType=Buyer|Supplier
POST   /api/v1/piata-live/presence
DELETE /api/v1/piata-live/presence
GET    /api/v1/rfqs/mine
POST   /api/v1/rfqs
GET    /api/v1/rfqs/{rfqId}
PUT    /api/v1/rfqs/{rfqId}
POST   /api/v1/rfqs/{rfqId}/publish
POST   /api/v1/rfqs/{rfqId}/cancel

GET    /api/v1/supplier/rfqs
GET    /api/v1/supplier/rfqs/{rfqId}
POST   /api/v1/rfqs/{rfqId}/offers
GET    /api/v1/offers/{offerId}
PUT    /api/v1/offers/{offerId}
POST   /api/v1/offers/{offerId}/submit
POST   /api/v1/offers/{offerId}/withdraw
GET    /api/v1/offers/{offerId}/revisions

GET    /api/v1/rfqs/{rfqId}/offer-comparison
POST   /api/v1/rfqs/{rfqId}/offers/{offerId}/accept

GET    /api/v1/offers/{offerId}/conversation
GET    /api/v1/conversations/{conversationId}/messages
POST   /api/v1/conversations/{conversationId}/messages
POST   /api/v1/conversations/{conversationId}/read

POST   /api/v1/attachments/upload-requests
POST   /api/v1/attachments/{attachmentId}/complete
GET    /api/v1/attachments/{attachmentId}/download-url

GET    /api/v1/admin/metrics
GET    /health/live
GET    /health/ready
```

RFQ material paste parsing is deliberately handled in Angular for immediate feedback. The backend still validates every parsed material when the complete draft is saved.

For authenticated users, Piața Live accepts the active profile type. The first-version locality filter shows the home city first and then other participants in the same county. `approximateRadiusKm` reports the saved Buyer market radius or Supplier delivery radius, but exact kilometre filtering is deferred until reliable coordinates are available. Anonymous visitors continue seeing the full public market.
