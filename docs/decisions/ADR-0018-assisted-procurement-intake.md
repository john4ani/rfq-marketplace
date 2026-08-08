# ADR-0018: Assisted procurement intake is a bounded draft adapter

- Status: accepted
- Date: 2026-08-04

## Context

Buyers often describe a need more naturally than they complete a long procurement form. Pasul 3 needs to convert that description into a useful, editable structure without letting a probabilistic model publish requests, invent technical bills of materials, or become a hard dependency of the established RFQ lifecycle.

## Decision

`/rfqs/new` begins with a natural-language question and keeps `/rfqs/new/advanced` as a permanent fallback. The backend persists an owner-scoped assisted-intake session containing the transcript, structured extraction, provider/model/prompt version, token usage and optional approved RFQ identifier.

The application owns one OpenAI-compatible extraction adapter. Direct OpenAI is the development and staging default; Azure OpenAI is selectable through configuration without changing the application or domain contracts. Both paths use GPT-5.6 Luna through the Responses API with strict JSON Schema output. The server resolves category slugs only against active local categories and maps the result into application-owned contracts.

The experience has four explicit stages: one high-impact clarification at a time, model-assisted analysis of the likely need, an editable proposed requirement with assumptions and uncertainties, and human confirmation. Live web search is not used in the MVP: it is unnecessary for ordinary clarification and would add latency, cost, external-data movement and source-quality complexity. The model must not invent sources, quantities, brands or standards; uncertain details remain visibly marked for Buyer or Provider verification.

Performance is part of the acceptance baseline. The public Angular shell must not wait for session recovery; only protected routes await the single bounded refresh attempt. The model adapter uses a reused `HttpClient`, HTTP/2 where supported, response-header streaming, low verbosity and no reasoning. Its strict schema contains only fields consumed by the MVP; unused web-research output is excluded. Provider duration and token counts are logged without user content so regressions can be measured rather than guessed.

Direct OpenAI currently processes the development/staging requests through its standard global endpoint. Requests set `store=false`; model prompts and completions are not retained as Responses API conversation state. Production is intended to select Azure OpenAI Data Zone Standard in the EU after quota approval. Until then, direct OpenAI is limited to development, synthetic tests and non-sensitive MVP validation; it must not be described as an EU-only processing path.

There is no local pseudo-AI path. When the model is unavailable or not configured, the UI goes directly to the established advanced form. If an outage happens after a session started, that persisted partial draft is opened in the advanced form and may be completed manually. Provider failures are logged without user text. Input is not stored by the provider (`store: false`), and the provider receives a one-way safety identifier rather than the Buyer ID.

Approval is explicit and idempotent. It creates the existing RFQ in `Draft` status with `Source=Assisted`; it never publishes. All existing RFQ validation, audit, editing and publication behavior remains authoritative.

## Consequences

- Model quality and hosting provider can evolve without changing the procurement domain.
- A provider outage removes the assisted journey but not request creation; the advanced form remains authoritative.
- The MVP does not show web citations. Model-derived suggestions are assumptions to verify, not sourced technical specifications.
- Token usage is measurable per session and supports cost monitoring.
- Variable cost consists only of provider model tokens; there is no web-search charge or additional always-on compute.
- The advanced form remains necessary for complex or privacy-sensitive requests.
- A future prompt/model change requires a new prompt version and regression examples.
