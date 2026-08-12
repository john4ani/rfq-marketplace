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

## Shared Procurement Core

`ProcurementNeed` is the user’s Cerere. The existing `Rfq` remains the compatible internal implementation until a concrete workflow requires a split.

```text
ProcurementNeed (current Rfq)
├── RequirementLines (current RfqItems)
├── location + delivery/service preferences
├── publication + lifecycle
├── supplier matching
├── offers + outcome
└── NeedCategories 1..N
```

It must support a car alternator, timber, transport, excavator hire, electrical materials, or a construction-generated need without construction-only assumptions. A need can have one or many categories regardless of whether it is a direct purchase, list, or project. Each requirement line belongs to the relevant need category grouping.

## Construction boundary

`ConstructionProject` owns construction-specific facts, terminology, assumptions, systems/work packages and safe decompositions. It may generate one or more `ProcurementNeed`s. Procurement does not know about roof pitch, airtightness, passive-house systems, or engineering rules.

## Supplier discovery

`OpportunityMatch` is explicit operational state between a `ProcurementNeed` and a registered Provider or `SupplierCandidate`:

```text
NeedId, ProviderId/SupplierCandidateId, MatchedAt, MatchReason,
optional MatchScore, AdminDecision, InvitationSent, Claimed, OfferSubmitted, Outcome
```

`SupplierCandidate` covers multiple central categories and begins unverified. Administrator approval is required before an invitation. `ContactSuppression` records company/contact, channel, opted-out date and optional reason; invitation delivery also observes deduplication, contact history and rate limits.
