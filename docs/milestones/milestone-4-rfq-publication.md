# Milestone 4 — RFQ Creation and Publication

Status: Complete

## Outcome

A signed-in Buyer can create and edit an RFQ draft, enter Romanian delivery details, add material rows manually or paste them from a spreadsheet, and publish the request when it is complete.

## Implemented scope

- `Rfq` aggregate with owned `RfqItem` rows.
- States: `Draft`, `Published`, and `Cancelled`.
- Buyer ownership enforced on every RFQ endpoint; another identifier returns `404`.
- Individual material lines are optional and appear in the editor only when requested; publication requires a future quotation deadline.
- Published requests cannot be edited.
- Buyer endpoints for list, create, read, update, publish, and cancel.
- “Cererile mele” dashboard card and a mobile-friendly Romanian editor.
- Buyer market radius defaults to 30 km and is copied into a new RFQ as its supplier-search radius.
- Authenticated Piața Live uses the active profile location: same city first, then other localities in the same county.
- Tab- or semicolon-separated material paste with row-specific feedback.
- PostgreSQL migration, domain tests, API integration coverage, and Angular service tests.

## API

```text
GET    /api/v1/rfqs/mine
POST   /api/v1/rfqs
GET    /api/v1/rfqs/{rfqId}
PUT    /api/v1/rfqs/{rfqId}
POST   /api/v1/rfqs/{rfqId}/publish
POST   /api/v1/rfqs/{rfqId}/cancel
```

## Explicit assumptions

- One RFQ has one primary product category in the MVP; individual material lines are optional.
- Kilometre values are stored now, but locality matching is county-based until profile coordinates are reliable.
- Currency is Romanian leu; the optional budget is stored as a decimal amount without a separate currency column.
- Spreadsheet parsing happens locally in the browser; the API validates the resulting structured rows.
- Published RFQ discovery for Suppliers and offer submission belong to the next milestone.
- Attachments, automatic unit conversion, templates, and duplicate-RFQ actions are outside this milestone.
