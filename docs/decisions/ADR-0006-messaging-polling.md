# ADR-0006: Use HTTP Polling for MVP Messaging

Status: Proposed  
Date: 2026-07-23

## Context

Buyer–supplier messages must be persistent and private, but real-time delivery is not necessary to validate the core business hypothesis.

## Decision

Use paginated/cursor-based REST reads with periodic polling only while a conversation view is active. Persist unread state and send asynchronous notifications.

## Alternatives considered

- SignalR/WebSockets: deferred because connection management and proxy configuration are unnecessary for the MVP.
- Manual refresh only: rejected because modest polling improves usability at low complexity.

## Consequences

Messages may appear after a short delay and polling creates predictable API traffic. The API contract should support later push updates without changing persistence.

## Security and operational implications

Every poll and post performs participant authorization. Competing suppliers cannot infer conversation existence.

## Revisit when

Adopt SignalR if measured usage shows polling load or latency materially harms the messaging workflow.
