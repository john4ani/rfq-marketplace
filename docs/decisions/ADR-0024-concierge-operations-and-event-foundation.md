# ADR-0024: Concierge operations and event foundation

## Status

Accepted — 2026-08-07

## Context

The local pilot needs a humane way to help participants when automatic matching, commercial details, or AI intake do not complete the task. It also needs measurable evidence of that work without building a warehouse, CRM, or hidden administrator controls.

## Decision

Pasul 9 adds an append-only `ProductEvent` record with a small event name, non-PII subject, optional actor, bounded metadata, and timestamp. The MVP records request creation/publication/outcome, offer submission, concierge interventions, and AI provider failures.

The Administrator workspace is an explicit concierge tool. An Administrator may request a Buyer correction, ask a Supplier to clarify an offer, record a confirmed outcome, retry the existing secure invitation flow, review AI failures, or grant one active Supplier a reasoned manual match for one open request. A manual match is persisted, unique per request/Supplier pair, visible as such to operations, and grants that Supplier access to respond. It is not verification, endorsement, ranking, or a replacement for normal category/location matching.

Administrators do not silently alter Buyer requirements or Supplier commercial terms. A correction/normalization is a clear in-app request with a route back to the owner’s own editing experience. Outcome recording is for a result confirmed outside the application and remains auditable through the existing procurement history plus the product event.

## Consequences

- Pilot operations can solve real exceptions without bypassing consent or masking data changes.
- The dashboard gives a small operational view rather than a vanity analytics system.
- Data can later be exported or projected into a warehouse without changing product workflows.
- A dedicated support-case lifecycle, role separation, automated outreach, bulk matching, event streaming/outbox, and richer analytics remain deferred until actual pilot volume justifies them.
