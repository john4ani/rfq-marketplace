# Architecture Decision Records

Records retain their individual lifecycle status. Implemented decisions are Accepted. Milestone 8 infrastructure decisions are accepted because their implementation and local production-runtime validation are complete; the milestone record separately tracks whether AWS has actually been provisioned and verified.

| ADR | Decision |
|---|---|
| [ADR-0001](ADR-0001-modular-monolith.md) | Modular monolith |
| [ADR-0002](ADR-0002-layered-module-structure.md) | Layered projects with feature modules |
| [ADR-0003](ADR-0003-persistence-and-postgresql.md) | PostgreSQL, EF Core, and direct persistence |
| [ADR-0004](ADR-0004-authentication.md) | Identity, JWTs, and rotating refresh tokens |
| [ADR-0005](ADR-0005-notification-outbox.md) | Database-backed notification outbox |
| [ADR-0006](ADR-0006-messaging-polling.md) | Polling for MVP messaging |
| [ADR-0007](ADR-0007-private-attachments.md) | Private S3 attachments |
| [ADR-0008](ADR-0008-lightsail-deployment.md) | Lightsail and Docker Compose deployment |
| [ADR-0009](ADR-0009-angular-state.md) | Feature-local Angular state |
| [ADR-0010](ADR-0010-terraform-state.md) | Separate Terraform roots and S3 state |
| [ADR-0011](ADR-0011-provider-coverage-and-total-offers.md) | Provider coverage without a product catalogue (coverage flag superseded by ADR-0022) |
| [ADR-0012](ADR-0012-defer-ratings-and-reputation.md) | Defer Buyer and Provider ratings beyond the MVP |
| [ADR-0013](ADR-0013-post-mvp-code-quality-hardening.md) | Schedule code-quality hardening after the MVP |
| [ADR-0014](ADR-0014-mvp-production-hardening-boundary.md) | MVP production-hardening and deployment boundary |
| [ADR-0015](ADR-0015-delivery-confirmation-lifecycle.md) | Separate request selection from delivery confirmation |
| [ADR-0016](ADR-0016-aws-release-and-secret-delivery.md) | Immutable AWS releases, OIDC deployment, and SSM secrets |
| [ADR-0017](ADR-0017-evolve-rfq-as-procurement-need.md) | Evolve RFQ as the first ProcurementNeed implementation |
| [ADR-0018](ADR-0018-assisted-procurement-intake.md) | Assisted procurement intake as a bounded draft adapter |
| [ADR-0019](ADR-0019-explicit-offer-comparability.md) | Explicit offer completeness and commercial comparability |
| [ADR-0020](ADR-0020-procurement-outcome-feedback-loop.md) | Terminal procurement outcomes and just-in-time follow-up |
| [ADR-0021](ADR-0021-assisted-supplier-discovery.md) | Human-controlled Supplier discovery and request-linked claim |
| [ADR-0022](ADR-0022-central-category-and-need-grouping.md) | Central categories, need-category groups, and separate project templates |

## ADR lifecycle

- `Proposed`: awaiting review.
- `Accepted`: approved design baseline.
- `Deprecated`: retained for history but discouraged.
- `Superseded by ADR-xxxx`: replaced by a later decision.

Accepted records are not silently rewritten when the decision changes.
