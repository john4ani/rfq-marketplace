# Pasul 7 verification — assisted Supplier discovery

Date: 2026-08-06

## Delivered outcome

An Administrator can record a sourced, unverified company; validate or reject its relevance; select a compatible open request; and generate one secure invitation link. The recipient sees a privacy-limited opportunity before onboarding, completes or adds the Supplier profile, and explicitly claims the company and invitation.

The workflow keeps discovery distinct from verification. It never sends outreach automatically, never publishes a discovered company as verified, never exposes the request's exact delivery address, and does not grant Administrator access from configuration or email matching.

## Automated evidence

- Supplier-discovery domain tests: 3 passed, covering initial unverified state, mandatory human-review evidence, and invitation lifecycle.
- Focused API integration: 1 passed, covering Administrator creation and review, category-compatible request invitation, public token view, Supplier profile completion, claim, and idempotent repeat claim.
- Focused Angular tests: admin discovery component 1 passed; public invitation component 1 passed; authentication/Administrator-role handling 5 passed.
- Angular production build: passed with the same four pre-existing component-style budget warnings.
- Additive migration `20260805184347_AddSupplierDiscovery` applied successfully to the preserved local PostgreSQL database.

## Security and UX decisions verified by implementation

1. Candidate records always start as `Unverified`; validation requires an Administrator note.
2. Duplicate company/county candidates are rejected with a conflict response.
3. An invitation can be created only after human review and only for an open, future-deadline RFQ in the candidate's category.
4. The raw 32-byte opaque token is returned once; only its SHA-256 hash is stored.
5. The public view labels the candidate unverified and omits private delivery-address data.
6. Claim requires the Supplier role and a completed Supplier profile, is concurrency-protected, and leaves an immutable audit trail.
7. Registration, sign-in, and Supplier-profile completion preserve the safe invitation return URL.
8. The Admin receives a link to copy manually; no automatic email, contact import, or mass outreach exists.

## Operational note

The existing `Administrator` Identity role gates the workspace. Assignment is an explicit, audited deployment operation outside the public application; no configured-email bootstrap or self-service elevation is included.

## Hosting impact

Pasul 7 adds PostgreSQL tables and ordinary API/UI traffic only. It adds no AWS service, worker, crawler, email campaign, or fixed monthly infrastructure charge. Storage grows by small candidate, invitation, and audit rows; the current Lightsail/PostgreSQL baseline is sufficient for the MVP pilot.
