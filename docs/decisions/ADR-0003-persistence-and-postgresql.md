# ADR-0003: Use PostgreSQL, EF Core, and Direct Persistence

Status: Proposed  
Date: 2026-07-23

## Context

The marketplace needs relational integrity, transactions, filtering, audit history, and monetary precision.

## Decision

Use PostgreSQL with EF Core migrations. Application use cases access a concrete persistence boundary and purpose-built queries. Do not create a generic repository. Use specialized repositories only when an aggregate requires meaningful persistence behavior.

## Alternatives considered

- Generic repository/unit of work: rejected because EF Core already provides these capabilities and generic interfaces hide useful query semantics.
- Document database: rejected because the model is relational and transaction-heavy.
- Managed RDS: deferred to keep the initial deployment inexpensive.

## Consequences

EF Core capabilities remain available and queries stay explicit. Application tests need a real PostgreSQL instance for provider-specific behavior.

## Security and operational implications

Parameterized EF queries reduce injection risk. PostgreSQL is not publicly exposed. Same-host deployment requires off-instance backups and restore testing.

## Revisit when

Consider RDS when availability, automated recovery, storage growth, or operational load justifies the cost.
