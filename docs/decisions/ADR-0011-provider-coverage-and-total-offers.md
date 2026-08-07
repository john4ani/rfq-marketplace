# ADR-0011: Provider coverage without a product catalogue

Status: Partially superseded by ADR-0022

Date: 2026-07-24

ADR-0022 supersedes the explicit multi-category capability flag and the rule that specialists are hidden whenever a full provider exists. Full coverage is now derived from the Provider covering every central category required by the need; partial coverage remains subject to the Buyer's split preference. The total-offer decisions below remain accepted.

## Context

Large Providers can source and deliver complete, multi-category requests without maintaining a Marketplace catalogue containing every material, product, or service they may supply. Requiring exhaustive catalogue entry during the MVP would make onboarding expensive, create stale data, and delay the request-to-offer workflow.

Selecting several categories is not sufficient proof that a Provider can coordinate those categories as one order. A Provider may serve each category independently or only accept specialist work.

## Decision

The MVP models supply capability rather than inventory:

- Providers select the categories they can supply.
- Providers explicitly choose whether they can fulfil complete multi-category requests.
- Delivery counties or nationwide delivery remain part of eligibility.
- A single-category request can be fully covered by any eligible Provider that selected that category.
- A multi-category request is fully covered only when an eligible Provider selected every required category and declared multi-category fulfilment.
- If a full-coverage Provider exists, the whole request is routed to those Providers.
- Specialists receive only their matching positions when the Buyer allowed splitting and no eligible full-coverage Provider exists.

Full-coverage Providers may submit either:

- a total-price offer for the complete request, without pricing every position; or
- an itemized offer with quantities, availability, unit prices, and delivery cost.

A total-price offer is explicitly marked as covering the complete request and includes delivery in the quoted total. Providers should use the notes field for stock conditions, substitutions, or exceptions. Partial/specialist matches cannot submit a whole-request total-price offer.

## Consequences

- Provider onboarding remains category-based and does not require a product catalogue.
- Buyers can distinguish whole-request totals from itemized offers.
- Category selection and multi-category fulfilment are separate concepts.
- The system does not claim real-time stock availability.
- Catalogue import, stock synchronization, ERP integration, and offer attachments remain future enhancements.

## Acceptance rules

1. Multi-category fulfilment is off by default.
2. Existing Providers remain specialists until they explicitly enable it.
3. Whole-request matching requires category coverage, delivery eligibility, and the multi-category capability.
4. Buyer split preference remains authoritative.
5. Total-price offers require a positive total and are available only for full matches.
6. Itemized pricing remains available for both full and partial matches.
