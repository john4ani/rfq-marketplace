# ADR-007 — SEAP categorization (CPV → CompaSpot category) and county-based filtering

## Decision

Anunțuri SEAP are **enriched at import time** and filtered by the supplier's company profile:

1. **CPV label is kept.** The parser retains both the code and the human-readable name
   (`SeapTender.CpvCodes` + `SeapTender.CpvNames`, e.g. `45342000-6` + "Montare de garduri").
2. **County is derived deterministically, without AI.** `CountyParser` matches the contracting-authority
   name against a 42-county alias table (county names + residence cities, diacritics-insensitive,
   last-match-wins), e.g. "... VASLUI" → Vaslui, "MUNICIPIUL CLUJ-NAPOCA" → Cluj, "COMUNA GIURGIU" →
   Giurgiu. A name that matches nothing yields `null`.
3. **The AI categorizer maps the CPV label to a CompaSpot category.** `SeapCategorizerService` uses the
   existing `ILlmProvider`; the persistent `CpvCategoryMappings` (CpvCode → CategoryId) cache makes the
   LLM cost per standardized CPV code ≈ one call (CPV codes are standardized, so the same code always
   maps to the same category). Categories the LLM returns that do not exist yet are **auto-created under
   the "Achiziții generale" root** (`GeneralProcurementRootId`), never inside the Construction domain.
4. **Categorization is enrichment, never a gate.** Any failure (LLM down, unparseable output, unknown
   geo alias) leaves the tender imported with `CategoryId`/`County = null`.
5. **Filtering.** The AI-mapped `CategoryId` is authoritative; the `Category.CpvPrefixes` prefix match
   remains the fallback for uncategorized tenders. `MarketRadiusKm` is measured **county-seat-to-county-
   seat** (`CountyDistance`, 42 seats, great-circle) from the supplier profile's county; tenders with
   `County = null` are excluded **only while a radius filter is explicitly active**.

## Motivation

SEAP payloads expose no structured location and no category — only `CpvCodeAndName` ("45342000-6 -
Montare de garduri (Rev.2)") and the contracting authority ("17095927 - DIRECTIA ... VASLUI"). Yet both
are recoverable: the authority name ends with the county seat/county, and the CPV label is enough for the
LLM to pick a CompaSpot category. This makes "Anunțuri SEAP filtrate după categoriile firmei + raza de
interes" possible without inventing a SEAP location feed.

## Alternatives considered

- **Radius from coordinates:** no coordinates exist in the payload; county-seat distance is an honest
  approximation and documented as such.
- **Categorize on every list query:** wasteful and non-deterministic; the import-time + persistent-cache
  approach costs one LLM call per unique CPV code.
- **CPV-prefix matching only:** kept as the fallback, but too coarse for categories like "Servicii
  specializate" or "Închiriere utilaje"; the LLM resolves the exact CompaSpot category.

## Consequences

- `SeapTender` gains `CpvNames`, `County`, `CategoryId` (nullable FK → `Categories`); `CpvCategoryMappings`
  is a new persistent cache table. Both migrations are additive.
- Auto-created categories live under General Procurement and appear in the shared category catalog; they
  are never routed into Construction.
- A tender in a county that is not a county seat (e.g. Petroșani) has `County = null` and is excluded only
  when the radius filter is on — documented, testable behavior.
- LLM cost stays bounded: import is already capped at `Seap:MaxNewNoticesPerHour` (5/hour) and the cache
  prevents repeat calls for the same CPV code.
