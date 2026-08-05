# Business scenario 1 — Small wood and derivatives supplier

## Business profile

An established small local supplier sells timber and derived wood products to loyal customers. Much of the current work arrives verbally or on paper. The first useful digital outcome is a fast, trustworthy order calculation that keeps quantities, unit prices, transport, VAT, and the payable total together.

## Representative marketplace journey

- Provider: `Depozitul de Lemn Marginea`, serving Suceava in `Lemn și plăci`.
- Buyer need: `2.4 m³` dried softwood timber and `12` exterior plywood sheets.
- Provider quote: `1,200 lei/m³`, `75 lei/sheet`, `250 lei` transport, VAT added at `19%`.
- Expected amounts: positions `3,780.00 lei`; net including transport `4,030.00 lei`; VAT `765.70 lei`; payable total `4,795.70 lei`.

## Verified on 2026-08-05

The reusable integration scenario proves registration, Provider profile/category/delivery configuration, request publication, matching, itemized calculation, offer submission, Buyer visibility, full coverage, and commercial comparability.

The real browser journey also proved that the Provider can discover the matched request, switch to itemized pricing, see Romanian unit labels, line subtotals, and the live commercial total, submit the offer, and find it under `Contul meu → Ofertele mele` with the correct awaiting-decision state. A separate Buyer/Provider browser exchange confirmed that every chat message identifies both its author and role from either participant's viewpoint.

## Product gap: offline customer orders

The current flow starts with a registered Buyer publishing a request. It does **not** yet let a Provider capture a loyal customer's phone, counter, or paper order directly. Therefore the marketplace quotation path works, but the stated highest-value paper-replacement workflow is not yet complete.

A separate vertical slice should add a Provider-owned `Ofertă rapidă / Comandă client` workspace with customer name or reference, editable lines, quantity/unit price, transport, VAT, totals, draft/final status, and a printable/shareable summary. It must not create a public RFQ or pretend that the offline customer is a registered Buyer. Product catalogue, inventory, invoicing, and accounting integration remain later capabilities unless pilot evidence makes one essential.

## Acceptance criteria for that slice

1. A Provider can start the order from its activity workspace without creating or switching to a Buyer profile.
2. A customer reference is enough; email and account registration are optional.
3. Each line shows quantity, unit, unit price, and calculated subtotal; the document shows transport, VAT, net, and payable total.
4. The Provider can save and reopen a draft, then mark the quote/order final.
5. A final summary can be printed or shared without exposing marketplace-private data.
6. The workflow is visibly separate from public requests and marketplace offers.
