# ADR-0020: Procurement outcome feedback loop

Status: Accepted

Date: 2026-08-05

## Context

CompaSpot cannot measure successful procurement from request publication, offers, or selection alone. A selected offer may not become a purchase, and a Buyer may purchase from a participant outside the platform, buy elsewhere, postpone, or receive no useful response. The ProcurementNeed-compatible RFQ already persists an optional outcome, but Buyers had no general command or follow-up experience for recording it.

The MVP has in-app notification polling and immutable procurement audit events. It does not yet have a scheduler, a general analytics event store, email delivery, or the administration workspace planned for Pasul 9.

## Decision

- A Buyer may close a published or offer-receiving request with one supported real-world outcome. `Other` requires an explanation.
- `PurchasedThroughCompaSpot` requires a selected offer. `PurchasedFromParticipant` requires at least one offer on the request. Cancellation remains its dedicated command.
- A selected offer without platform-tracked delivery may be finalized as purchased through CompaSpot. Delivery-tracked offers continue through Provider delivery reporting and Buyer confirmation, which records the outcome automatically.
- For a selected offer without platform-tracked delivery, purchase confirmation is presented inside the selected-offer decision panel. The generic outcome form is shown only when no platform offer was selected, avoiding two competing completion sections.
- Recording an outcome is terminal and immutable. Correcting a selected offer still uses the existing one-time reopen workflow before an outcome is recorded.
- Every explicit result appends `OutcomeRecorded` to the immutable procurement history. Cancellation and delivery confirmation append the same event after their lifecycle event. Existing numeric audit enum values are preserved; the new value is appended.
- Once an open request's quotation deadline passes, the existing notification reads materialize one `ProcurementOutcomeRequested` notification using a unique per-request deduplication key. Concurrent list/count calls may compete safely on that key.
- The audit event is the MVP funnel source. A universal analytics envelope, event warehouse, and administration tools remain Pasul 9 work.

## Consequences

- The product can distinguish real purchase outcomes from engagement and offer-selection signals.
- Buyers are prompted when they return, without a continuously running scheduler or a new AWS service.
- Notification reads perform a small, indexed due-request check for the authenticated Buyer. This is deliberately bounded to the local pilot and can move behind a scheduled worker when email or offline reminders are introduced.
- Outcome corrections after final recording require an explicit future administration policy; the MVP does not silently rewrite funnel history.
- No database migration or additional hosting resource is required because outcome columns, notifications, deduplication, and audit storage already exist.

## Acceptance rules

1. Draft requests cannot record an outcome.
2. Unknown outcomes and unexplained `Other` outcomes are rejected.
3. A successful result closes the request and is returned by Buyer reads.
4. A second outcome attempt is rejected.
5. Expired requests create at most one linked reminder.
6. The Buyer request list and comparison page make a due or completed outcome visible.
7. Historical audit enum values retain their stored meaning.
