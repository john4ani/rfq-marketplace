# ADR-0017: Evolve RFQ as the first ProcurementNeed implementation

Status: Accepted
Date: 2026-08-04

## Context

CompaSpot's product boundary is a procurement need, while the working application and its preserved data use `Rfq` and `RfqItem`. Introducing a second aggregate now would duplicate identity and lifecycle state, require a high-risk migration, and leave two competing sources of truth before a distinct pre-approval workflow exists.

## Decision

- Keep the current `Rfq` identifier, table, routes, and lifecycle as the persisted ProcurementNeed implementation for this slice.
- Persist the need's source, input type, requirement kind, optional outcome, and immutable audit events.
- Treat `RfqItem` as a requirement line that can describe either a product or a service.
- Expose `ProjectContext`, `DeliveryConstraints`, and `NeedStatus` as normalized domain/API views over the compatible existing fields and lifecycle. Do not persist duplicate status or delivery columns.
- Preserve old clients through defaults: omitted source is `Manual`, omitted input type is `StructuredForm`, and omitted line kind is `Product`.
- Backfill one `Migrated` audit event per existing request. All new lifecycle audit events are written in the same database transaction as the business change.
- Keep exact delivery addresses out of Supplier open-request responses.
- Introduce separate intake, publication, recipient, or invitation records only when an implemented workflow gives them independent behavior and lifecycle.

## Consequences

The existing Buyer-to-Provider circuit and all identifiers remain stable, while Step 3 can create assisted drafts with an explicit source and input type. The model becomes industry-neutral and supports services without a parallel request system. Derived views must remain covered by tests so their mapping cannot silently drift from the RFQ lifecycle.

## Revisit when

Split intake from RFQ publication when unapproved needs must be stored independently, when one need produces multiple publications, or when invitation/recipient delivery needs its own explainable lifecycle.
