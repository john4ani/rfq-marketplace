# ADR-009 — SEAP Tender Match (potrivire de licitații prin chat AI)

## Decision

Anunțuri SEAP gains a third, dedicated interpretation workflow — **SEAP Tender Match** — that translates a
free-text description into rigid database filter parameters (CPV prefixes + counties) and persists them on
the represented company profile:

1. **A third distinct interpreter contract**, `ISeapTenderMatchInterpreter` → `OpenAiSeapTenderMatchInterpreter`,
   following ADR-002: separate contract, prompt, JSON schema and clarification semantics — never a shared
   universal workflow with a swapped prompt. It reuses the single `OpenAiResponsesTransport` (structured
   output via JSON schema), like General and Construction.
2. **Structured output contract** (JSON schema, `additionalProperties:false`):
   `clarificationNeeded`, `clarificationQuestion`, `extractedCpvPrefixes`, `extractedCountyCodes`,
   `summaryText`. When the description is too vague the interpreter asks **exactly one** clarification at a
   time; when it is ready it returns the rigid parameters.
3. **Deterministic normalization, never trusted LLM values**: `CpvPrefix.TryNormalize` extracts the digit
   CPV prefix (≤ 8 digits, e.g. `45233000-1` → `45233000`); `RomanianCountyCodes.TryNormalize` maps county
   codes/names **and residence-city aliases** to the canonical county names the database stores (e.g.
   `IS` → `Iași`, `Cluj-Napoca` → `Cluj`, `Târgu-Mureș` → `Mureș`). The alias vocabulary is shared with
   `CountyParser` (`RomanianCountyCodes.CountyAliases` is the single source of truth), so the chat filter
   and the tender-import county always agree. The LLM can never invent a county or prefix that the filter
   would not match.
4. **Intent detection in the General interpreter**: `SuggestedProcurementMode` gains `SeapTenderSearch`;
   the General prompt detects SEAP-matching intent and returns a non-binding hint, so the General chat may
   suggest the SEAP match without auto-redirecting or converting the request.
5. **Persistence on the company profile**: `SupplierProfile` gains additive columns
   `SeapCpvPrefixes`/`SeapCounties` (`text[]`), saved through `SeapTenderMatchService.SaveFiltersAsync`
   (authorized by `CanRepresentCompany`). The Anunțuri SEAP list reads them as defaults and filters
   deterministically.
6. **Deterministic list filtering**: `BiddingGenerationService.ListTendersAsync` accepts optional
   `cpvPrefixes`/`counties`; CPV prefixes match the tender's own `CpvCodes` (prefix `LIKE ANY`, same
   NpgsqlParameter pattern as the category fallback) and apply to categorized and uncategorized tenders
   alike; counties match the canonical `SeapTender.County`. Both apply only when non-empty and compose with
   the existing category/radius filters.
7. **Stateless chat, no `AiIntakeSession`**: SEAP Tender Match does not produce a Procurement Need, so it
   deliberately does not reuse `AiIntakeSession` (which is tied to `Rfq` approval). The chat is stateless —
   the request carries the accumulated clarification turns — and only the confirmed filters are persisted.

## Motivation

Anunțuri SEAP is the shortest path to revenue and the default MVP surface (ADR-008), but configuring the
filter profile (CPV codes + counties) is expert-heavy. A conversational entry ("Descrie ce licitații cauți")
lets a consultant or provider describe the opportunity in natural language and receive a confirmable,
editable filter card before the feed is filtered. Keeping the interpretation and the filtering deterministic
avoids non-reproducible AI filtering.

## Alternatives considered

- **Central strategy/enum router "Interpreter AI"**: rejected — ADR-002 and `docs/domain-model.md`
  explicitly forbid one universal workflow with a swapped prompt; the third branch follows the established
  distinct-workflow pattern and detection stays a non-binding hint.
- **Reusing `AiIntakeSession` with a new kind**: rejected — the session lifecycle is coupled to Rfq
  approval (`ApprovedRfqId`, `ReadyForApproval`); a SEAP match has no Procurement Need.
- **Claude/`ILlmProvider` for the match**: rejected for the interpreter — the intake family already uses
  OpenAI Responses structured output; introducing a second interpreter transport would violate the
  one-shared-transport rule (ADR-002 §14). Claude remains the SEAP categorization/auto-bid LLM.
- **Trusting LLM county codes directly**: rejected — the DB stores canonical names; the deterministic
  `RomanianCountyCodes` mapping guarantees filter correctness.

## Consequences

- `SupplierProfile` gains `SeapCpvPrefixes`/`SeapCounties` (additive `text[]` columns, migration
  `AddSeapTenderMatchProfile`); `GET /api/v1/suppliers/{id}` exposes them.
- New endpoints: `GET /api/v1/seap/tender-match/availability`, `POST /api/v1/seap/tender-match`,
  `PUT /api/v1/seap/tender-match/profile`.
- `GET /api/v1/seap/tenders` accepts `cpvPrefixes` and `counties` query parameters.
- The Anunțuri SEAP feed shows an "Editare avansată filtre" chip editor (CPV + județe) persisted on the
  company profile, and a chat entry (`/seap/match`) that transitions to the filtered feed on confirmation.
- The General chat may suggest `SeapTenderSearch` (transfers the description via `?description=`, no
  auto-redirect), mirroring the Construction suggestion.
- LLM cost stays bounded: one structured-output call per chat turn, no web search, no per-tender AI work.
