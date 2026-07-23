# Main Sequence Diagrams

Status: Proposed

## Registration, verification, and login

```mermaid
sequenceDiagram
    actor User
    participant SPA as Angular SPA
    participant API as Identity module
    participant DB as PostgreSQL
    participant Outbox as Notification outbox
    participant SES as Amazon SES

    User->>SPA: Submit registration
    SPA->>API: POST /auth/register/{persona}
    API->>DB: Create user and profile
    API->>Outbox: Append verification notification
    API-->>SPA: 202 registration pending verification
    Outbox->>SES: Send verification email asynchronously
    User->>API: POST /auth/verify-email
    API->>DB: Verify single-use token and email
    API-->>User: Verification result
    User->>API: POST /auth/login
    API->>DB: Validate credentials and account state
    API-->>User: Short-lived JWT + secure refresh cookie
```

## RFQ creation and publication

```mermaid
sequenceDiagram
    actor Buyer
    participant SPA
    participant API as RFQ module
    participant DB as PostgreSQL
    participant Audit

    Buyer->>SPA: Enter RFQ and paste material rows
    SPA->>API: POST /rfqs/items:parse-pasted
    API-->>SPA: Parsed preview and field errors
    Buyer->>SPA: Confirm rows
    SPA->>API: POST /rfqs
    API->>DB: Save Draft and items
    API-->>SPA: RFQ response with version
    Buyer->>SPA: Publish
    SPA->>API: POST /rfqs/{id}/publish + Idempotency-Key
    API->>API: Authorize owner and validate invariants
    API->>DB: Draft -> Published
    API->>Audit: Append RFQ published
    API-->>SPA: Published RFQ
```

## Supplier discovery

```mermaid
sequenceDiagram
    actor Supplier
    participant SPA
    participant API as Supplier discovery
    participant DB as PostgreSQL
    participant Distance as Distance rules

    Supplier->>SPA: Browse relevant RFQs
    SPA->>API: GET /supplier/rfqs?filters
    API->>DB: Load approved active supplier settings
    API->>DB: Query open RFQs by category/county/deadline
    API->>Distance: Apply coordinates/radius or county fallback
    Distance-->>API: Eligible RFQs with approximate distance
    API->>API: Create privacy-safe projections
    API-->>SPA: Paginated relevant RFQs
```

## Offer submission and revision

```mermaid
sequenceDiagram
    actor Supplier
    participant SPA
    participant API as Offers module
    participant RFQ as RFQ module
    participant DB as PostgreSQL
    participant Outbox

    Supplier->>SPA: Complete line responses
    SPA->>API: PUT /offers/{id}
    API->>RFQ: Validate RFQ open and supplier eligible
    API->>API: Calculate lines, totals, and completeness
    API->>DB: Save draft using concurrency version
    Supplier->>API: POST /offers/{id}/submit
    API->>DB: Save Submitted offer and audit
    API->>Outbox: Append offer-received notification
    API-->>SPA: Authoritative submitted offer

    Supplier->>API: PUT /offers/{id} before deadline
    API->>DB: Append previous commercial snapshot
    API->>API: Recalculate authoritative values
    API->>DB: Save Revised offer
    API->>Outbox: Append offer-revised notification
    API-->>SPA: Revised offer and revision number
```

## Private messaging

```mermaid
sequenceDiagram
    actor Sender
    participant SPA
    participant API as Messaging module
    participant Auth as Resource authorization
    participant DB as PostgreSQL
    participant Outbox

    Sender->>API: POST /conversations/{id}/messages
    API->>Auth: Verify buyer/supplier participation
    Auth-->>API: Allowed
    API->>DB: Append message
    API->>Outbox: Append recipient notification
    API-->>SPA: Created message
    loop Periodic polling while view is active
        SPA->>API: GET /conversations/{id}/messages?after=cursor
        API->>Auth: Verify participation
        API-->>SPA: New messages
    end
```

## Transactional offer acceptance

```mermaid
sequenceDiagram
    actor Buyer
    participant SPA
    participant API as Offer acceptance use case
    participant DB as PostgreSQL
    participant Outbox

    Buyer->>API: POST /rfqs/{rfqId}/offers/{offerId}/accept
    Note over Buyer,API: Idempotency-Key and expected RFQ version
    API->>DB: Begin transaction
    API->>DB: Load and protect RFQ + candidate offer
    API->>API: Authorize owner; verify open RFQ, deadline, validity
    API->>DB: Mark candidate Accepted
    API->>DB: Mark competitors Rejected
    API->>DB: Mark RFQ OfferSelected
    API->>DB: Append audit and notification rows
    API->>DB: Commit
    API-->>SPA: Accepted offer and updated RFQ
    Outbox-->>Buyer: Send confirmation asynchronously
    Outbox-->>Outbox: Notify winning and unsuccessful suppliers
```

The database transaction and concurrency controls guarantee one winner even when acceptance requests race.

## Attachment upload and download

```mermaid
sequenceDiagram
    actor User
    participant SPA
    participant API as Attachments module
    participant S3 as Private S3 bucket
    participant DB as PostgreSQL

    User->>API: POST /attachments/upload-requests
    API->>API: Authorize parent and validate metadata
    API->>DB: Create PendingUpload metadata
    API->>S3: Create short-lived presigned PUT URL
    API-->>SPA: Upload ID and presigned URL
    SPA->>S3: Upload file directly
    SPA->>API: POST /attachments/{id}/complete
    API->>S3: HEAD object and verify size/type
    API->>DB: Mark Available
    User->>API: GET /attachments/{id}/download-url
    API->>API: Authorize access to parent resource
    API->>S3: Create short-lived presigned GET URL
    API-->>User: Download URL
```

## Notification processing

```mermaid
sequenceDiagram
    participant Command as Business command
    participant DB as PostgreSQL
    participant Worker as Hosted worker
    participant SES as Amazon SES

    Command->>DB: Commit business data + Pending notification
    loop Poll available work
        Worker->>DB: Atomically claim batch
        Worker->>SES: Send rendered email
        alt Send succeeds
            Worker->>DB: Mark Sent
        else Transient failure
            Worker->>DB: Return Pending with exponential backoff
        else Permanent/max attempts
            Worker->>DB: Mark DeadLetter
        end
    end
```
