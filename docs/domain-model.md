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
└── NeedCategories 1..N
```

It must support a car alternator, timber, transport, excavator hire, electrical materials, or a construction-generated need without construction-only assumptions. A need can have one or many categories regardless of whether it is a direct purchase, list, or project. Each requirement line belongs to the relevant need category grouping.

General Procurement is a generic entry mode into this Core, not a permanent domain Vertical. The current `Vertical`/`Category` model may be reused for compatibility. If a general placeholder Vertical is temporarily necessary, it must be explicitly provisional and must not carry domain rules or determine matching semantics.

The General interpreter may return a non-binding `SuggestedProcurementMode` when a description appears to be a construction project. It is a UI hint only: it never converts the need, redirects the user, or persists new Construction facts in `Rfq`.

`NeedType.Project` is a legacy/general shape for a project-like Procurement Need. It is not a `ConstructionProject` and must not be used to enter, select, or infer the Construction bounded context. Construction mode is explicit and owns the specialised project context before it generates Procurement Needs.

Until Task 9, the current compatible Construction state remains stored and handled exactly as it is. `IConstructionProjectInterpreter` may be introduced before the aggregate solely as an interpretation boundary. It must not create temporary generic `Rfq` fields, or place new Construction facts in the Procurement Core.

## Construction boundary

`ConstructionProject` owns construction-specific facts, terminology, assumptions, systems/work packages and safe decompositions. It may generate one or more `ProcurementNeed`s. Procurement does not know about roof pitch, airtightness, passive-house systems, or engineering rules.

## Supplier discovery

`OpportunityMatch` is explicit operational state between a `ProcurementNeed` and a registered Provider or `SupplierCandidate`:

```text
NeedId, ProviderId/SupplierCandidateId, MatchedAt, MatchReason,
optional MatchScore, AdminDecision, InvitationSent, Claimed, OfferSubmitted, Outcome
```

`SupplierCandidate` covers multiple central categories and begins unverified. `OpportunityMatch` records the relevance of one published Procurement Need to exactly one commercial target: a registered `SupplierProfile` company or an unregistered `SupplierCandidate`. It is historical and opportunity-specific. Candidate relevance is `PendingReview`, `Approved`, or `Rejected`; a registered-company match is retained as matching history. Candidate identity/claim state remains separate. An invitation for a newly linked opportunity requires that opportunity's explicit approval; no approval triggers contact automatically. `ContactSuppression` records company/contact, channel, opted-out date and optional reason; invitation delivery also observes deduplication, contact history and rate limits.

### Outreach, contact policy and claim continuity (Task 8)

`SupplierOutreachContact` is the audit record of one human-approved contact attempt against one `OpportunityMatch`: `Channel` (`Email`, `Phone`, `InPerson`, `Sms` reserved/deferred), `Status` (`Pending`, `Sent`, `Failed`, `Recorded`), optional note, and — for email — an opaque hashed opt-out token. It never stores message content, raw tokens, or SMTP credentials. `SupplierContactSuppression` records a permanent "do not contact" decision per `SupplierCandidate`, scoped to `Email` or `AllExternalContact`, with a normalized email so a later rediscovery of the same contact detail is still caught. Both entities are additive to `SupplierDiscovery`; no V2 candidate/match model was introduced.

Eligibility (`ISupplierDiscoveryService.SendEmailInvitationAsync`/`RecordManualContactAsync`) is centralized and re-checked on every attempt, never cached: the `OpportunityMatch` must be `Approved`, the Need must still be open, the Candidate must be unclaimed, the channel must have a usable destination, the Candidate/contact must not be suppressed, and — for `Email` — a prior `Sent` contact for the same `OpportunityMatch` blocks a duplicate send while `SupplierOutreach:EmailCooldownHours` blocks a new email to the same Candidate across opportunities. Suppression is evaluated before every outreach attempt, including after Supplier Discovery rediscovers the same Candidate, so opt-out cannot be bypassed by a new `OpportunityMatch`. Manual `Phone`/`InPerson` contact is recorded directly as `Recorded` and is exempt from the email cooldown, but an explicit "nu mai contacta" action still creates an `AllExternalContact` suppression.

The existing `SupplierInvitation` is reused unchanged in shape, with an optional `OpportunityMatchId` link so an invitation always traces back to the real opportunity that justified contact. Claim continuity reuses the existing Claim Company flow: an authenticated `ApplicationUser` claims the `SupplierCandidate`'s `SupplierProfile`/`CompanyMembership` and is returned to the original opportunity, never to a generic dashboard. A successful Claim marks the Candidate `Claimed`, which the eligibility check above treats as terminal for further external contact — future opportunities for that company use registered-Provider matching and notifications, not Supplier Discovery outreach.
