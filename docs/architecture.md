# CompaSpot MVP v2 — Architecture

CompaSpot remains an ASP.NET Core + PostgreSQL + Angular modular monolith deployed on the existing AWS/Cloudflare topology. No microservice migration, Kubernetes, Redis, broker, or general framework adoption is implied by MVP v2.

## Dependency rule

```text
Domain → Application → Infrastructure / API / Angular UI
```

Domain does not depend on AWS, OpenAI, SMS/email implementation, Angular, EF Core implementation details, or transport concerns. Controllers/endpoints stay thin; application/domain services own use-case logic.

## Data and delivery rules

- Use explicit DTOs, boundary validation, centralized authorization, `ProblemDetails`, async/cancellation, `HttpClientFactory`, bounded timeout/retry, structured logs and correlation IDs.
- Reads use projections, `AsNoTracking`, pagination and indexes driven by real query patterns. Keep migrations additive, small and reversible.
- Important operations are idempotent where appropriate.
- Invitation delivery uses transactional persistence of Invitation + Outbox record, followed by a background worker. Retries must not lose or duplicate contact.
- Cache only stable reference data such as categories/templates/service-area lookups. Do not cache active offers, workflows, permissions, claims, notification preferences or user-specific AI responses.

## AI contracts and observability

Keep separate application contracts: `IGeneralNeedInterpreter` and `IConstructionProjectInterpreter`. Each user turn should normally use one model call. Capture operation, model/provider, prompt version, duration, success/failure, token count, need/project ID, clarification count, fallback and user correction—never secrets or unnecessary request content.

Before Task 9, the Construction interpreter is a routing/interpretation boundary only. Preserve the existing Construction project state and compatible request fields exactly as they are; do not add temporary generic fields to `Rfq` to store new Construction facts.

## Routing

Critical business states should be refreshable, back-safe and directly addressable where meaningful. Not every transient dialog needs a route. Buyer Home, My Requests, offer comparison, provider opportunity/request and claim continuity are critical paths.
