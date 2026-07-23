# ADR-0001: Use a Modular Monolith

Status: Proposed  
Date: 2026-07-23

## Context

The MVP must validate whether buyers publish structured RFQs, suppliers respond, and buyers accept offers. The product has tightly coupled transactional flows, an unvalidated workload, a small initial operational footprint, and a low-cost AWS target.

## Decision

Build one ASP.NET Core deployable containing explicit business modules and use one PostgreSQL database. Module boundaries are enforced through namespaces, application use cases, ownership rules, and tests.

## Alternatives considered

- Microservices: rejected because deployment, network failure handling, observability, versioning, and distributed consistency add cost before independent scaling is needed.
- Unstructured monolith: rejected because it obscures ownership and makes future change harder.
- Serverless primary backend: rejected by requirements and poorly aligned with the initial container/PostgreSQL deployment.

## Consequences

- Simple local development, deployment, transactions, and debugging.
- Offer acceptance can use one database transaction.
- Modules cannot be deployed or scaled independently.
- Discipline and tests are required to prevent accidental coupling.

## Security and operational implications

One process has access to the full application database, so least-privilege isolation exists at the application boundary rather than separate database credentials per service.

## Revisit when

Revisit only when measured scaling, release ownership, fault isolation, or regulatory needs cannot be met within the modular monolith. Extraction must follow an existing module boundary and include an explicit distributed-consistency design.
