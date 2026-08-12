# CompaSpot MVP gap analysis

## Executive assessment

The repository is healthy enough for incremental evolution. It already implements most of the mechanical reverse-marketplace core, so a rewrite would destroy value. The next work should stabilize the real two-party circuit, evolve the current RFQ model into the approved-need boundary, and only then add assisted intake.

## Functional gaps

| Capability | Assessment | Evidence / gap |
|---|---|---|
| Registration and additive roles | Complete for current MVP | Unique normalized email, Buyer/Supplier roles, profile upgrade, recovery |
| Buyer request draft and publication | Complete with lifecycle gaps | Structured form, draft/update/publish; no audit or publication recipients |
| Registered Provider matching | Partial | Category/county/capability works; no returned match reasons, quantity limits, declared availability, or reliable radius calculation |
| Provider receives opportunity | Partial | Pull discovery on Piața Live/Open Requests; no publication event or targeted Provider notification |
| Provider offer response | Partial | Itemized/total, transport, timing, validity, terms; alternatives, VAT, explicit missing-line semantics, and coverage summary incomplete |
| Buyer comparison | Partial | Totals and terms are visible; unavailable lines currently inflate totals and coverage/missing lines are not clearly compared |
| Selection and fulfilment | Partial | Award, one reopen, delivery report/confirm exist; no general procurement outcome taxonomy |
| External Provider discovery | Implemented first slice | Sourced unverified candidates, human validation, request-linked invitations, secure claim, and audit; automated discovery/outreach excluded |
| Referrals | Missing | No invitation token, attribution, sharing events, or conversion state |
| Trust foundation | Implemented first slice | Factual verification state, profile completeness, participation facts, Administrator review and account audit; ratings and public reputation remain deferred |
| Notifications | Partial | In-app offer/message/reopen/delivery events; no email opportunity delivery or deadline/outcome reminders |
| Concierge administration | Partial | Supplier discovery workspace is implemented; correction, matching override, invitation retry, normalization support, and AI review remain Pasul 9 |
| Analytics | Missing | No extensible funnel event store |
| AI intake | Missing intentionally | Must follow Pasul 1 and request-model evolution |

## Stabilization defects and risks

### Immediate Pasul 1 defects

1. The critical happy path lacks a separate-Buyer/separate-Provider integration test.
2. `Offer.ItemsTotal` includes unavailable lines, so the displayed and sorted total can be commercially false.
3. `Rfq.Cancel` permits cancellation after an offer is selected or delivery is confirmed, allowing inconsistent lifecycle data through direct API use.
4. Offer submission actions have UI busy state but no method-level re-entry guard.
5. Selection, cancellation, reopening, and delivery transitions have no optimistic-concurrency token. Competing requests can both report success while the last write silently wins.

### Migration and data risks

- Splitting `Rfq` into ProcurementNeed, publication, and RFQ tables too early would create a high-risk data migration without immediate user value.
- Introducing organizations requires migration of embedded company fields and a membership/ownership decision; it should be additive first.
- Existing general requests may have no lines and legacy offers may store an all-inclusive value as delivery cost; compatibility handling already exists and must remain.
- Enum additions are low-schema-risk because EF stores current enum values as integers, but all API/frontend mappings and old records must be reviewed.
- Publication recipients cannot be reconstructed perfectly from current data because matching is evaluated at read time against mutable Provider settings.

### Technical risks

- `RfqService` and `OfferService` contain query orchestration and policy concerns that will become difficult to evolve without extracting focused application use cases.
- Matching loads broad Provider/category/settings sets into memory for coverage decisions. This is acceptable for the local pilot but must be measured.
- Product audit, analytics, and notification delivery are direct writes rather than an outbox-backed event pipeline; ADR-0005 describes an outbox direction that is not implemented.
- Supplier approval is stored but not enforced as a verified trust claim.
- Several Angular components are large and inline-template heavy; avoid broad refactoring during product slices, but extract when a slice creates a second real responsibility.
- Existing style-budget warnings reduce headroom and should be addressed during planned quality hardening.

## Smallest coherent next change

Pasul 1 should not create new ProcurementNeed or AI abstractions. It should:

- add a true two-party integration test from request creation through Buyer offer visibility;
- correct unavailable-line commercial totals in domain and UI;
- enforce legal cancellation states;
- prevent repeated Provider save/submit actions;
- add optimistic concurrency for RFQ decision transitions and a useful conflict response;
- exercise the same journey in a real browser for separate roles and dual-role access;
- keep the audit and acceptance documentation synchronized.

This creates a trustworthy execution seam for Pasul 2 and Pasul 3 without a rewrite.
