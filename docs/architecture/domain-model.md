# Domain Model and Aggregate Boundaries

Status: Implemented through the central category, procurement need, offer, conversation, and supplier-discovery aggregates; attachments remain proposed

```mermaid
classDiagram
    class ApplicationUser {
      +UUID Id
      +Email Email
      +UserStatus Status
      +verifyEmail()
      +suspend()
    }
    class RefreshToken {
      +UUID Id
      +Hash TokenHash
      +DateTime ExpiresAt
      +rotate()
      +revokeFamily()
    }
    class UserPresence {
      +UUID UserId
      +DateTime LastSeenAt
      +markSeen()
      +isOnline()
    }
    class BuyerProfile {
      +UUID Id
      +BuyerType Type
      +string DisplayName
      +int MarketRadiusKm
    }
    class SupplierProfile {
      +UUID Id
      +ApprovalStatus ApprovalStatus
      +bool IsActive
      +approve()
      +suspend()
    }
    class SupplierDeliverySettings {
      +Distance NormalRadius
      +Distance MaximumRadius
      +Money MinimumOrderValue
      +bool NationwideDelivery
      +update()
    }
    class Vertical {
      +UUID Id
      +string Name
      +string Slug
      +bool IsActive
    }
    class Category {
      +UUID Id
      +UUID VerticalId
      +UUID ParentId
      +string Name
      +string Slug
      +deactivate()
    }
    class Rfq {
      +UUID Id
      +UUID BuyerId
      +RfqStatus Status
      +ProcurementNeedType NeedType
      +UUID ProjectTemplateId
      +bool DeliveryRequired
      +FulfillmentStatus FulfillmentStatus
      +DateTime QuotationDeadline
      +int SupplierSearchRadiusKm
      +publish()
      +cancel()
      +selectOffer()
      +reportDelivered()
      +confirmDelivery()
    }
    class NeedCategory {
      +UUID Id
      +UUID ProcurementNeedId
      +UUID CategoryId
      +int SortOrder
    }
    class RfqItem {
      +UUID Id
      +int ItemNumber
      +string ProductName
      +decimal Quantity
      +UnitOfMeasure Unit
    }
    class Offer {
      +UUID Id
      +UUID RfqId
      +UUID SupplierId
      +OfferStatus Status
      +bool DeliveryIncluded
      +Money Total
      +decimal Completeness
      +submit()
      +revise()
      +accept()
      +reject()
    }
    class OfferItem {
      +UUID RfqItemId
      +decimal QuantitySupplied
      +Money UnitPrice
      +AvailabilityStatus Availability
      +bool IsAlternative
    }
    class OfferRevision {
      +int RevisionNumber
      +json CommercialSnapshot
      +DateTime CreatedAt
    }
    class Conversation {
      +UUID Id
      +UUID OfferId
      +sendMessage()
      +markRead()
    }
    class Message {
      +UUID Id
      +UUID SenderUserId
      +string Body
      +DateTime CreatedAt
    }
    class Attachment {
      +UUID Id
      +AttachmentStatus Status
      +string StorageKey
      +completeUpload()
      +authorizeDownload()
    }
    class Notification {
      +UUID Id
      +NotificationStatus Status
      +int Attempts
      +claim()
      +markSent()
      +scheduleRetry()
    }
    class AuditEntry {
      +UUID Id
      +string Action
      +UUID ActorUserId
      +DateTime OccurredAt
    }
    class ProjectTemplate {
      +UUID Id
      +UUID VerticalId
      +string Name
      +bool IsActive
    }
    class SupplierCandidate {
      +UUID Id
      +string CompanyName
      +SupplierCandidateStatus Status
      +validateRelevance()
      +reject()
      +claim()
    }
    class SupplierInvitation {
      +UUID Id
      +Hash TokenHash
      +DateTime ExpiresAt
      +claim()
      +revoke()
    }
    class SupplierDiscoveryAuditEvent {
      +UUID Id
      +string EventType
      +UUID ActorUserId
      +DateTime OccurredAt
    }

    ApplicationUser "1" *-- "0..*" RefreshToken
    ApplicationUser "1" *-- "0..1" UserPresence
    ApplicationUser "1" --> "0..1" BuyerProfile
    ApplicationUser "1" --> "0..1" SupplierProfile
    Vertical "1" --> "1..*" Category
    SupplierProfile "0..*" -- "0..*" Category
    SupplierProfile "1" *-- "0..1" SupplierDeliverySettings
    Category "0..1" --> "0..*" Category : parent
    BuyerProfile "1" --> "0..*" Rfq
    Rfq "1" *-- "1..*" NeedCategory
    NeedCategory "0..*" --> "1" Category
    NeedCategory "1" *-- "0..*" RfqItem
    Vertical "1" --> "0..*" ProjectTemplate
    ProjectTemplate "0..*" -- "1..*" Category
    Rfq "0..*" --> "0..1" ProjectTemplate
    Rfq "1" --> "0..*" Offer
    SupplierProfile "1" --> "0..*" Offer
    Offer "1" *-- "1..*" OfferItem
    OfferItem "*" --> "1" RfqItem
    Offer "1" *-- "0..*" OfferRevision
    Offer "1" --> "0..1" Conversation
    Conversation "1" *-- "0..*" Message
    Category "0..*" -- "0..*" SupplierCandidate
    SupplierCandidate "1" *-- "0..*" SupplierInvitation
    SupplierInvitation "*" --> "1" Rfq
    SupplierCandidate "1" --> "0..*" SupplierDiscoveryAuditEvent
```

## Aggregate ownership

| Aggregate root | Owned state | Important invariants |
|---|---|---|
| `ApplicationUser` | Refresh-token families | Email uniqueness; token rotation and revocation |
| `UserPresence` | Last activity heartbeat | One record per user; online status expires after two minutes |
| `BuyerProfile` | Buyer profile fields and market radius | One profile per user; radius is 1–1,000 km and defaults to 30 km |
| `SupplierProfile` | Categories, delivery areas, preferences | Approved and active to participate; valid radii |
| `Vertical` | Market boundary and active state | Construction is the only active MVP vertical |
| `Category` | Central category identity, vertical, and optional parent link | One taxonomy is reused by needs, Providers, templates, and discovery; unique slug; no parent cycle |
| `Rfq` / `ProcurementNeed` | Need type, exactly one primary `NeedCategory`, optional requirement lines, optional project template, and supplier-search radius | Need type never controls category; requirement lines have no category association; publication requires a future deadline; published needs are immutable |
| `ProjectTemplate` | Editable starting set of central categories for a project | A template is not a category and references at least one category in its vertical |
| `Offer` | Offer items and revision snapshots | One per supplier/RFQ; server-calculated money and completeness |
| `Conversation` | Messages and participant read state | Exactly the RFQ buyer and offer supplier may participate |
| `Attachment` | File metadata and lifecycle | Authorized parent; verified object before availability |
| `Notification` | Attempts and delivery state | At-least-once, safely retryable delivery |
| `SupplierCandidate` | Sourced company data, central-category coverage, human review, claim link | Covers one or more central categories; starts unverified; only reviewed candidates may be invited; one claim |
| `SupplierInvitation` | One candidate/request invitation and hashed token | Future expiry; opaque token; one active claim; concurrency protected |

`AuditEntry` and `SupplierDiscoveryAuditEvent` are append-only operational evidence rather than behavior-rich aggregates.

## Cross-aggregate consistency

Offer acceptance modifies one RFQ and multiple offers. It is an application use case executed in one PostgreSQL transaction with concurrency protection. The transaction:

1. verifies buyer ownership and RFQ openness;
2. verifies selected offer status and validity;
3. accepts exactly one offer;
4. rejects competing submitted/revised offers;
5. moves the RFQ to `OfferSelected`;
6. appends audit and notification records.

## Commercial calculations

- Money uses `decimal`/`numeric`, never floating point.
- `line total = quantity supplied × unit price`.
- Offer subtotal is the sum of line totals.
- Total follows a documented rounding order using subtotal, transport, discount, and VAT.
- Completeness is quantity-weighted per requested line and capped at requested quantity.
- Unit conversions are not inferred. A different unit must be represented as an alternative and receives no automatic equivalence.
- Backend results are authoritative; frontend calculations are previews only.
