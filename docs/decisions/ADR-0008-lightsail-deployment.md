# ADR-0008: Deploy API and PostgreSQL with Docker Compose on Lightsail

Status: Accepted
Date: 2026-07-23

## Context

The MVP prioritizes low AWS cost and simple deployment over high availability.

## Decision

Run Caddy, the API, and PostgreSQL with Docker Compose on one Linux Lightsail instance. Expose only Caddy on 80/443 and restrict SSH to a configured administrator CIDR. Store PostgreSQL data in a named volume and back it up off-instance.

## Alternatives considered

- ECS/EKS/Kubernetes: prohibited and operationally excessive.
- RDS: deferred for cost.
- Separate database host: deferred until availability needs justify it.

## Consequences

Deployment and diagnosis are simple, but the host is a single point of failure. Capacity is vertically constrained.

## Security and operational implications

Containers use minimal privileges, database/API ports remain private, images are pinned, logs rotate, patches are applied, and restore procedures are tested.

## Revisit when

Move PostgreSQL to a managed service when recovery objectives, load, availability, or operations justify the added cost.
