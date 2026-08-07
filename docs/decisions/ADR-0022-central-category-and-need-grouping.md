# ADR-0022 — Central categories and category-grouped procurement needs

Status: Accepted  
Date: 2026-08-07

## Context

Providers, procurement needs, project shortcuts, and supplier discovery must match through the same market vocabulary. Treating a project as a category, or making category cardinality depend on the need type, creates duplicate taxonomies and prevents reliable full/partial coverage matching.

## Decision

- `Vertical` owns the central `Category` taxonomy. Construction is the only active MVP vertical.
- A `Provider` and a discovered `SupplierCandidate` may cover multiple central categories.
- Every `ProcurementNeed` contains one or more `NeedCategory` groups. Each requirement line belongs to exactly one of those groups.
- `NeedType` (`DirectPurchase`, `ShoppingList`, or `Project`) describes the buyer workflow and does not determine whether the need has one or several categories.
- `ProjectTemplate` is separate from `Category` and references several central categories. Choosing one only initializes an editable need.
- Matching compares provider/candidate coverage with all required need categories. Complete coverage is preferred; partial coverage is allowed only when the buyer permits splitting.
- Category creation is an Administrator operation in one central catalog. Provider settings, need entry, and discovery only select from that catalog.

## Consequences

- One category identifier has the same meaning everywhere in the product.
- Large providers match an entire need by selecting all categories they cover; no product catalogue or special flag is required.
- Buyers can express one or many markets for any need type, while every detailed line remains routable.
- The migration converts legacy RFQ and supplier-candidate category data without discarding it.
