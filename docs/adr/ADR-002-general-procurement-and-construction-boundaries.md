# ADR-002 — General Procurement and Construction boundaries

## Decision

General Procurement and Construction Projects use separate AI/domain contracts: `IGeneralNeedInterpreter` and `IConstructionProjectInterpreter`. Construction Projects may generate one or more Procurement Needs.

## Consequences

Construction-specific vocabulary, facts, decomposition and safe calculations do not enter Generic Procurement. A construction-looking general request may receive a user-controlled suggestion to switch modes, never an automatic redirect.
