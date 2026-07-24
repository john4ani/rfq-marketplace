# ADR-0012: Defer Buyer and Provider ratings beyond the MVP

Status: Accepted

Date: 2026-07-24

## Context

Marketplace ratings can improve trust, but a score is only meaningful when it is based on clearly defined, auditable events. The MVP currently supports request publication, offer submission, Buyer selection, and reopening. It does not yet include the complete post-selection workflow required to judge whether either participant fulfilled their commitment fairly.

In particular, the product does not yet have:

- Provider confirmation or rejection of a selected offer;
- a confirmation deadline and reminder policy;
- delivery or fulfilment completion;
- Buyer cancellation and scope-change reasons;
- mutually confirmed completion;
- disputes, evidence, moderation, or appeals;
- protection against retaliation, duplicate reviews, and rating manipulation.

Penalizing a Provider merely because a selected offer was not confirmed would be unreliable without distinguishing an explicit rejection, a timeout, a Buyer-side change, a technical failure, or a legitimate dispute.

## Decision

Buyer and Provider ratings, public review scores, and automated reputation penalties are excluded from the MVP.

The MVP may retain objective operational events needed by the active workflow, but it will not:

- calculate or display public ratings;
- reduce a participant score;
- rank participants using a reputation score;
- allow free-form public reviews;
- treat reopening, rejection, or inactivity as an automatic rating penalty.

Post-selection Provider confirmation can be introduced in a later milestone without activating ratings. Reputation will be designed only after the confirmation and fulfilment lifecycle is stable.

## Prerequisites for a future rating milestone

Before ratings can affect users or matching, the product must define and implement:

1. Provider confirmation, rejection, and timeout states.
2. A visible confirmation deadline with reminders.
3. Buyer cancellation and changed-requirement reasons.
4. Completion or non-completion evidence.
5. Exceptions for technical failures and mutually agreed changes.
6. A dispute and appeal path.
7. Protection against duplicate, retaliatory, and coordinated ratings.
8. Separate objective reliability metrics from subjective reviews.
9. Minimum activity thresholds before publishing an aggregate score.
10. Auditability of every event that changes reputation.

## Future direction

Objective reliability should be introduced before subjective star ratings. Potential signals include:

- selected offers confirmed on time;
- explicit declines after selection;
- confirmation timeouts;
- fulfilments completed;
- cancellations attributable to each party;
- disputes resolved in favor of either participant.

These signals must be shown with context and sufficient sample size. A Provider decline or timeout should only affect reliability after the system can prove that the Provider received the selection, had a fair confirmation window, and was not blocked by a Buyer-side change.

## Consequences

- The MVP remains focused on validating request discovery, offers, and selection.
- Participants are not exposed to unfair or premature reputation damage.
- Later confirmation and fulfilment features must preserve auditable events so reputation can be added without reconstructing history.
- The future rating system requires a separate reviewed decision covering scoring, visibility, moderation, privacy, and abuse prevention.
