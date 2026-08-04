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

Add source/input type, explicit need status, normalized delivery constraints, requirement-line semantics, optional generic project context, outcome fields, and audit events. Keep current IDs and API compatibility where practical. Introduce separate publication/recipient records only when required for explainable delivery and invitations.

## Pasul 3 — First assisted-intake slice

Start `/rfqs/new` with “Ce dorești să cumperi sau să construiești?”. Produce a structured draft from natural language, ask one commercially relevant clarification at a time, display assumptions/uncertainties, allow manual edits, and require approval before calling the existing publication flow. Keep the advanced editor as fallback and use a feature flag if rollout risk warrants it.

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
