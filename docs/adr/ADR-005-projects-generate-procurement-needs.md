# ADR-005 — Projects generate Procurement Needs

## Decision

Construction Projects are not categories. A project has specialised facts and work packages and can produce one or more shared Procurement Needs.

Construction mode is explicit. The legacy `NeedType.Project` value on the compatible `Rfq`/Procurement Need shape represents a generic project-like need only; it must not select or infer the Construction bounded context.

## Consequences

Project templates may propose central categories, but need category cardinality is independent of need type. Matching, discovery, offers, comparison and outcome operate on generated/shared needs.
