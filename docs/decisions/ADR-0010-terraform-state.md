# ADR-0010: Use Separate Terraform Roots and S3 State

Status: Proposed  
Date: 2026-07-23

## Context

Development and production require clear isolation, safe collaboration, encrypted state, and current locking without unnecessary services.

## Decision

Create a separately applied bootstrap configuration for a private, encrypted, versioned S3 state bucket. Use separate `dev` and `prod` Terraform roots and keys, with native S3 lockfile locking.

## Alternatives considered

- Local state: acceptable only during initial bootstrap, not persistent environments.
- Terraform workspaces: rejected because separate roots make environment differences and state boundaries more explicit.
- DynamoDB locking: rejected because S3 native lockfiles are available and DynamoDB locking is deprecated.

## Consequences

Bootstrap has its own lifecycle and recovery instructions. Backend configuration is supplied partially at initialization rather than hardcoded with credentials.

## Security and operational implications

State access is least-privilege; state, plan files, secret tfvars, and `.terraform/` are never committed. CI uses OIDC temporary credentials.

## Revisit when

Consider a managed Terraform platform if governance, policy enforcement, audit, or team concurrency requirements increase.
