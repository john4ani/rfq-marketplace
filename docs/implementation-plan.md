# CompaSpot MVP v2 — Implementation plan

## Task 0 — Documentation baseline

Update authoritative direction, domain, UX, architecture, plan, AGENTS and ADRs. No application code. **Stop for review.**

## Task 1 — Stabilization

P0/P1 only: AI availability verification, Live Market error handling, form feedback, dashboard/account overlap, embedded critical states, password recovery, and Provider relevance dead-end. Deploy and test. No redesign.

## Task 2 — Procurement Core analysis

Analysis only. Identify construction-specific assumptions, reusable elements, necessary migration only, generic-need support, retained APIs and construction separation. **Stop for review.**

## Task 3 — Procurement Core implementation

After approval only: 3A remove shared construction assumptions; 3B generic requirement/categories/location; 3C AI compatibility; 3D offer compatibility; 3E construction compatibility. One slice at a time.

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
