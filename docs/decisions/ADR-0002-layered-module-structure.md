# ADR-0002: Use Layered Projects with Feature Modules

Status: Proposed  
Date: 2026-07-23

## Context

The codebase needs recognizable domain boundaries without producing a project for every feature or adopting a framework-heavy architecture.

## Decision

Use four production backend projects: Api, Application, Domain, and Infrastructure. Within Application and Domain, organize code by feature module. Dependencies point inward, and Api is the composition root.

## Alternatives considered

- One project: rejected because technical and domain concerns would blur quickly.
- One project per module and layer: deferred because it creates many assemblies before team size justifies them.
- Generic clean-architecture templates: rejected where they introduce generic repositories, mediator pipelines, or speculative abstractions.

## Consequences

The structure is familiar, testable, and compact. Compile-time isolation between modules is weaker than separate assemblies, so architecture tests and review conventions are required.

## Security and operational implications

Authorization remains centralized at API/application boundaries while resource policies live near their use cases.

## Revisit when

Split a module into a separate assembly only when ownership, build boundaries, or dependency enforcement provides demonstrated value.
