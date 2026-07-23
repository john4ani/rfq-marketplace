# ADR-0009: Use Feature-Local Angular State

Status: Proposed  
Date: 2026-07-23

## Context

The UI has several workflows but no demonstrated need for a global event store.

## Decision

Use standalone components, Reactive Forms, signals for local/derived feature state, and RxJS for HTTP and asynchronous composition. Do not introduce NgRx initially.

## Alternatives considered

- NgRx: deferred because boilerplate and indirection are not justified yet.
- Ad hoc shared mutable services: rejected because ownership becomes unclear.

## Consequences

Features remain understandable and independently testable. Shared server state must be deliberately cached or reloaded.

## Security and operational implications

Frontend state is never trusted for authorization or commercial totals. Sensitive access tokens remain in memory.

## Revisit when

Consider a formal store if cross-feature state coordination, offline behavior, or debugging complexity becomes demonstrably difficult.
