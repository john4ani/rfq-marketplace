# ADR-0015: Separate request selection from delivery confirmation

Status: Accepted

Date: 2026-07-25

## Context

Selecting a Provider completes the sourcing decision but does not prove that goods were delivered. When transport is included, treating `OfferSelected` as the final user-visible state hides an important part of the transaction and provides no reliable point for later ratings, disputes, or fulfilment reporting.

Transport cannot be inferred from a non-zero delivery cost. A Provider may include transport in an all-inclusive price, while a Buyer may arrange pickup or third-party transport separately.

## Decision

The Buyer states whether transport must be included when creating the request. Each Provider explicitly states whether transport is included in the offer. A request that requires transport cannot select an offer that excludes it.

For the MVP, an offer with transport included uses this deliberately small fulfilment flow:

1. Selecting the offer creates `AwaitingDelivery`.
2. The selected Provider marks the order as delivered, creating `DeliveryReported`.
3. The Buyer confirms receipt after checking the goods, creating the final user-visible state `Delivered` and closing the request.

If the selected offer does not include transport, fulfilment is `NotApplicable` and the existing post-selection behaviour remains unchanged.

Request procurement status and fulfilment status are stored separately. This avoids expanding the request status enum for every logistics event while allowing the UI to present `Delivered` as the final status.

The one-time request reopening action is available only before the Provider reports delivery. It cannot be used as a substitute for a future delivery dispute process.

## Authorization and notifications

- Only the Provider whose offer was selected can report delivery.
- Only the Buyer who owns the request can confirm receipt.
- Reporting delivery notifies the Buyer and links to the selected-offer page.
- Confirming receipt notifies the selected Provider and links to the originating request.
- Both transitions are one-way and notification deduplication keys prevent duplicate notification intent.

## Deferred complete workflow

After the MVP, replace the Provider's single “delivered” action with a fuller fulfilment process where justified:

`AwaitingDispatch → Dispatched → InTransit → DeliveryReported → Delivered`

The later design must consider carrier references, estimated arrival, partial deliveries, failed delivery, proof of delivery, disputes, cancellation after award, and automatic reminders. Those concepts must not be added speculatively before operational evidence defines them.

## Consequences

- Buyer and Provider see an honest distinction between commercial selection and completed delivery.
- Pickup and separately arranged transport do not gain unnecessary logistics steps.
- Ratings can later be requested only after a meaningful completion event.
- The MVP relies on participant confirmation rather than carrier integration or proof-of-delivery evidence.
