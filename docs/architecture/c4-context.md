# C4 Level 1 — System Context

Status: Proposed

```mermaid
flowchart LR
    buyer["Buyer<br/>Creates RFQs, compares offers,<br/>messages suppliers, selects an offer"]
    supplier["Supplier<br/>Finds relevant RFQs, submits offers,<br/>answers questions"]
    admin["Administrator<br/>Approves suppliers, manages categories,<br/>inspects activity and metrics"]

    marketplace["RFQ Marketplace<br/>Web marketplace for construction-material<br/>requests and supplier quotations"]

    email["Amazon SES<br/>Transactional email delivery"]
    storage["Amazon S3<br/>Private attachment storage"]
    geocoder["Geocoding provider (optional)<br/>Converts locality/address to coordinates"]

    buyer -->|"Uses over HTTPS"| marketplace
    supplier -->|"Uses over HTTPS"| marketplace
    admin -->|"Uses over HTTPS"| marketplace
    marketplace -->|"Sends transactional email"| email
    marketplace -->|"Stores and retrieves private files"| storage
    marketplace -.->|"Optional geocoding request"| geocoder
```

## System responsibilities

The RFQ Marketplace owns:

- account registration, verification, authentication, and authorization;
- buyer and supplier profiles;
- supplier approval and service-area configuration;
- RFQ drafting, publication, and expiry;
- deterministic supplier relevance;
- offer entry, revision, comparison, and acceptance;
- private buyer–supplier conversations;
- attachment authorization and metadata;
- durable notifications and audit history;
- administrative inspection and MVP metrics.

## Trust boundaries

- Browser input is untrusted and every command is revalidated by the API.
- Supplier-visible RFQ projections omit unnecessary buyer-sensitive data.
- Offer and conversation access is resource-authorized, not merely role-authorized.
- AWS services are external systems accessed with least-privilege credentials.
- The optional geocoder receives only the minimum location data needed.

## Out of scope

Payments, contracts, invoicing, inventory, delivery tracking, public competing prices, machine learning, native mobile apps, microservices, and real-time bidding are outside the MVP boundary.
