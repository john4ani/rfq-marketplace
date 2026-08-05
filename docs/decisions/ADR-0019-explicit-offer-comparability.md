# ADR-0019: Explicit offer comparability

Status: Accepted

Date: 2026-08-05

## Context

An offer total is misleading when it does not say which positions are covered, whether a quoted item is an alternative, how VAT is treated, or whether transport is part of the amount. Sorting such offers by a single number can make an incomplete offer appear preferable to a complete one.

The existing offer aggregate already supports itemized and whole-request pricing and must remain backward compatible. The current RFQ lifecycle selects one winning offer; it does not yet support several simultaneous awards.

## Decision

- Every new submitted offer explicitly records `Full` or `Partial` coverage.
- An itemized offer is full only when every requested position is available in at least the requested quantity. The API validates the declaration against the RFQ.
- Partial offers are accepted only when the Buyer allowed splitting, but cannot close the current single-winner RFQ by themselves. They remain visible for comparison and clarification. Multi-award fulfillment requires a separate lifecycle decision rather than overloading `SelectedOfferId`.
- Alternative lines are marked explicitly and require a concrete offered-product description. The API rejects alternatives where the Buyer disallowed them.
- Tax treatment is one of VAT included, VAT excluded, not applicable, or legacy unspecified. New submissions cannot remain unspecified. The API exposes net, VAT, and payable totals.
- Delivery terms distinguish included in price, added to total, Buyer pickup, and arranged separately. A total is commercially comparable only when tax is explicit and transport is not left to later negotiation.
- Buyer results put complete and commercially comparable offers first, then price. The UI explains coverage, delivery, alternatives, and term trade-offs; it does not call the lowest price the best offer.
- Existing rows are upgraded additively. Their previous delivery behavior is preserved, partial itemized offers are inferred from missing quantities, and tax remains visibly unspecified until the Provider revises the offer.

## Consequences

- Buyers can compare the same commercial concepts and inspect every requested line.
- Providers see an explicit coverage result before submission and clear tax/transport controls.
- Legacy offers remain readable without inventing a tax assertion.
- Provider verification is not fabricated here; verified company state remains part of Pasul 8.
- Selecting combinations of partial offers remains a later explicit multi-award design.

## Acceptance rules

1. Unavailable, absent, or short-quantity lines make an itemized offer partial.
2. Partial offers cannot be submitted when the Buyer requires one Provider.
3. Total-price offers are full and include transport in their price.
4. VAT calculations are deterministic to two decimals and the payable total is returned by the API.
5. Unknown transport prevents a total from being labelled comparable.
6. The Buyer comparison includes a position-by-position matrix and concrete trade-off text.
