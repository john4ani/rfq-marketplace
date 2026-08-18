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
- Company representation is resolved through active `CompanyMembership`, not a global active-company claim. This foundation is additive: `SupplierProfile` and its shared-key compatibility constraint remain unchanged until a concrete future requirement justifies a broader migration.
- A provider notification is never addressed to a company ID. The application resolves its active CompanyMembership users in the same write operation and creates one personal notification per distinct recipient. Conversations use the same membership check for company-side access; message authors remain User IDs.
- Supplier Discovery persists `OpportunityMatch` as an auditable, per-need relation to exactly one registered company or unregistered candidate. Registered provider matching remains authoritative; candidate relevance is deterministic from category compatibility and county, then requires a separate Administrator decision for that exact opportunity. The decision never sends contact automatically.
- Outreach is an additive Supplier Discovery concern: `SupplierOutreachContact` records manual or email contact attempts, `SupplierContactSuppression` prevents later contact (including rediscovery), and email links are invitation- and opportunity-specific. SMTP is called only after the attempt/invitation is persisted, then delivery is recorded without holding the database transaction across the network call.

## AI contracts and observability

Keep separate application contracts: `IGeneralNeedInterpreter` and `IConstructionProjectInterpreter`. Each user turn should normally use one model call. Capture operation, model/provider, prompt version, duration, success/failure, token count, need/project ID, clarification count, fallback and user correction—never secrets or unnecessary request content.

`IGeneralNeedInterpreter` returns only a non-binding `SuggestedProcurementMode` (`ConstructionProjects` when appropriate). The shared Procurement Core remains in its current flow until the user makes an explicit choice; this keeps construction detection out of routing and persistence.

Before Task 9, the Construction interpreter is a routing/interpretation boundary only. Preserve the existing Construction project state and compatible request fields exactly as they are; do not add temporary generic fields to `Rfq` to store new Construction facts.

## Routing

Critical business states should be refreshable, back-safe and directly addressable where meaningful. Not every transient dialog needs a route. Buyer Home, My Requests, offer comparison, provider opportunity/request and claim continuity are critical paths.
