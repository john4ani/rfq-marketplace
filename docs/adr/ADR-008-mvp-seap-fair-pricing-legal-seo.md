# ADR-008 — MVP focus: Anunțuri SEAP only, fair-pricing, legal shield & SEO

## Decision

Effective 2026-08-27 the MVP is **Anunțuri SEAP-first**: every marketplace surface (live market, RFQs, offers,
profiles, messaging, notifications, supplier discovery) moves behind the live feature flag
`Features:MarketPlaceEnabled` (exposed as `MarketPlace/Enabled` in the UI layer) whose deploy-time value is
**false**. Only the standalone **Anunțuri SEAP** product and the **Administrator** workspace remain visible,
**plus the user's own account surfaces**: **Cont** (`/account`, profile + represented companies) and **Firma**
(`/account/company/*`, create/register and edit the companies the user represents) stay reachable because SEAP
auto-bidding needs the user's company data (Company Vault, categories, county). The SEAP list page shows an
"Înregistrează firma" CTA when the user has no company yet. The SEAP radius filter is sourced from the
company profile's `MarketRadiusKm` and is **editable in Cont → Firma → setările firmei** (Rază de interes
pentru anunțuri); saving it updates the SEAP list on the next load.
Additionally:

1. **Fair Pricing** replaces the 29/49/99 EUR tiers: document generation (.docx/.xlsx) is priced dynamically
   from the tender's `EstimatedValue` (SEAP, RON), converted to EUR with the configurable
   `AutoBidding:RonToEurRate` (default 5). Tiers per generation: <20k€ → 25€, 20k–100k€ → 75€,
   100k–500k€ → 300€, 500k–1M€ → 500€, 1M–5M€ → 1.000€, 5M–10M€ → 2.000€, 10M–15M€ → 3.000€,
   15M–20M€ → 4.000€, 20M–25M€ → 5.000€, 25M–30M€ → 6.000€, 30M–35M€ → 7.000€, 35M–40M€ → 8.000€,
   40M–45M€ → 9.000€, 45M–50M€ → 10.000€, ≥50M€ → 20.000€. **The first generation per account is
   free** (launch offer) — tracked by `ApplicationUser.FreeGenerationsRemaining` (default 1), consumed by the API.
2. **Legal shield**: the platform is positioned as an assisted-drafting software tool (Copilot), not legal or
   technical consulting. A public **Terms page** (`/terms`) contains the explicit Limitation of Liability clause.
   Before every document download the user must accept a responsibility checkbox ("Îmi asum responsabilitatea
   verificării ... înainte de semnarea electronică și depunerea în SEAP").
3. **SEO**: the public landing (`/`) targets purchase-intent keywords („redactare oferta tehnica SEAP",
   „model propunere tehnica constructii", „soft generare deviz caiet de sarcini", „consultanta achizitii publice
   AI") through static `index.html` meta tags (title/description/keywords/OG) and an SEO landing component that
   replaces the live market when `MarketPlaceEnabled=false`.

## Motivation

Anunțuri SEAP is the only surface with the shortest path to revenue. Hiding the rest of the marketplace behind
a flag lets the team ship SEAP quickly, keep Admin for internal operations, and re-enable the other surfaces
later without a code rollback. Fair pricing charges proportionally to the value of the opportunity (the higher
the tender value, the more the documents are worth) while the free first generation removes the onboarding
friction. The legal shield protects the platform by making the user's responsibility explicit before download.
SEO content is required so the public page can be found for the exact searches buyers perform.

## Alternatives considered

- **Hard-remove marketplace routes**: riskier (code rollback needed to re-enable) and would break tests/other
  surfaces; a live flag keeps both code paths.
- **Per-file free generation**: ambiguous; per-account entitlement (`FreeGenerationsRemaining`) is simpler to
  reason about and display.
- **Pricing in RON**: the tiers were specified in EUR; conversion happens at the API with a configurable rate.

## Consequences

- `FeaturesOptions` gains `MarketPlaceEnabled` (SSM `features-marketplace-enabled`, default false for Prod and
  Staging). The SPA reads it via `GET /api/v1/features`; navigation and routes hide marketplace surfaces and
  redirect to `/seap/tenders`. The account routes **stay open**: `/account`, `/account/company/new`,
  `/account/company/:companyId` keep only `authGuard` (no `marketplaceGuard`), and the buyer/company rails keep
  the **Cont** and **Firma** entries visible even when the flag is off.
- New additive migration `AddFreeGenerationsRemaining` (AspNetUsers, default 1).
- `PricingCalculator` semantics change from RON thresholds to EUR thresholds — the unit tests were updated to
  the new tiers.
- New endpoints: `GET /api/v1/features`, `GET /api/v1/seap/tenders/{id}/pricing`.
- The admin Caiet de sarcini test surface and all SEAP downloads require the responsibility checkbox.
- When the flag is flipped back on, the existing marketplace surfaces reappear unchanged; the SEO landing is
  still served at `/` for anonymous visitors.
