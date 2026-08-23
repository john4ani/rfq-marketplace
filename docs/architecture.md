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
- Admin web discovery is a bounded two-stage extension of Supplier Discovery: an explicit Need-context action, or an exact Admin **Firme** lookup after internal search is insufficient, performs limited external evidence gathering, then a tool-free strict-JSON qualification pass. `SupplierDiscoveryService` orchestrates `SupplierSearchIntent`, a shared `CategoryDiscoveryProfile`, geographic planning, independent Google Places/Web collectors, deterministic evidence merging and the qualifier. Google Places and OpenAI Web Search are independent sources; source warnings do not discard usable evidence from the other source. Geographic expansion is local → nearby → county only when the preceding tier is insufficient, and the merger persists that provenance. `SupplierWebDiscoveryRun` persists only provisional commercial evidence/results and telemetry; it does not bypass candidate review, opportunity approval or outreach policy. A company lookup never creates an `OpportunityMatch`.
- Outreach is an additive Supplier Discovery concern: `SupplierOutreachContact` records manual or email contact attempts, `SupplierContactSuppression` prevents later contact (including rediscovery), and email links are invitation- and opportunity-specific. SMTP is called only after the attempt/invitation is persisted, then delivery is recorded without holding the database transaction across the network call.

## AI contracts and observability

Keep separate application contracts: `IGeneralNeedInterpreter` and `IConstructionProjectInterpreter`. Each user turn should normally use one model call. Capture operation, model/provider, prompt version, duration, success/failure, token count, need/project ID, clarification count, fallback and user correction—never secrets or unnecessary request content.

`IGeneralNeedInterpreter` returns only a non-binding `SuggestedProcurementMode` (`ConstructionProjects` when appropriate). The shared Procurement Core remains in its current flow until the user makes an explicit choice; this keeps construction detection out of routing and persistence.

Before Task 9, the Construction interpreter is a routing/interpretation boundary only. Preserve the existing Construction project state and compatible request fields exactly as they are; do not add temporary generic fields to `Rfq` to store new Construction facts.

## Routing

Critical business states should be refreshable, back-safe and directly addressable where meaningful. Not every transient dialog needs a route. Buyer Home, My Requests, offer comparison, provider opportunity/request and claim continuity are critical paths.

## V3 — Auto-bidding (2026-08)

A new, configuration-gated auto-bidding module extends the modular monolith without touching the Procurement Core:

- **SEAP ingestion** (`Seap` config): a background worker polls the public e-licitatie.ro notice-search API (`POST /api-pub/NoticeCommon/GetCNoticeList/` with a JSON filter of `sysNoticeTypeIds`) and imports them as a distinct `SeapTender` aggregate. SEAP tenders are public opportunities, not CompaSpot buyer needs — the buying institution is external and never becomes an ApplicationUser, so tenders deliberately do not reuse `Rfq`. Attachments download to S3 (`SeapTenderAttachment`). Known limitation (2026-08): the anonymous public API serves a stale, capped dataset (2019 records, `total: 3000`); "recent" tenders require an authenticated session or an alternative source.
- **Company Vault** (`CompanyVault`/`VaultDocument`/`DocumentChunk`): PDF uploads, optional Azure Document Intelligence OCR, overlapping chunking, optional OpenAI embeddings stored in a `vector(1536)` column (pgvector). The RAG query orders `DocumentChunks` by cosine distance (`<=>`).
- **Bidding engine** (`Bidding`): embeds tender requirements → top-N chunk retrieval → Anthropic Claude (`ILlmProvider`, mocked in tests) → strict JSON parsing → `.docx`/`.xlsx` generation (`IDocumentGenerator`) → S3.
- **Stripe checkout** (`Stripe` config): dynamic `PriceData` from the estimated-value tiers (29/49/99 EUR, pure `PricingCalculator`), webhook verifies `Stripe-Signature` and triggers generation, `bid-status` is polled.
- **Shadow Mode** (`AutoBidding:IsShadowMode`): generates silently, skips Stripe, exposes nothing to end users — the switch for safe real-data rehearsal before go-live.
- **Standalone product surface**: the SEAP experience (**Anunțuri SEAP**) is a permanent Main Menu item for authenticated users — never context-gated; its routes (`/seap/tenders`, `/seap/tenders/{id}`) also keep the company rail for continuity.

Every external dependency (SEAP API, S3, OCR, embeddings, Claude, Stripe) is behind an interface and disabled by default in `appsettings.json`; the platform starts and works without any of the V3 credentials. Integration tests now use a Testcontainers `pgvector/pgvector:pg17` instance instead of the local development database; the local compose database image was updated to `pgvector/pgvector:pg17` to match.

### Safety switches and cost guardrails (V3)

- **Feature flags** (`Features` section, hot-reloadable): `SeapIngestionEnabled` and `AutoBiddingEnabled` are master switches, both `false` at deploy. They are read through `IOptionsMonitor`, so a config change takes effect within about a minute **without a redeploy**; the base MVP application never depends on them.
- **LLM/S3 cost guardrails**: `max_tokens` is hard-capped at 4000 inside `AnthropicClaudeProvider` regardless of configuration; the SEAP scraper imports at most `Seap:MaxNewNoticesPerHour` (default 5) new tenders per rolling hour; Claude and Azure Document Intelligence each run a consecutive-failure circuit breaker (3 failures → open for one hour, calls fail fast while open).
- **Staging ingestion** (`SeapRawIngestion`): raw notices land first in a staging table (`Pending`), and are promoted to the public `SeapTenders` table only after parsing succeeds (`Parsed`); failures keep the payload and `ErrorMessage` (`Failed`). Live users never see half-processed announcements.
- **Non-breaking migrations**: every V3 migration is additive (new tables, new nullable columns); no existing column is dropped or renamed in the same step.
