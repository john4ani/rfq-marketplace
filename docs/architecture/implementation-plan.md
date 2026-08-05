# CompaSpot incremental implementation plan

## Principles

- Preserve the modular monolith, PostgreSQL, ASP.NET Core Identity, Angular, deployment topology, and existing RFQ workflow.
- Deliver one observable user outcome per slice.
- Add abstractions only when the current slice creates a second real implementation.
- Keep migrations additive and compatible with existing requests/offers.
- Keep construction knowledge outside the generic procurement core.

## Pasul 1 — Stabilize the existing two-party circuit

**Status:** implemented; automated and browser acceptance evidence is recorded with the delivery report.

**Outcome:** a real Buyer and a different eligible Provider can complete the existing request-to-offer circuit reliably, while a dual-role account retains all valid actions.

Scope:

1. Separate-identity integration acceptance from request creation and publication through matching, Provider offer submission, Buyer notification, and Buyer comparison.
2. Correct totals for unavailable offer lines.
3. Legal cancellation-state enforcement.
4. Provider offer action re-entry protection and useful errors.
5. Optimistic concurrency for RFQ decision transitions with a retry/reload response.
6. Browser acceptance for Buyer, Provider, and dual-role behavior.

Exit criteria: full builds/tests pass, preserved database upgrades, real browser journey passes, central lifecycle cannot be silently corrupted, and no AI/model expansion is introduced.

## Pasul 2 — Evolve Request toward ProcurementNeed

**Status:** implemented; automated, migration, and browser acceptance evidence is recorded in the Step 2 verification report.

**Outcome:** the existing request is now the first backward-compatible ProcurementNeed implementation: it records source/input type, exposes an explicit need status, normalizes delivery constraints and optional project context, supports product or service requirement lines, carries outcome fields, and records an immutable lifecycle history.

Current IDs, routes, flat compatibility fields, and existing data remain valid. Separate publication/recipient records remain deferred until explainable delivery or invitation behavior gives them an independent lifecycle. See [ADR-0017](../decisions/ADR-0017-evolve-rfq-as-procurement-need.md).

## Pasul 3 — First assisted-intake slice

**Status:** implemented; final automated and browser acceptance evidence is recorded in the Step 3 verification report.

Start `/rfqs/new` with “Ce dorești să cumperi sau să construiești?”. Use one simple sequence: clarification → model-assisted analysis → editable proposed requirement with assumptions/uncertainties → human confirmation. Never publish automatically. Keep the advanced editor as the direct fallback when AI is unavailable and use a feature flag if rollout risk warrants it.

The model is an extraction and analysis adapter, not a lifecycle authority. Direct OpenAI GPT-5.6 Luna is the development/staging default; Azure OpenAI remains a configuration-only production option for the pending EU Data Zone quota. Live web search is intentionally excluded. Sessions and token counts are persisted, schema/category values are server-validated, provider failure transfers the user to the advanced form, and approval creates the existing editable RFQ draft without publishing it. See [ADR-0018](../decisions/ADR-0018-assisted-procurement-intake.md).

## Pasul 4 — Offer completeness and comparison

Add explicit full/partial coverage, missing positions, alternatives, VAT/tax presentation, commercially valid totals, delivery distinctions, and line-by-line comparison. Recommendations must explain trade-offs rather than label lowest price as best.

## Pasul 5 — Procurement outcome and feedback

Record purchased through CompaSpot, purchased from a participant, purchased elsewhere, postponed, cancelled, no suitable offer, insufficient responses, or another reason. Add follow-up notifications and funnel events.

## Pasul 6 — Invitations and referrals

Add secure opportunity invitations, personal invitation links, registration source and inviter attribution, controlled share actions, and invitation lifecycle analytics. No automated contact import or rewards.

## Pasul 7 — Assisted Supplier discovery

Add unverified Supplier candidates, human validation, request-linked invitations, and company claim flow. Never present discovered data as verified and never send mass outreach automatically.

## Pasul 8 — Trust foundation

Add email, phone, and company verification state; profile completeness; participation history; and audit of important actions. Keep ratings, public scores, tiers, and gamification deferred.

## Pasul 9 — Analytics and concierge administration

Add an extensible event format and an Admin workspace for request correction, matching overrides, candidate validation, invitation retry, offer normalization support, closure/outcome recording, and AI failure review.

## Deferred platform evolution

Organization/membership separation, company workspaces, inventory/catalog integrations, richer fulfilment, vertical knowledge policies, and procurement intelligence follow demonstrated pilot use. Microservices and a universal plug-in framework are not planned for the MVP.
