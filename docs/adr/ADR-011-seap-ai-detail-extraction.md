# ADR-011 — SEAP AI Detail Extraction (Sinteză AI + câmpuri de decizie)

## Decision

The Anunțuri SEAP tender detail page is fed by an **AI extraction step that runs at import time and is
never a gate** — `SeapSummaryService` (`ISeapSummaryGenerator`, shared `ILlmProvider`):

1. **Input**: the notice data the importer already holds — title, CPV codes/names, estimated value + currency,
   contracting authority, county, publication/deadline, and `Description` when present — plus the text of the
   published attachments (Fișa de Date / Caiet de Sarcini) **when the ingestion pipeline has already
   downloaded and processed them**. Today the SEAP ingestion downloads attachments as files but does not
   extract their text, so the summarizer receives `attachmentText = null` and the LLM works from the notice
   metadata alone; the optional parameter is the hook for a future OCR/extraction step.
2. **Output contract** (strict JSON, Romanian): `summaryPoints` (max 5 bullets, max 140 chars each),
   `description` (2–3 sentence acquisition summary), `awardCriterion`, `participationGuarantee`,
   `fundingSource`, `contractDuration`, `qualificationRequirements`, `keyPersonnel`. The prompt mandates
   `null` for any fact the notice does not state — the LLM must never invent data. The parser tolerates
   Markdown-wrapped output and degrades to null on unparseable output.
3. **Persistence**: additive migration `AddSeapAiDetailExtraction` on `SeapTender` —
   `SummaryPoints` as `text[]` (a flat list of short strings, matching the existing `CpvCodes`/`CpvNames`
   convention; JSONB was rejected — see alternatives), the detail fields as nullable `text`.
   `SeapTender.ApplyAiExtraction` stores them; the 2–3 sentence AI `description` fills `SeapTender.Description`
   **only when the notice's own full text is absent** (a parser-delivered full text always wins).
4. **Where it runs**: at the **background** import (`ImportNoticeAsync`), after categorization, persisted in
   the same `SaveChanges` as the tender. The on-demand „Caută în SEAP" search (`ImportSearchNoticeAsync`)
   deliberately **does not** call the summarizer synchronously: a live search can match up to
   `OnDemandSearchMaxMatches` notices, and one Claude/OpenAI call per notice inside the request thread made
   „Caută în SEAP" unacceptably slow. **On-demand fallback**: `POST /api/v1/seap/tenders/{id}/summary` generates
   the extraction lazily for tenders that still have none (legacy tenders imported before the summarizer
   existed, or tenders whose import-time generation failed). It is idempotent — an existing summary is
   returned without an LLM call — and a failed generation returns 503 with a friendly `detail`. The detail
   `GET` stays side-effect free. Enrichment is never a gate: any LLM/parse failure logs a warning and the
   tender stays fully usable without AI details (ADR-007 principle).
5. **Cost**: bounded by the existing caps — background ≤ `Seap:MaxNewNoticesPerHour` (default 5) per hour,
   on-demand ≤ `Seap:OnDemandSearchMaxMatches` (default 500, no LLM and no per-notice document resolution
   in the search path) per user-initiated search.
6. **Exposure**: `SeapTenderDetailsDto` maps the real values (`QualificationRequirements`/`KeyPersonnel` as
   single-element arrays); `Certificates` stays null (not part of the contract yet). The „Sinteză AI" card is
   an honest state machine — **never an endless fake-progress skeleton**: real bullets when the summary
   exists, a skeleton only while a genuine on-demand request is running, an actionable empty state
   („Generează sinteza AI") when the tender has none, and a recoverable error with a deliberate „Încearcă
   din nou" action when generation fails. No LLM call is auto-fired on page view.

## Motivation

The current notice-search payload (`GetCNoticeList`) carries no description, no award criterion, no
participation guarantee, no funding source, no duration and no qualification requirements — so the detail
page could only show elegant placeholders and the „Sinteză AI" tab had to fall back to clearly-labelled
mock bullets. A supplier deciding whether to bid needs exactly these facts (guarantee, experience, criterion,
deadlines). Generating them from the available notice data at import gives every tender a real, factual
summary with zero extra data plumbing, while the optional attachment-text hook grounds the extraction in the
full published documents as soon as an OCR/extraction step exists.

## Alternatives considered

- **Fetch the notice-detail endpoint from e-licitatie.ro** (`GetCNoticeDetails`-style): rejected for this
  phase — the endpoint is not documented in the repo, the SPA loads it via AJAX, and the golden payload
  contains no detail sample. A future phase can add `FetchNoticeDetailAsync` and feed the full text into the
  same summarizer (the `attachmentText`-equivalent input).
- **JSONB for `SummaryPoints`**: rejected — a flat list of short strings read whole needs no JSON object
  semantics, no partial updates, no inner queries; `text[]` is the existing array convention on
  `SeapTenders` and maps natively via Npgsql.
- **Skipping the on-demand search path**: rejected — most tenders enter the database through „Caută în SEAP",
  so skipping it would leave the primary surface without AI details; the `OnDemandSearchMaxMatches` cap keeps
  the cost and latency bounded.
- **On-demand (lazy) generation on the detail GET**: rejected — a GET must stay side-effect free. The
  on-demand path is a **dedicated `POST /summary`** (idempotent, 503 on failure) that the SPA triggers only
  on a deliberate user action.

## Consequences

- `SeapTender` gains `SummaryPoints` (`text[]`) plus `AwardCriterion`, `ParticipationGuarantee`,
  `FundingSource`, `ContractDuration`, `QualificationRequirements`, `KeyPersonnel` (nullable `text`);
  migration `AddSeapAiDetailExtraction` is additive.
- New `ISeapSummaryGenerator`/`SeapSummaryService` (registered `AddScoped`), invoked by
  `SeapIngestionService` in both import paths; failures degrade to null (never a gate).
- `SeapTenderDetailsDto.From` returns the real extracted values; `SeapTenderDetailsEndpointTests` now asserts
  the AI-extraction projection, and `SeapSummaryServiceTests` guard the JSON contract (valid output,
  Markdown-wrapped output, garbage/throwing LLM → null, bullet cap/truncation, prompt contents).
- The SPA no longer shows mock „Orientativ" bullets; it renders the real `summaryPoints` and detail rows.
  The summary card is an honest state machine (bullets / real skeleton while the on-demand request runs /
  actionable empty state / recoverable error with deliberate retry) — no endless fake-progress skeleton.
