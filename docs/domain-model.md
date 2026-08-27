# CompaSpot MVP v2 — Domain model

## Bounded contexts

| Context | Responsibility |
|---|---|
| Identity / Organizations | Person or company identity; a participant can buy and sell. |
| Procurement | `ProcurementNeed`, requirement lines, publication, location and commercial preferences. |
| Supplier Network | Provider capability/categories and service area. |
| Supplier Discovery | Candidate, opportunity match, approval, invitation, claim and suppression. |
| Offers | Offers, offer lines, alternatives, comparison and outcome. |
| Trust | Verified facts and accountable behaviour. |
| Notifications | Preferences, suppression and reliable delivery. |
| AI | Separate General Need and Construction Project interpreters. |
| Construction | Project, project type/facts/templates/requirements and generated procurement needs. |

This is a modular monolith. These boundaries do not require separate `.csproj` projects or services.

## Supplier / Company semantic alias

`SupplierProfile` is a **legacy persistence name**. Semantically, it represents a selling **Company / ProviderOrganization**, including its capability, delivery and offer relationships. It is not the personal profile of the User who sells. `CompanyMembership` associates one or more Users with that company and records `Owner` or `Representative`; a revoked membership cannot represent the company. The legacy shared-key constraint remains until a later, concrete scenario justifies changing it.

Commercial conversations retain the `SupplierProfile` company ID; individual messages retain their authenticated User sender ID. Provider-company notifications are materialized for active CompanyMembership users, while buyer notifications remain addressed directly to the Buyer User. `AccountTrustRecord` remains account persistence: provider commercial activity is queried by Company ID and legacy company-verification records are read compatibly from the original owner-keyed record.

## Shared Procurement Core

`ProcurementNeed` is the user’s Cerere. The existing `Rfq` remains the compatible internal implementation until a concrete workflow requires a split.

```text
ProcurementNeed (current Rfq)
├── RequirementLines (current RfqItems)
├── location + delivery/service preferences
├── publication + lifecycle
├── supplier matching
├── offers + outcome
└── NeedCategory 1..1
```

It must support a car alternator, timber, transport, excavator hire, electrical materials, or a construction-generated need without construction-only assumptions. Each need has exactly one primary shared commercial `NeedCategory`, regardless of whether it is a direct purchase, list, or project. Requirement lines carry no category association in MVP; the need category is the only commercial routing category.

General Procurement is a generic entry mode into this Core, not a permanent domain Vertical. The current `Vertical`/`Category` model may be reused for compatibility. If a general placeholder Vertical is temporarily necessary, it must be explicitly provisional and must not carry domain rules or determine matching semantics.

The General interpreter may return a non-binding `SuggestedProcurementMode` when a description appears to be a construction project. It is a UI hint only: it never converts the need, redirects the user, or persists new Construction facts in `Rfq`.

`NeedType.Project` is a legacy/general shape for a project-like Procurement Need. It is not a `ConstructionProject` and must not be used to enter, select, or infer the Construction bounded context. Construction mode is explicit and owns the specialised project context before it generates Procurement Needs.

Until Task 9, the current compatible Construction state remains stored and handled exactly as it is. `IConstructionProjectInterpreter` may be introduced before the aggregate solely as an interpretation boundary. It must not create temporary generic `Rfq` fields, or place new Construction facts in the Procurement Core.

## Construction boundary

`ConstructionProject` owns construction-specific facts, terminology, assumptions, systems/work packages and safe decompositions. It may generate one or more `ProcurementNeed`s. Procurement does not know about roof pitch, airtightness, passive-house systems, or engineering rules.

### Construction alignment — current MVP state

The distinction is locked as: **General Procurement = breadth, Construction Projects = depth, Procurement Core = shared commercial infrastructure.** Construction is a distinct specialised entry mode, never a General Procurement category and never a separate marketplace.

As built today (no `ConstructionProject` aggregate exists yet):

- **Two distinct intake workflows, one shared Core.** General (`/rfqs/new`, `IGeneralNeedInterpreter`) and Construction (`/rfqs/new/construction`, `IConstructionProjectInterpreter`) are separate interpretation workflows — separate routes, controllers (`assisted-intakes` / `construction-intakes`), prompts, JSON schemas and clarification semantics. They are **not** one universal workflow with a swapped prompt. Construction understands the project first (a human-readable `projectSummary` plus visible, non-certified assumptions) and then derives the need; General does domain-light commercial extraction. Both reuse the generic `AiIntakeSession` storage (discriminated by `AiIntakeSession.Kind` = General/Construction) and the same OpenAI Responses transport (`OpenAiResponsesTransport`), so neither the HTTP client, config nor telemetry is duplicated.
- One Construction entry produces **exactly one** shared `Rfq` / Procurement Need on approve, through the same publication, matching, discovery, offers, comparison and outcome as General. There is no separate persisted Project entity and no multi-Need decomposition engine. Multi-Need generation (`House → timber + roofing + insulation + …`) remains a documented future capability and must not be built for architectural purity alone.
- `NeedType.Project` is **not** the discriminator and no shared code infers Construction from it. Construction origin is carried by the `AiIntakeSession` (its `Kind` and its `ApprovedRfqId` link) — an observability seam that never forks the commercial pipeline. The manual editor's optional `Rfq.ProjectTemplateId` remains a separate compatibility marker.
- Construction may retain its own domain taxonomy, templates and attributes, but any commercial need it produces uses the same shared `NeedCategory` catalog as General Procurement. General Procurement is an entry mode, not a category or permanent vertical. The provisional compatibility vertical is not a matching rule or a second taxonomy.
- The General interpreter may still return a non-binding `SuggestedProcurementMode.ConstructionProjects`; choosing it routes to `/rfqs/new/construction` and **transfers the original text** (no re-typing). If the Construction interpreter is unavailable, the flow preserves the text and falls back to the manual Construction editor. Construction estimates are procurement assistance only — no engineering certification, no Passive House, no structural/thermal calculation.

## Supplier discovery

`OpportunityMatch` is explicit operational state between a `ProcurementNeed` and a registered Provider or `SupplierCandidate`:

```text
NeedId, ProviderId/SupplierCandidateId, MatchedAt, MatchReason,
optional MatchScore, AdminDecision, InvitationSent, Claimed, OfferSubmitted, Outcome
```

`SupplierCandidate` covers multiple central categories and begins unverified. `OpportunityMatch` records the relevance of one published Procurement Need to exactly one commercial target: a registered `SupplierProfile` company or an unregistered `SupplierCandidate`. It is historical and opportunity-specific. Candidate relevance is `PendingReview`, `Approved`, or `Rejected`; a registered-company match is retained as matching history. Candidate identity/claim state remains separate. An invitation for a newly linked opportunity requires that opportunity's explicit approval; no approval triggers contact automatically. `ContactSuppression` records company/contact, channel, opted-out date and optional reason; invitation delivery also observes deduplication, contact history and rate limits.

### Bounded Admin web discovery

`SupplierWebDiscoveryRun` is temporary, auditable evidence initiated only by an Administrator. A Need-context run follows internal company/candidate matching; an Admin **Firme** lookup has no Need and can only create/reuse a `SupplierCandidate`. Stage 1 builds a bounded intent from the Need’s primary category and category discovery profile, then searches in the strict order local → nearby → county only while evidence is insufficient. Google Places provides local/business geographic recall (including commercially named businesses with generic Google types); OpenAI Web Search provides business/activity/product evidence. The sources independently fail, deterministically merge/deduplicate and retain source plus geographic-tier provenance. Stage 2 is tool-free and converts only that merged evidence into strict JSON candidates/rejections; generic Google type alone never proves product fit. An Administrator explicitly accepts one result before the normal `SupplierCandidate` and, for Need-context only, `OpportunityMatch` flow begins. A run never creates a company, membership, invitation or contact automatically; qualification failure preserves Stage 1 evidence for retry.

### Outreach, contact policy and claim continuity (Task 8)

`SupplierOutreachContact` is the audit record of one human-approved contact attempt against one `OpportunityMatch`: `Channel` (`Email`, `Phone`, `InPerson`, `Sms` reserved/deferred), `Status` (`Pending`, `Sent`, `Failed`, `Recorded`), optional note, and — for email — an opaque hashed opt-out token. It never stores message content, raw tokens, or SMTP credentials. `SupplierContactSuppression` records a permanent "do not contact" decision per `SupplierCandidate`, scoped to `Email` or `AllExternalContact`, with a normalized email so a later rediscovery of the same contact detail is still caught. Both entities are additive to `SupplierDiscovery`; no V2 candidate/match model was introduced.

Eligibility (`ISupplierDiscoveryService.SendEmailInvitationAsync`/`RecordManualContactAsync`) is centralized and re-checked on every attempt, never cached: the `OpportunityMatch` must be `Approved`, the Need must still be open, the Candidate must be unclaimed, the channel must have a usable destination, the Candidate/contact must not be suppressed, and — for `Email` — a prior `Sent` contact for the same `OpportunityMatch` blocks a duplicate send while `SupplierOutreach:EmailCooldownHours` blocks a new email to the same Candidate across opportunities. Suppression is evaluated before every outreach attempt, including after Supplier Discovery rediscovers the same Candidate, so opt-out cannot be bypassed by a new `OpportunityMatch`. Manual `Phone`/`InPerson` contact is recorded directly as `Recorded` and is exempt from the email cooldown, but an explicit "nu mai contacta" action still creates an `AllExternalContact` suppression.

The existing `SupplierInvitation` is reused unchanged in shape, with an optional `OpportunityMatchId` link so an invitation always traces back to the real opportunity that justified contact. Claim continuity reuses the existing Claim Company flow: an authenticated `ApplicationUser` claims the `SupplierCandidate`'s `SupplierProfile`/`CompanyMembership` and is returned to the original opportunity, never to a generic dashboard. A successful Claim marks the Candidate `Claimed`, which the eligibility check above treats as terminal for further external contact — future opportunities for that company use registered-Provider matching and notifications, not Supplier Discovery outreach.

## SEAP tenders (Anunțuri SEAP)

`SeapTender` is a public procurement opportunity imported from e-licitatie.ro — a distinct aggregate, because the buying institution is an external authority, not an `ApplicationUser`, and must not pollute `Rfq`/`ProcurementNeed`.

```text
SeapTender
├── SeapExternalId (unique), Title, Description, BuyerName
├── CpvCodes + CpvNames (aligned: code + human-readable label, e.g. "45342000-6" + "Montare de garduri")
├── County (nullable, deterministic from BuyerName — CountyParser, 42-county alias table)
├── CategoryId (nullable → Category; AI-mapped from the CPV label)
├── EstimatedValue, Currency, PublicationDate, Deadline, Status, attachments
```

- **County** is derived at import from the contracting-authority name ("... VASLUI" → Vaslui, "MUNICIPIUL CLUJ-NAPOCA" → Cluj, "COMUNA GIURGIU" → Giurgiu). No AI; authorities routinely end with the county seat or the county. A name that matches nothing (e.g. a locality that is not a county seat) yields `null`.
- **Categorization** is enrichment, never a gate: `SeapCategorizerService` (an `ILlmProvider`) maps the CPV label to a CompaSpot `Category`; failures leave `CategoryId` null and the tender still imports. `CpvCategoryMapping` (CpvCode → CategoryId) is a persistent cache: because CPV codes are standardized, the LLM is called once per unique code, not once per tender. Unknown categories are auto-created under the "Achiziții generale" root (`GeneralProcurementRootId`), never inside the Construction domain.
- **List filtering** (Anunțuri SEAP): the AI-mapped `CategoryId` is authoritative; the `Category.CpvPrefixes` prefix match remains the fallback for uncategorized tenders. `MarketRadiusKm` is measured county-seat-to-county-seat (`CountyDistance`, 42 county seats, great-circle); tenders with `County = null` are excluded only while a radius filter is explicitly active.
