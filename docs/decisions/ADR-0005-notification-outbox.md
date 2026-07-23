# ADR-0005: Use a PostgreSQL-Backed Notification Outbox

Status: Proposed  
Date: 2026-07-23

## Context

Email must not block HTTP requests, and notification intent must not be lost when a business transaction succeeds.

## Decision

Insert notification rows in the same PostgreSQL transaction as business changes. A hosted background worker in the API process claims rows, renders separate templates, sends through SES, and records retries.

## Alternatives considered

- Send email in the request: rejected because latency and provider failure affect business commands.
- RabbitMQ/Kafka: rejected as unnecessary infrastructure.
- In-memory queue: rejected because restarts lose work.

## Consequences

Delivery is at-least-once and handlers must be idempotent. Worker health and dead-letter rows require monitoring.

## Security and operational implications

Payloads contain only necessary template data. Sensitive data is not logged. Claiming uses database concurrency controls to avoid simultaneous processing.

## Revisit when

Introduce an external broker only when measured throughput, independent worker scaling, or additional consumers cannot be handled reliably by PostgreSQL.
