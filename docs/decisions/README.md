# Architecture Decision Records

All records are **Proposed** during Milestone 0. They become **Accepted** only after architecture approval.

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

## ADR lifecycle

- `Proposed`: awaiting review.
- `Accepted`: approved design baseline.
- `Deprecated`: retained for history but discouraged.
- `Superseded by ADR-xxxx`: replaced by a later decision.

Accepted records are not silently rewritten when the decision changes.
