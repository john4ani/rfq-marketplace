# RFQ Marketplace Architecture

Status: Implemented application baseline; AWS deployment proposed
Scope: Milestones 0–8
Last updated: 2026-07-25

This directory is the design baseline for the industry-neutral RFQ Marketplace MVP. The initial geography is Romania and construction materials are the first category, while the platform model is intended to support additional markets and industries. The application, documentation, and development language is English.

Application functionality is implemented through Milestone 7. AWS infrastructure and deployment automation remain proposed for Milestone 8.

## Architecture principles

- Deploy a modular monolith: one API process and one PostgreSQL database.
- Keep business modules explicit without introducing distributed boundaries.
- Enforce authorization and commercial rules on the backend.
- Use direct, readable Entity Framework Core persistence; do not add generic repositories.
- Prefer deterministic rules and transparent calculations.
- Keep private data and competing offers isolated.
- Optimize the initial AWS deployment for low cost and operational simplicity.
- Record decisions and update affected diagrams when architecture changes.

## Document map

| Area | Document |
|---|---|
| C4 system context | [c4-context.md](c4-context.md) |
| C4 containers | [c4-container.md](c4-container.md) |
| C4 Angular frontend components | [c4-component-frontend.md](c4-component-frontend.md) |
| C4 ASP.NET Core backend components | [c4-component-backend.md](c4-component-backend.md) |
| Domain model and aggregates | [domain-model.md](domain-model.md) |
| Database model | [entity-relationship.md](entity-relationship.md) |
| Main interactions | [sequence-diagrams.md](sequence-diagrams.md) |
| Entity lifecycles | [state-diagrams.md](state-diagrams.md) |
| Runtime deployment | [deployment.md](deployment.md) |
| Terraform view | [terraform-infrastructure.md](terraform-infrastructure.md) |
| REST API | [api-overview.md](api-overview.md) |
| Proposed repository layout | [folder-structure.md](folder-structure.md) |
| Architecture decisions | [../decisions/README.md](../decisions/README.md) |

## Diagram policy

All architectural diagrams are Mermaid diagrams stored under `docs/architecture`. C4 views use standard Mermaid flowcharts with C4-style boundaries because this has broader renderer support than Mermaid's experimental C4 syntax.

Every design-affecting change must:

1. update the affected diagram and explanatory text;
2. update the API or data documentation when applicable;
3. add, amend, or supersede an ADR when the decision changes.

Accepted ADRs are not rewritten to hide prior decisions. A later ADR supersedes them.

## Shared assumptions

1. One identity may have Buyer and Provider profiles simultaneously; valid actions are not hidden merely because the other profile is active.
2. One supplier profile represents one company; one buyer profile represents one buyer account.
3. RON is the only enabled currency, but offers store the ISO currency code.
4. PostgreSQL `numeric` and C# `decimal` are used for money; floating-point types are prohibited.
5. All timestamps are UTC and all public entity identifiers are UUIDs.
6. Location matching uses coordinates and Haversine distance when possible, with county-based fallback.
7. A supplier can create at most one offer per RFQ and may revise it before the deadline.
8. Submitted offers explicitly respond to every requested line, including unavailable lines.
9. Messaging is one private conversation per offer and uses polling in the MVP.
10. PostgreSQL is hosted on the same Lightsail instance as the API for the initial deployment.
11. MVP notifications are persisted in PostgreSQL and delivered in-app. A durable email outbox remains proposed.
12. Attachments and their private S3 delivery remain post-MVP.

## Delivery gate

Implemented application behavior follows [the MVP acceptance checklist](../operations/mvp-acceptance-checklist.md). Documents for unimplemented AWS resources retain Proposed status until Milestone 8.
