# Pasul 9A — UX audit and redesign scope

## Audit basis

This audit covers the live Angular routes and components for the Buyer and Provider procurement paths. The Forest Warm reference is the visual and interaction benchmark. Existing procurement rules, permissions, and API contracts are to be preserved.

## Critical path and gaps

| Journey surface | Current friction | Redesign outcome |
|---|---|---|
| Public home and sign-in | A public market page and account shell compete for attention; navigation is horizontal and task context is weak. | A responsive task-led workspace, with a clear route into the next meaningful task after sign-in. |
| Buyer dashboard | The dashboard is a workspace/activity page with multiple equal actions. It does not start with the user's need or make incomplete work prominent. | A welcoming home with a single natural-language entry point, clear active work, and contextual secondary cards. |
| Start and AI clarification | The initial input is separate from a dense draft editor. The assistant's work is useful but reads as a conversation beside a technical form. | A guided copilot flow: acknowledged understanding, one question at a time, an always-visible concise summary, auto-resume, and optional manual detail. |
| Request editor and preview | Internal terms, category mechanics, request-type choice, and commercial settings arrive early. The preview is not a dedicated human-readable confirmation step. | Progressive sections, an explicit review/confirmation moment, smart defaults, compact completed line summaries, and advanced details only on demand. |
| Provider matching | Relevant providers are represented through functional lists rather than explanatory recommendation cards. Match rationale and trust are not consistently first-class. | Warm provider cards with location, coverage, verified facts, compact trust, response behavior, and a clear “why recommended” explanation. |
| Published request and offers | Status is functional but varies between components and can expose implementation language. A Buyer does not always see the next action or continuity of work first. | Plain-language status panels, visible next step, resume actions, and contextual progress. |
| Offer comparison | The detailed comparison is capable but dense. Price, coverage, delivery, and trust compete with lower-priority commercial data. | Decision-first offer cards: total, coverage, delivery, transport and trust; advanced terms progressively disclosed; explanation rather than unexplained “best”. |
| Provider request and offer entry | The provider can offer from the detail screen, but request context and buyer trust are separated from the response action. The offer form has many fields at once. | A response-first layout with key need, location, deadline and buyer trust above the form; grouped commercial details, partial coverage and drafts clearly supported. |
| Profile, trust, notifications and empty/error states | Trust facts are present but their presentation and actionability vary. Empty/error/loading messages are not yet consistently instructional. | Compact contextual trust facts plus full explanation on profile; helpful empty, loading, error and success states that preserve confidence. |

## Cross-cutting findings

1. **Too much information early:** the advanced request editor exposes NeedType, project-template and category mechanics before a simple request is understood.
2. **Internal-model terminology:** “RFQ”, “matching”, “need type”, and category mechanics appear too close to normal Buyer tasks.
3. **Weak primary-action hierarchy:** dashboard actions are visually equal; there is no dominant “tell us what you need” path.
4. **Continuity:** drafts exist, but incomplete work is not surfaced as a confident “continue where you left off” experience.
5. **Inconsistent density:** card visual treatment and hard-coded styles differ substantially across routes; desktop horizontal navigation becomes constrained on mobile.
6. **Decision support:** trust and match rationale exist in data but are not consistently visible at the decision point.
7. **Low-tech/mobile friction:** complex forms and comparison tables need clearer grouping, larger touch targets and fewer simultaneous choices.

## Materially changed screens in this pass

1. Shared shell/navigation and Dashboard.
2. Buyer start, AI clarification, request editor/preview and offers comparison.
3. Provider open request and offer creation.
4. Shared trust, empty/loading/error/success states on the above screens.

## Explicitly deferred

- Administration redesign, analytics, new integrations, animation libraries, and a standalone design-system project.
- New business capabilities such as ratings, sponsored ranking, or automatic external supplier search.

These are deliberately outside the MVP UX hardening scope; the redesign focuses on making existing workflows clearer and more human.
