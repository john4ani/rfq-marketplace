# Entity–Relationship Diagram

Status: Implemented through central category grouping, project templates, offers, messaging, and supplier discovery; later attachment/reporting entities remain proposed

```mermaid
erDiagram
    USERS ||--o{ REFRESH_TOKENS : owns
    USERS ||--o| USER_PRESENCES : reports
    USERS ||--o| BUYER_PROFILES : has
    USERS ||--o| SUPPLIER_PROFILES : has
    USERS ||--o{ USER_ROLES : assigned
    ROLES ||--o{ USER_ROLES : contains

    VERTICALS ||--|{ CATEGORIES : contains
    SUPPLIER_PROFILES ||--o{ SUPPLIER_CATEGORIES : subscribes
    CATEGORIES ||--o{ SUPPLIER_CATEGORIES : selected
    SUPPLIER_PROFILES ||--o{ SUPPLIER_DELIVERY_AREAS : serves
    SUPPLIER_PROFILES ||--o| SUPPLIER_DELIVERY_SETTINGS : configures
    CATEGORIES ||--o{ CATEGORIES : parent_of

    BUYER_PROFILES ||--o{ RFQS : creates
    RFQS ||--|{ PROCUREMENT_NEED_CATEGORIES : requires
    CATEGORIES ||--o{ PROCUREMENT_NEED_CATEGORIES : groups
    PROCUREMENT_NEED_CATEGORIES ||--o{ RFQ_ITEMS : contains
    VERTICALS ||--o{ PROJECT_TEMPLATES : offers
    PROJECT_TEMPLATES ||--|{ PROJECT_TEMPLATE_CATEGORIES : proposes
    CATEGORIES ||--o{ PROJECT_TEMPLATE_CATEGORIES : referenced_by
    PROJECT_TEMPLATES ||--o{ RFQS : optionally_starts

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
      string normalized_user_name UK
      string normalized_email
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
    USER_PRESENCES {
      uuid user_id PK,FK
      timestamptz last_seen_at
    }
    BUYER_PROFILES {
      uuid id PK,FK
      string display_name
      string buyer_type
      string county_code
      int market_radius_km
    }
    SUPPLIER_PROFILES {
      uuid id PK,FK
      string trading_name
      string approval_status
      boolean is_active
      decimal latitude
      decimal longitude
    }
    CATEGORIES {
      uuid id PK
      uuid vertical_id FK
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
      uuid supplier_id PK,FK
      string county PK
    }
    SUPPLIER_DELIVERY_SETTINGS {
      uuid supplier_id PK,FK
      int normal_radius_km
      int maximum_radius_km
      numeric minimum_order_value
      boolean nationwide_delivery
      timestamptz updated_at
    }
    RFQS {
      uuid id PK
      uuid buyer_id FK
      string need_type
      uuid project_template_id FK
      string status
      string delivery_address
      string delivery_locality
      string county
      timestamptz quotation_deadline
      date preferred_delivery_date
      numeric estimated_budget
      int supplier_search_radius_km
    }
    RFQ_ITEMS {
      uuid id PK
      uuid rfq_id FK
      uuid need_category_id FK
      int item_number
      string product_name
      numeric quantity
      string unit
    }
    PROCUREMENT_NEED_CATEGORIES {
      uuid id PK
      uuid procurement_need_id FK
      uuid category_id FK
      int sort_order
    }
    VERTICALS {
      uuid id PK
      string name
      string slug UK
      boolean is_active
    }
    PROJECT_TEMPLATES {
      uuid id PK
      uuid vertical_id FK
      string name
      string slug
      boolean is_active
    }
    PROJECT_TEMPLATE_CATEGORIES {
      uuid project_template_id FK
      uuid category_id FK
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
    CATEGORIES ||--o{ SUPPLIER_CANDIDATE_CATEGORIES : classifies
    SUPPLIER_CANDIDATES ||--|{ SUPPLIER_CANDIDATE_CATEGORIES : covers
    USERS ||--o{ SUPPLIER_CANDIDATES : creates
    SUPPLIER_CANDIDATES ||--o{ SUPPLIER_INVITATIONS : receives
    RFQS ||--o{ SUPPLIER_INVITATIONS : references
    SUPPLIER_PROFILES ||--o{ SUPPLIER_INVITATIONS : claims
    SUPPLIER_CANDIDATES ||--o{ SUPPLIER_DISCOVERY_AUDIT_EVENTS : records
    SUPPLIER_CANDIDATES {
      uuid id PK
      string normalized_company_name
      string county
      string source
      string status
      uuid claimed_by_supplier_id FK
      uint version
    }
    SUPPLIER_CANDIDATE_CATEGORIES {
      uuid candidate_id FK
      uuid category_id FK
    }
    SUPPLIER_INVITATIONS {
      uuid id PK
      uuid candidate_id FK
      uuid rfq_id FK
      string token_hash UK
      timestamptz expires_at
      uuid claimed_by_supplier_id FK
      uint version
    }
    SUPPLIER_DISCOVERY_AUDIT_EVENTS {
      uuid id PK
      uuid candidate_id FK
      uuid invitation_id FK
      uuid actor_user_id
      string event_type
      timestamptz occurred_at
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

- Unique `(supplier_id, category_id)`, `(supplier_id, county_code)`, and `(candidate_id, category_id)`.
- Unique `(procurement_need_id, category_id)`, `(procurement_need_id, sort_order)`, `(rfq_id, item_number)`, `(rfq_id, supplier_id)`, and `(offer_id, rfq_item_id)`.
- Unique `(offer_id, revision_number)` and one conversation per offer.
- Positive RFQ quantities; non-negative prices, transport, VAT, and discounts.
- Latitude in `[-90, 90]`; longitude in `[-180, 180]`.
- Completeness in `[0, 100]`; delivery radii are non-negative and maximum is not below normal.
- Money columns use `numeric(19,4)`. Display rounding is currency-specific and occurs at defined calculation boundaries.
- Index need discovery on status/deadline/county and category membership through `PROCUREMENT_NEED_CATEGORIES`.
- Index offer comparison on `(rfq_id, status)`.
- Index notification claiming on `(status, next_attempt_at)`.
- Index messages on `(conversation_id, created_at, id)`.
- Index audit records on `(entity_type, entity_id, occurred_at)`.
- Unique Supplier candidate `(normalized_company_name, county)`, unique invitation `token_hash`, and indexes on candidate status/category and invitation candidate/RFQ/expiry.

Attachments use a pragmatic polymorphic parent reference. The API centralizes parent authorization, and integration tests cover it because the database cannot provide a conventional foreign key to multiple parent tables.
