# REST API Overview

Status: Implemented through Pasul 9 concierge administration; attachments remain proposed

Base path: `/api/v1`  
Format: JSON  
Time: UTC ISO 8601  
Errors: RFC 7807 Problem Details

## Compatibility policy

`ProcurementNeed` is the product/domain term, while `/api/v1/rfqs` remains the compatible public API resource. Do not rename the route to `/procurement-needs` merely to match internal terminology. Retain existing RFQ routes and DTO fields; introduce only additive DTO fields where a concrete workflow needs them.

## Endpoint groups

| Area | Main endpoints | Access |
|---|---|---|
| Authentication | `/auth/register/*`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/profiles/*`, verification/reset | Public or authenticated self |
| Buyer profile | `GET/PUT /buyers/{buyerId}` | Owning Buyer |
| Supplier profile | `GET/PUT /suppliers/{supplierId}` | Owning Supplier |
| Central category catalog | `GET /categories`, `POST /categories` | Authenticated read; Administrator create with duplicate/similarity confirmation |
| Project templates | `GET/POST /project-templates` | Authenticated read; Administrator create |
| Piața Live | `GET /piata-live`, presence heartbeat | Public directory; authenticated presence |
| Buyer procurement needs/RFQs | `/rfqs`, `/rfqs/{id}`, history, publish, cancel, deadline | Owning buyer |
| Assisted intake | `/assisted-intakes`, resume, clarify, approve | Owning buyer |
| Supplier discovery | `/supplier/rfqs` | Approved active supplier |
| Offers | `/open-rfqs/{id}/offer`, `/offers/mine`, submit, revise, withdraw | Resource-dependent |
| Comparison | `/rfqs/{id}/offers`; coverage, missing lines, tax and commercial totals | Owning buyer only |
| Acceptance | `/rfqs/{rfqId}/offers/{offerId}/select` | Owning buyer only |
| Delivery confirmation | Provider report and Buyer confirmation routes | Selected Provider or owning Buyer |
| Messaging | `/offers/{id}/conversation`, messages and read state | Buyer and that offer's supplier |
| Attachments | upload request, complete, download URL, delete | Authorized parent participant |
| Supplier discovery administration | `/admin/supplier-candidates`, `/admin/discovery-opportunities`, `/admin/supplier-discovery/needs/{rfqId}`, `/admin/supplier-discovery/acquisition`, `/admin/supplier-discovery/outreach-opportunities`, `/admin/supplier-discovery/web-discovery/{runId}/qualify` and `/candidates/{index}/accept`, `/admin/companies/web-discovery` | Administrator |
| Company registry administration | `GET/PUT/DELETE /admin/companies`, including discovered-company detail editing and deletion | Administrator |
| Supplier invitation and claim | `/supplier-invitations/{token}` | Public view; Supplier claim |
| Concierge administration | `/admin/concierge`, supplier search, manual match, correction, normalization and outcome actions | Administrator |
| Broader administration | `/admin/users`, suppliers, categories, inspection, reports, metrics | Partially implemented; dedicated operations workspace is available |
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
| Report delivery | No | Selected Provider only | No |
| Confirm receipt | Owning buyer | No | No |
| Approve/suspend supplier | No | No | Yes |
| Add a missing pilot category | No | Yes, guarded | Proposed moderation |

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
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout
POST   /api/v1/auth/request-password-reset
POST   /api/v1/auth/reset-password
GET    /api/v1/auth/profiles
POST   /api/v1/auth/profiles/{accountType}

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
GET    /api/v1/project-templates
POST   /api/v1/project-templates
GET    /api/v1/piata-live?profileType=Buyer|Supplier
POST   /api/v1/piata-live/presence
DELETE /api/v1/piata-live/presence
GET    /api/v1/rfqs/mine
POST   /api/v1/rfqs
GET    /api/v1/rfqs/{rfqId}
GET    /api/v1/rfqs/{rfqId}/history
PUT    /api/v1/rfqs/{rfqId}
POST   /api/v1/rfqs/{rfqId}/publish
POST   /api/v1/rfqs/{rfqId}/cancel
POST   /api/v1/rfqs/{rfqId}/outcome
POST   /api/v1/rfqs/{rfqId}/extend-deadline

GET    /api/v1/assisted-intakes/availability
POST   /api/v1/assisted-intakes
GET    /api/v1/assisted-intakes/{sessionId}
POST   /api/v1/assisted-intakes/{sessionId}/clarifications
POST   /api/v1/assisted-intakes/{sessionId}/approve

GET    /api/v1/supplier/rfqs
GET    /api/v1/supplier/rfqs/{rfqId}
GET    /api/v1/open-rfqs/{rfqId}/offer
PUT    /api/v1/open-rfqs/{rfqId}/offer
POST   /api/v1/open-rfqs/{rfqId}/offer/submit
POST   /api/v1/offers/{offerId}/withdraw
GET    /api/v1/offers/mine

GET    /api/v1/rfqs/{rfqId}/offers
POST   /api/v1/rfqs/{rfqId}/offers/{offerId}/select
POST   /api/v1/open-rfqs/{rfqId}/delivery/report
POST   /api/v1/rfqs/{rfqId}/delivery/confirm

GET    /api/v1/offers/{offerId}/conversation
POST   /api/v1/offers/{offerId}/conversation/messages
POST   /api/v1/offers/{offerId}/conversation/read

POST   /api/v1/attachments/upload-requests
POST   /api/v1/attachments/{attachmentId}/complete
GET    /api/v1/attachments/{attachmentId}/download-url

GET    /api/v1/admin/supplier-candidates
POST   /api/v1/admin/supplier-candidates
PUT    /api/v1/admin/supplier-candidates/{candidateId}/review
GET    /api/v1/admin/discovery-opportunities
POST   /api/v1/admin/supplier-candidates/{candidateId}/invitations
GET    /api/v1/supplier-invitations/{token}
POST   /api/v1/supplier-invitations/{token}/claim

GET    /api/v1/admin/metrics                         # proposed
GET    /health/live
GET    /health/ready
```

RFQ material paste parsing is deliberately handled in Angular for immediate feedback. The backend still validates every parsed material when the complete draft is saved.

Assisted-intake sessions are persisted, owner-scoped and rate-limited. Each extraction records provider, model, prompt version, token usage and the clarification transcript. Direct OpenAI is the development/staging default; Azure OpenAI is selectable through configuration. The MVP does not call web search or return web citations. Approval is idempotent and creates an ordinary RFQ in `Draft`; publication remains a separate existing command. If the model is missing or unavailable, start/clarify returns `503` and the Angular client opens the established advanced form. No local pseudo-AI response is returned.

### ProcurementNeed compatibility

The `/rfqs` contract remains backward-compatible and adds `source`, `inputType`, `needStatus`, `projectContext`, `deliveryConstraints`, outcome fields, and a `kind` (`Product` or `Service`) on each requirement line. Older payloads default to manual structured input and product lines. `GET /rfqs/{rfqId}/history` is Buyer-owned and returns the ordered immutable lifecycle audit trail. Supplier open-request responses include the generic context and requirement kind but continue to omit the exact delivery address.

`POST /rfqs/{rfqId}/outcome` is Buyer-owned and terminal. It accepts one supported `outcome` plus an optional `reason`; `Other` requires a reason. A CompaSpot purchase requires a selected offer, a participant purchase requires at least one response, and cancellation remains the separate cancellation command. Successful recording closes the request and appends `OutcomeRecorded`. Cancellation and confirmed delivery also append this funnel event for a complete outcome series. When an open request passes its quotation deadline without an outcome, notification polling materializes one deduplicated `ProcurementOutcomeRequested` notification. This just-in-time approach needs no scheduler and does no work for offline Buyers until they return.

For authenticated users, Piața Live accepts the active profile type. The first-version locality filter shows the home city first and then other participants in the same county. `approximateRadiusKm` reports the saved Buyer market radius or Supplier delivery radius, but exact kilometre filtering is deferred until reliable coordinates are available. Anonymous visitors continue seeing the full public market.

The snapshot exposes `onlineUserCount`, `onlineBuyerCount`, and `onlineSupplierCount` separately from `participants`. The headline count is the number of distinct recently active Buyer/Supplier identities, including accounts whose public profiles are not complete. A dual-role account counts once in `onlineUserCount` and once in each applicable role count. `participants` remains the privacy-safe directory of completed public profiles and may be location-filtered.

Piața Live may display the global headline counts. The `/participants` directory instead derives its participant and online totals from the returned, location-filtered `participants` collection and deduplicates by account ID; it must not present a global count beside a narrower local result set.

### Supplier open-request discovery

```text
GET /api/v1/open-rfqs
GET /api/v1/open-rfqs/{rfqId}
```

These Supplier-only endpoints return active published RFQs matched by selected category and delivery county. Exact delivery addresses, coordinates, and private Buyer notes are not exposed.

### Assisted Supplier discovery

Candidate creation and review are restricted to the existing `Administrator` role. Every candidate starts as `Unverified` and records its source; only a human-reviewed `EligibleForInvitation` candidate may be associated with a compatible, open request. Invitation URLs contain a cryptographically random token, while persistence stores only its SHA-256 hash. Public reads expose locality and requirement summary, never the exact delivery address. Claim requires an authenticated Supplier with a completed Supplier profile, is idempotent for that Supplier, and records immutable discovery audit events. The application generates a link for deliberate manual sharing; it sends no email or bulk outreach automatically.

### V3 - Auto-bidding (SEAP ingestion, Company Vault, Stripe)

All V3 endpoints require authentication unless noted. Shadow Mode (AutoBidding:IsShadowMode) makes auto-bid generate silently without Stripe and without exposing results to end users.

```text
GET  /api/v1/features                        [AllowAnonymous — SPA feature flags]
GET  /api/v1/seap/tenders                    [also accepts cpvPrefixes[] and counties[] profile filters]
GET  /api/v1/seap/tenders/{tenderId}
GET  /api/v1/seap/tenders/{tenderId}/pricing  [fair-pricing preview: EUR value, price, free generations]
POST /api/v1/seap/tenders/{tenderId}/auto-bid
GET  /api/v1/seap/tenders/{tenderId}/bid-status
GET  /api/v1/seap/tender-match/availability   [SEAP Tender Match chat availability]
POST /api/v1/seap/tender-match                [natural language → CPV prefixes + counties, one clarification at a time]
PUT  /api/v1/seap/tender-match/profile        [persist confirmed filters on the represented company]
POST /api/v1/webhooks/stripe            [AllowAnonymous, signature-verified]
GET  /api/v1/companies/{companyId}/vault
POST /api/v1/companies/{companyId}/vault/documents   [multipart PDF]
```

SEAP tenders are a distinct aggregate (SeapTender); they are public opportunities, not CompaSpot buyer needs, and the buying institution is not an ApplicationUser. auto-bid prices dynamically with the **fair-pricing EUR tiers** (25/75/300/500/1000/2000 €, converted from the RON estimated value with `AutoBidding:RonToEurRate`) via a Stripe Checkout session with PriceData, **except the first generation per account which is free** (`ApplicationUser.FreeGenerationsRemaining`); bid-status is polled after checkout completes. The webhook verifies the Stripe-Signature header before triggering generation. Vault uploads accept a PDF; OCR (Azure Document Intelligence) and embeddings (OpenAI) run only when configured, and the document stays indexed-less otherwise. `GET /api/v1/features` exposes `marketplaceEnabled` (default false) so the SPA hides every marketplace surface except Anunțuri SEAP and Admin.
