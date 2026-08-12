# Current implementation state

## Architecture

The repository is a .NET 10 modular monolith with an Angular 21 SPA and PostgreSQL 17. The dependency direction is healthy:

```text
Angular SPA → ASP.NET Core controllers → application contracts
                                      → infrastructure services → EF Core/PostgreSQL
                     domain aggregates ← infrastructure services
```

- `Marketplace.Domain` owns RFQ, offer, profile, notification, messaging, supplier-setting, presence, and Supplier-discovery rules.
- `Marketplace.Application` defines use-case contracts and the pure Provider coverage policy.
- `Marketplace.Infrastructure` implements Identity, EF Core persistence, matching, offers, notifications, messaging, profiles, and email delivery.
- `Marketplace.Api` is the HTTP composition root with JWT authorization, rate limiting, CORS, health checks, problem details, and correlation IDs.
- `marketplace-web` uses standalone Angular components, lazy routes, in-memory access tokens, a refresh cookie, and HTTP services.

Deployment is already implemented for AWS: CloudFront/S3 frontend, Lightsail with Caddy/API/PostgreSQL, Terraform, GitHub OIDC, backups, and cost alarms.

## Identity and authorization

ASP.NET Core Identity stores one user per normalized email. Buyer and Supplier are additive roles on the same identity. A user can add the missing role later, and the API issues replacement credentials. Resource ownership is enforced in services and controllers; sensitive missing or unauthorized resources commonly return `404`.

The current model does not yet separate organizations and memberships. `BuyerProfile` and `SupplierProfile` are one-to-one profile records keyed by the user ID. Company fields are embedded in those profiles.

## Current domain mapping

| CompaSpot concept | Current implementation | State |
|---|---|---|
| User | `ApplicationUser` + Identity roles | Complete for MVP identity |
| Organization / membership | Company fields embedded in profiles | Missing as independent concepts |
| ProcurementNeed | Backward-compatible `Rfq` evolution after the need is structured | Strong MVP coverage |
| RequirementLine | `RfqItem` | Strong initial coverage |
| ProjectContext | Optional normalized project name and description | Implemented |
| RFQ / Request | `Rfq` aggregate and Buyer endpoints | Complete existing workflow |
| Publication | `Rfq.Publish` status transition | Implemented without recipient records |
| Matching | `RfqService.GetOpenAsync` + `ProviderCoveragePolicy` | Implemented, reasons not returned |
| Capabilities | Multiple central categories and delivery settings | Implemented at category level; full coverage is derived from selected categories |
| Offer / OfferLine | `Offer`, `OfferItem`, explicit coverage, tax, alternatives and delivery terms | Implemented for MVP |
| Offer comparison | Position matrix, comparable totals and explained trade-offs | Implemented for MVP |
| Notifications | In-app records, polling and deduplicated outcome follow-up | Implemented for current workflows |
| Outcome | Eight terminal real-world outcomes plus automatic cancellation/delivery results | Implemented for MVP |
| Trust / verification | pending Supplier approval field, profile completeness | Partial and not operationalized |
| Invitation / referral / discovery | Human-reviewed candidates and request-linked claim invitations | Supplier discovery implemented; general Pasul 6 referrals remain deferred |
| Audit / analytics | Immutable procurement history with minimal outcome funnel events | Partial; general event platform remains Pasul 9 |
| AI intake | Resumable clarification, model extraction and human-approved draft | Implemented first slice |
| Administration | Supplier candidate review and request-linked invitation workspace | First bounded capability implemented; broader concierge administration remains Pasul 9 |

## Existing request-to-offer flow

1. A Buyer completes a Buyer profile.
2. The Buyer creates a draft `Rfq` at `/rfqs/new`, with optional lines, delivery data, deadline, category, split preference, and transport requirement.
3. The Buyer explicitly publishes the draft.
4. A registered Provider appears eligible when the request is open, its central categories provide full coverage or allowed partial coverage, and county/nationwide delivery matches.
5. A full-coverage Provider may send a whole-request total or itemized offer. A specialist receives only matching lines when splitting is allowed and no full-coverage Provider exists.
6. Offer submission changes the request to `ReceivingOffers` and creates an in-app Buyer notification.
7. The Buyer compares submitted/revised offers and selects one. Competing submitted offers become not selected.
8. If transport is included, Provider delivery reporting and Buyer confirmation complete the delivery lifecycle.
9. The Buyer records the real procurement outcome; expired open requests create one linked follow-up notification when the Buyer returns.

## Persistence and migrations

EF Core owns the ordered additive migrations from the initial foundation through Supplier discovery. Important constraints include unique normalized email, one offer per Provider/request, unique line numbers, unique offer lines, positive quantities, non-negative prices, unique notification deduplication keys, unique candidate company/county pairs, and unique invitation-token hashes.

PostgreSQL `xmin` optimistic concurrency protects RFQ decision transitions and returns a reload-oriented conflict response.

## Frontend state and UX

- Piața Live is the home/discovery page.
- `/rfqs/new` is the canonical request editor.
- `/open-requests` and `/open-requests/:id` are Provider discovery and response pages.
- `/rfqs/:id/offers` is Buyer request management and offer comparison.
- `/dashboard` contains Buyer requests, Provider offers, and profile/settings entry points.
- `/admin/supplier-discovery` is the Administrator workspace for candidate sourcing, human review, and one-request invitation creation.
- `/supplier-invitations/:token` is the public, privacy-limited invitation and Supplier claim journey.
- Loading, empty, common validation, unavailable-backend, and many permission states exist.
- Request lines collapse to summaries and invalid submissions focus/scroll to useful errors.

The visible product brand is still Piața Live/RFQ Marketplace rather than CompaSpot. Rebranding is a separate coherent change, not part of stabilization.

## Test baseline at audit

- .NET solution build: passed with 0 warnings and 0 errors.
- Domain tests: 18 passed.
- Application tests: 5 passed.
- Integration tests: 16 passed.
- Angular tests: 36 passed across 16 files.
- Angular production build: passed with four pre-existing component-style budget warnings.
- Database migrations: all 14 migrations applied to the preserved local database.

The largest critical coverage weakness is that the broad offer lifecycle test uses one dual-role identity as both Buyer and Provider. Separate-identity matching, response, notification, and Buyer comparison are not covered as one continuous vertical test.
