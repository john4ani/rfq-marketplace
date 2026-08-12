# CompaSpot MVP v2 — Implementation plan

## Task 0 — Documentation baseline

Update authoritative direction, domain, UX, architecture, plan, AGENTS and ADRs. No application code. **Stop for review.**

## Task 1 — Stabilization — completed locally, pending deployment

P0/P1 only: AI availability verification, Live Market error handling, form feedback, dashboard/account overlap, embedded critical states, password recovery, and Provider relevance dead-end. No redesign.

Completed locally:

- verified AI availability is checked before assisted intake starts; unavailable configurations keep the user on the complete form rather than a failed AI flow;
- retained explicit Live Market loading, retry and error states;
- retained actionable form validation and error focus/scroll behaviour;
- separated Dashboard from Account: Dashboard is the work/start surface and Account is a dedicated route for profiles, preferences and trust;
- protected Buyer-only requests from rendering blank for a Supplier/direct URL; profile loading now completes before the Buyer route decision;
- replaced Provider relevance dead ends with return-to-requests and company-preferences actions, and pointed stale preference actions to Account;
- documented registration, role selection, adding a second profile and password recovery in the active UX requirements; sign-in and registration errors now use user language rather than development tooling;
- verified password reset has production configuration validation and keeps an enumeration-safe response;
- verified locally with TypeScript, focused Angular tests (18), focused .NET integration tests (11), and browser navigation on the local stack.

Not deployed in this task. Deployment remains an explicit release action after review.

## Task 2 — Procurement Core analysis — complete, pending review

Analysis only. Identify construction-specific assumptions, reusable elements, necessary migration only, generic-need support, retained APIs and construction separation.

Also analyse the current identity/profile model against the target contextual-role model. Before proposing any implementation, document:

- how `/register` works today;
- which entities are created for a Buyer;
- which entities are created for a Provider;
- how `ActiveProfile` works;
- which foreign keys and ownership rules depend on Buyer/Provider;
- what can remain unchanged;
- the smallest safe migration required;
- how existing accounts, ownership, offers, requests, trust data and company associations are preserved.

Do not implement identity, registration or profile changes in Task 2. **Stop for review.**

Analysis outcome and decision gate: [`task-2-procurement-core-analysis.md`](task-2-procurement-core-analysis.md).

## Task 3 — Procurement Core implementation

After approval only: **3A User-owned Procurement Foundation** makes the primary registration contract User-only, permits every authenticated User to create and use procurement, makes Buyer-profile defaults optional, and retargets only `Rfqs.BuyerId` and `AiIntakeSessions.BuyerId` from `BuyerProfiles` to `ApplicationUser`. It preserves all legacy routes, roles and data. **3B Generic Procurement compatibility** removes the shared single-Construction-root category assumption and adds a small provisional general category seed set for alternators, timber, transport, excavator hire and specialist services. It does not rewrite schema, matching, construction templates or construction state. Subsequent slices: 3C AI compatibility; 3D offer compatibility; 3E construction compatibility. Introducing `IConstructionProjectInterpreter` before Task 9 preserves the existing Construction state exactly as-is and must not add temporary generic `Rfq` fields for new Construction facts. One slice at a time.

## Task 4 — General Procurement UX

UX-1 auth layout and app shell; UX-2 Buyer Home with two entry modes; UX-3 general AI intake; UX-4 review/confirm/publish; UX-5 My Requests. After each slice: local screenshot, relevant tests, stop.

## Task 5 — Supplier Discovery

Reuse `SupplierCandidate`; add generic matching, `OpportunityMatch`, admin approval, dedupe and relevant-match history. No contact until invitation flow is ready.

## Task 6 — Invitation / Claim

Email/SMS, transactional outbox, suppression, rate limiting, preferences and minimal claim-to-offer continuity.

## Task 7 — Provider UX

Opportunities Home, request detail, offer creation, partial offer, decline, claim-to-offer continuity; test with a small established supplier scenario.

## Task 8 — Best Value comparison

Decision-first comparison of total, coverage, delivery, condition, warranty, transport, trust, alternatives and explainable AI assistance—never cheapest-only.

## Task 9 — Construction repositioning

Preserve working construction capability, isolate specialised interpretation, feed the Procurement Core, and retain a dedicated construction journey. Test a general alternator, a roof, and a passive-house request.

## Task 10 — Hardening

Responsive/mobile, accessibility, Forest Warm consistency, recovery states, metrics, latency, logs and documentation synchronization.
