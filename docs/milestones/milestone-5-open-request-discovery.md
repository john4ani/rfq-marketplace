# Milestone 5 — Open Request Discovery

Status: First slice ready for review

## Outcome

A signed-in Supplier can open **Cereri deschise / Open Requests**, see active published RFQs matched to the supplier's selected product categories and delivery counties, filter the list, and inspect a read-only request detail.

## Matching rules

- The RFQ must be `Published` and its quotation deadline must be in the future.
- Its primary category must be selected by the active Supplier.
- Its county must be configured as a delivery county, unless nationwide delivery is enabled.
- If delivery settings have not yet been saved, the Supplier profile county is used as a conservative fallback.
- Search matches the title, project name, delivery locality, or material name.

## Privacy boundary

The Supplier response includes the Buyer display/company name, delivery locality and county, deadline, budget, description, and material lines. It deliberately excludes the exact delivery address, coordinates, phone number, tax identifier, and private RFQ-level Buyer notes.

## API

```text
GET /api/v1/open-rfqs
GET /api/v1/open-rfqs/{rfqId}
```

Optional list query parameters:

- `search`
- `categoryId`

## Next slice

Supplier offer drafts, submission/withdrawal, Buyer comparison, and award/closure remain the next Milestone 5 slice.
