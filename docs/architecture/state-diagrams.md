# State Diagrams

Status: Proposed

## RFQ lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Published: Buyer publishes valid RFQ
    Draft --> Cancelled: Buyer cancels
    Published --> ReceivingOffers: First offer submitted
    Published --> Cancelled: Buyer cancels
    Published --> Expired: Deadline passes
    ReceivingOffers --> OfferSelected: Buyer accepts valid offer
    ReceivingOffers --> Cancelled: Buyer cancels
    ReceivingOffers --> Expired: Deadline passes
    OfferSelected --> Closed: Buyer closes after confirmation
    Cancelled --> [*]
    Expired --> [*]
    Closed --> [*]
```

Commercial fields are editable in `Draft`. After publication, changes are audited; after the first offer, quantity and specification changes are blocked in the MVP.

## Offer lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Submitted: Supplier submits before deadline
    Draft --> Withdrawn: Supplier abandons offer
    Submitted --> Revised: Supplier changes commercial values
    Revised --> Revised: Supplier submits another revision
    Submitted --> Accepted: Buyer selects offer
    Revised --> Accepted: Buyer selects offer
    Submitted --> Rejected: Competing offer selected
    Revised --> Rejected: Competing offer selected
    Submitted --> Withdrawn: Supplier withdraws
    Revised --> Withdrawn: Supplier withdraws
    Submitted --> Expired: Offer validity/deadline passes
    Revised --> Expired: Offer validity/deadline passes
    Accepted --> [*]
    Rejected --> [*]
    Withdrawn --> [*]
    Expired --> [*]
```

Revisions append immutable commercial snapshots. No transition out of a terminal state is allowed.

## Supplier lifecycle

```mermaid
stateDiagram-v2
    [*] --> PendingApproval
    PendingApproval --> Approved: Administrator approves
    PendingApproval --> Suspended: Administrator suspends
    Approved --> Suspended: Administrator suspends
    Suspended --> Approved: Administrator reactivates
```

An additional `is_active` setting lets an approved supplier voluntarily pause participation without changing administrative approval.

## Attachment lifecycle

```mermaid
stateDiagram-v2
    [*] --> PendingUpload
    PendingUpload --> Uploaded: Client uploads to presigned S3 URL
    PendingUpload --> Expired: Upload window ends
    Uploaded --> Available: API verifies S3 metadata
    Uploaded --> Rejected: Size/type/metadata mismatch
    Available --> Deleted: Authorized logical deletion
    Rejected --> [*]
    Expired --> [*]
    Deleted --> [*]
```

Only `Available` attachments receive download URLs.

## Notification lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Processing: Worker claims row
    Processing --> Sent: Provider confirms send
    Processing --> Pending: Transient failure and backoff
    Processing --> DeadLetter: Permanent failure or max attempts
    Processing --> Pending: Stale claim recovered
    Sent --> [*]
    DeadLetter --> [*]
```

Delivery is at-least-once. A deduplication key prevents repeated business events from creating duplicate notification intent.
