# ADR-0021 — Human-controlled Supplier discovery and request-linked claim

Status: Accepted  
Date: 2026-08-06

## Context

The local marketplace can have a real Buyer need before the relevant company has registered. CompaSpot needs a concierge path to identify a possible Supplier without implying that public information is verified, creating unsolicited bulk outreach, or bypassing Supplier consent and onboarding.

Pasul 6's general referral system is not yet implemented. Pasul 7 still requires a narrow invitation that preserves the link between one discovered company and one live procurement opportunity.

## Decision

Introduce three additive records:

- `SupplierCandidate` stores sourced company/contact/locality data, covers one or more categories from the central taxonomy, and always begins as `Unverified`.
- `SupplierInvitation` associates a human-approved candidate with one category-compatible, open RFQ. It stores only a SHA-256 token hash, expires no later than the quotation deadline, and is concurrency-protected.
- `SupplierDiscoveryAuditEvent` records candidate creation, relevance review, rejection, invitation creation, and company claim.

Only an authenticated `Administrator` may create, review, reject, or invite a candidate. Human review means relevant for central marketplace categories; it is not legal, identity, or quality verification. The public page says that the company is unverified and exposes only safe request context. Claim requires an authenticated Supplier with a completed Supplier profile and an explicit action. A successful claim links the candidate to that Supplier, merges its central categories into the Supplier profile, and revokes its other active invitations.

The application produces a copyable invitation link for deliberate one-to-one outreach. It does not import contacts, scrape automatically, send messages automatically, or perform mass outreach.

## Consequences

- A concierge operator can bridge supply gaps for real requests without presenting discovered data as trusted.
- Opaque, expiring, hashed tokens limit damage if persistence is exposed.
- Candidate and claim state is auditable and protected against concurrent decisions.
- Administrator-role assignment remains an explicit operational Identity action; the application does not elevate a configured email automatically.
- General referral attribution, personal share links, retry delivery, analytics, verification, and richer Admin tooling remain in Pasul 6, Pasul 8, and Pasul 9.
