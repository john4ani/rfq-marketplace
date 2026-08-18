# ADR-004 — Human-approved Supplier Discovery

## Decision

Supplier discovery is a core MVP capability, but no new candidate may be contacted before Administrator approval for the specific procurement opportunity. Candidate identity review and opportunity relevance are distinct decisions.

## Consequences

Persist explicit opportunity matching, candidate review, invitation history, contact suppression and delivery controls. Use real opportunities only, deduplicate contact, respect permanent opt-out, and rate-limit outreach. An approval records eligibility only; an Administrator must separately choose to create and send any invitation. Supplier invitations may link to their approved OpportunityMatch while keeping existing request-linked invitations compatible.
