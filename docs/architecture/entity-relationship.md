# Entity–Relationship Diagram

Status: Proposed

```mermaid
erDiagram
    USERS ||--o{ REFRESH_TOKENS : owns
    USERS ||--o| BUYER_PROFILES : has
    USERS ||--o| SUPPLIER_PROFILES : has
    USERS ||--o{ USER_ROLES : assigned
    ROLES ||--o{ USER_ROLES : contains

    SUPPLIER_PROFILES ||--o{ SUPPLIER_CATEGORIES : subscribes
    CATEGORIES ||--o{ SUPPLIER_CATEGORIES : selected
    SUPPLIER_PROFILES ||--o{ SUPPLIER_DELIVERY_AREAS : serves
    CATEGORIES ||--o{ CATEGORIES : parent_of

    BUYER_PROFILES ||--o{ RFQS : creates
    CATEGORIES ||--o{ RFQS : primary_category
    RFQS ||--|{ RFQ_ITEMS : contains
    RFQS ||--o{ RFQ_CATEGORIES : classified
    CATEGORIES ||--o{ RFQ_CATEGORIES : relates

    RFQS ||--o{ OFFERS : receives
    SUPPLIER_PROFILES ||--o{ OFFERS : submits
    OFFERS ||--|{ OFFER_ITEMS : contains
    RFQ_ITEMS ||--o{ OFFER_ITEMS : answered_by
    OFFERS ||--o{ OFFER_REVISIONS : records

    OFFERS ||--o| CONVERSATIONS : owns
    CONVERSATIONS ||--o{ CONVERSATION_PARTICIPANTS : includes
    USERS ||--o{ CONVERSATION_PARTICIPANTS : participates
    CONVERSATIONS ||--o{ MESSAGES : contains
    USERS ||--o{ MESSAGES : sends

    USERS ||--o{ ATTACHMENTS : uploads
    USERS ||--o{ NOTIFICATIONS : receives
    USERS ||--o{ AUDIT_ENTRIES : acts
    USERS ||--o{ CONTENT_REPORTS : reports

    USERS {
      uuid id PK
      string normalized_email UK
      string status
      timestamptz created_at
    }
    REFRESH_TOKENS {
      uuid id PK
      uuid user_id FK
      string token_hash UK
      uuid family_id
      timestamptz expires_at
      timestamptz revoked_at
    }
    BUYER_PROFILES {
      uuid id PK
      uuid user_id FK,UK
      string display_name
      string buyer_type
      string county_code
    }
    SUPPLIER_PROFILES {
      uuid id PK
      uuid user_id FK,UK
      string trading_name
      string approval_status
      boolean is_active
      decimal latitude
      decimal longitude
      decimal normal_radius_km
      decimal maximum_radius_km
    }
    CATEGORIES {
      uuid id PK
      uuid parent_id FK
      string name
      string slug UK
      boolean is_active
    }
    SUPPLIER_CATEGORIES {
      uuid supplier_id FK
      uuid category_id FK
    }
    SUPPLIER_DELIVERY_AREAS {
      uuid id PK
      uuid supplier_id FK
      string county_code
    }
    RFQS {
      uuid id PK
      uuid buyer_id FK
      uuid primary_category_id FK
      string status
      string delivery_county_code
      timestamptz quotation_deadline
      int version
    }
    RFQ_CATEGORIES {
      uuid rfq_id FK
      uuid category_id FK
    }
    RFQ_ITEMS {
      uuid id PK
      uuid rfq_id FK
      int item_number
      string product_name
      numeric quantity
      string unit
    }
    OFFERS {
      uuid id PK
      uuid rfq_id FK
      uuid supplier_id FK
      string status
      numeric subtotal
      numeric transport_cost
      numeric discount
      numeric vat_amount
      numeric total
      string currency
      numeric completeness
      int version
    }
    OFFER_ITEMS {
      uuid id PK
      uuid offer_id FK
      uuid rfq_item_id FK
      numeric quantity_supplied
      numeric unit_price
      numeric line_total
      string availability
      boolean is_alternative
    }
    OFFER_REVISIONS {
      uuid id PK
      uuid offer_id FK
      int revision_number
      jsonb commercial_snapshot
      timestamptz created_at
    }
    CONVERSATIONS {
      uuid id PK
      uuid offer_id FK,UK
      uuid buyer_user_id FK
      uuid supplier_user_id FK
    }
    CONVERSATION_PARTICIPANTS {
      uuid conversation_id FK
      uuid user_id FK
      timestamptz last_read_at
    }
    MESSAGES {
      uuid id PK
      uuid conversation_id FK
      uuid sender_user_id FK
      string body
      timestamptz created_at
    }
    ATTACHMENTS {
      uuid id PK
      uuid uploader_user_id FK
      string parent_type
      uuid parent_id
      string storage_key UK
      string mime_type
      bigint file_size
      string status
    }
    NOTIFICATIONS {
      uuid id PK
      uuid recipient_user_id FK
      string type
      string status
      jsonb payload
      int attempts
      timestamptz next_attempt_at
    }
    AUDIT_ENTRIES {
      uuid id PK
      uuid actor_user_id FK
      string action
      string entity_type
      uuid entity_id
      jsonb metadata
      timestamptz occurred_at
    }
    CONTENT_REPORTS {
      uuid id PK
      uuid reporter_user_id FK
      string target_type
      uuid target_id
      string status
    }
```

## Required constraints and indexes

- Unique `(supplier_id, category_id)`, `(supplier_id, county_code)`, `(rfq_id, category_id)`.
- Unique `(rfq_id, item_number)`, `(rfq_id, supplier_id)`, `(offer_id, rfq_item_id)`.
- Unique `(offer_id, revision_number)` and one conversation per offer.
- Positive RFQ quantities; non-negative prices, transport, VAT, and discounts.
- Latitude in `[-90, 90]`; longitude in `[-180, 180]`.
- Completeness in `[0, 100]`; delivery radii are non-negative and maximum is not below normal.
- Money columns use `numeric(19,4)`. Display rounding is currency-specific and occurs at defined calculation boundaries.
- Index RFQ discovery on status, deadline, category, and county.
- Index offer comparison on `(rfq_id, status)`.
- Index notification claiming on `(status, next_attempt_at)`.
- Index messages on `(conversation_id, created_at, id)`.
- Index audit records on `(entity_type, entity_id, occurred_at)`.

Attachments use a pragmatic polymorphic parent reference. The API centralizes parent authorization, and integration tests cover it because the database cannot provide a conventional foreign key to multiple parent tables.
