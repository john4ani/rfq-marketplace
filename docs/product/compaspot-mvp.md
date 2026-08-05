# CompaSpot MVP product master

## Product definition

CompaSpot turns a need expressed by a person or company into a clear, assisted, and completed procurement process.

> From “I need…” to a completed purchase.

The reverse marketplace is the first execution mechanism, not the permanent boundary of the product. Construction, wood, pellets, and local materials are the initial commercial vertical for the Suceava micro-market; the horizontal workflow remains industry-neutral.

## Product principles

- The user must understand their own problem, not the application’s internal model.
- A need may begin as free text, a list, a project description, or—later—a document, image, voice message, or email.
- AI may extract, clarify, normalize, and explain, but cannot publish, select a winner, claim stock or verification, or present engineering estimates as validated design.
- The user always sees and approves the structured need before publication.
- Matching is capability- and delivery-based and must be explainable.
- Trust claims must be supported by real verification or behavior.
- Success is a real procurement outcome, not a click or registration.

## Conceptual model

The central future concept is `ProcurementNeed`, containing its initiator, buyer context, delivery constraints, optional project context, requirement lines, status, and outcome. The existing `Rfq` currently represents the approved and publishable part of that concept:

```text
Current Rfq ≈ approved ProcurementNeed + executable RFQ
Current RfqItem ≈ RequirementLine
```

This mapping is deliberate for the first phases. Step 2 implements it: the existing aggregate now records source/input type, product or service requirement lines, outcome fields, and audit history, while normalized need status, project context, and delivery constraints are exposed without duplicating persisted lifecycle data. CompaSpot will split the aggregate only when real workflows require independent pre-approval or publication lifecycles.

## MVP circuit

```text
Buyer expresses a need
  → structured request draft
  → Buyer approval and publication
  → explainable matching to eligible Providers
  → Provider response
  → comparable offers
  → Buyer decision
  → recorded real-world outcome
```

The initial pilot is local and concentric: the founder’s village, nearby villages, Rădăuți and its delivery area, then Suceava county. Operations may be concierge-assisted while the real patterns are learned.

## MVP functional scope

1. Assisted request creation with the existing advanced form retained as fallback.
2. Editable preview and explicit publication approval.
3. Capability/category and delivery-area matching to registered Providers.
4. Human-controlled discovery and invitation of external Provider candidates.
5. Complete and partial Provider responses with item prices, transport, delivery time, alternatives, validity, tax/commercial notes, and coverage.
6. Explainable offer comparison without claiming that lowest price is automatically best.
7. Explicit procurement outcome and feedback.
8. Basic trust, referral attribution, analytics events, audit events, and concierge administration.

## Explicit non-goals

The MVP does not include payments, accounting, full ERP or inventory, structural engineering, guaranteed BOM generation, native mobile applications, mass outreach, complex chat, prestige scores, gamification, ML matching, a universal vertical plug-in framework, or microservices.

## North-star measure

`Successful Procurements`: real purchases completed after a CompaSpot process. Supporting measures include request-to-publication time, eligible and invited Providers, response rate, time to first offer, comparable-offer coverage, confirmed outcomes, repeat Buyers, repeat Providers, invitation conversion, and required concierge interventions.

## Delivery order

The implementation follows the audited vertical slices in [implementation-plan.md](../architecture/implementation-plan.md). AI intake begins only after the existing two-party request-to-offer circuit is stable and demonstrably covered.

Pasul 5 closes the first measurable circuit: the Buyer records the actual procurement result, expired requests prompt once on return, and cancellation or confirmed delivery enter the same outcome funnel. These events measure the marketplace without introducing ratings, automatic publication, or a general analytics platform.
