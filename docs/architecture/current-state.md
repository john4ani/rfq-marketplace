# Current implementation state

## Architecture

The repository is a .NET 10 modular monolith with an Angular 21 SPA and PostgreSQL 17. The dependency direction is healthy:

```text
Angular SPA → ASP.NET Core controllers → application contracts
                                      → infrastructure services → EF Core/PostgreSQL
                     domain aggregates ← infrastructure services
```

- `Marketplace.Domain` owns RFQ, offer, profile, notification, messaging, supplier-setting, and presence rules.
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
| ProcurementNeed | `Rfq` after the need is structured | Partial conceptual coverage |
| RequirementLine | `RfqItem` | Strong initial coverage |
| ProjectContext | `ProjectName` and description fields | Partial |
| RFQ / Request | `Rfq` aggregate and Buyer endpoints | Complete existing workflow |
| Publication | `Rfq.Publish` status transition | Implemented without recipient records |
| Matching | `RfqService.GetOpenAsync` + `ProviderCoveragePolicy` | Implemented, reasons not returned |
| Capabilities | Supplier categories, delivery settings, multi-category flag | Implemented at category level |
| Offer / OfferLine | `Offer`, `OfferItem`, total/itemized pricing | Implemented with noted gaps |
| Offer comparison | Buyer comparison page ordered by total | Implemented, coverage explanation partial |
| Notifications | In-app notification records and polling | Implemented for offers/messages/reopening/delivery |
| Outcome | selection and delivery lifecycle only | Partial |
| Trust / verification | pending Supplier approval field, profile completeness | Partial and not operationalized |
| Invitation / referral / discovery | none | Missing |
| Audit / analytics | correlation logs only | Missing as product data |
| AI intake | none | Missing by design until stabilization |
| Administration | none | Missing |

## Existing request-to-offer flow

1. A Buyer completes a Buyer profile.
2. The Buyer creates a draft `Rfq` at `/rfqs/new`, with optional lines, delivery data, deadline, category, split preference, and transport requirement.
3. The Buyer explicitly publishes the draft.
4. A registered Provider appears eligible when the request is open, categories overlap according to coverage rules, and county/nationwide delivery matches.
5. A full-coverage Provider may send a whole-request total or itemized offer. A specialist receives only matching lines when splitting is allowed and no full-coverage Provider exists.
6. Offer submission changes the request to `ReceivingOffers` and creates an in-app Buyer notification.
7. The Buyer compares submitted/revised offers and selects one. Competing submitted offers become not selected.
8. If transport is included, Provider delivery reporting and Buyer confirmation complete the delivery lifecycle.

## Persistence and migrations

EF Core owns 14 ordered migrations from the initial foundation through delivery confirmation. The preserved local database is current as of this audit. Important constraints include unique normalized email, one offer per Provider/request, unique line numbers, unique offer lines, positive quantities, non-negative prices, and unique notification deduplication keys.

No row-version or other optimistic-concurrency token currently protects RFQ decision transitions.

## Frontend state and UX

- Piața Live is the home/discovery page.
- `/rfqs/new` is the canonical request editor.
- `/open-requests` and `/open-requests/:id` are Provider discovery and response pages.
- `/rfqs/:id/offers` is Buyer request management and offer comparison.
- `/dashboard` contains Buyer requests, Provider offers, and profile/settings entry points.
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
