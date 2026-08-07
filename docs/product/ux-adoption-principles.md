# CompaSpot UX and Adoption Principles

Status: MVP Step 9A acceptance baseline; governing standard for all later user-facing work
Source: Product addendum supplied 2026-08-07

## Product promise

CompaSpot helps people complete a procurement task with minimum cognitive effort, uncertainty, and friction. It must feel familiar, calm, trustworthy, guided, forgiving, and usable without training by people who often prefer phone, WhatsApp, or a copied list over complex software.

Users understand their need, not our internal concepts. The product absorbs the complexity of RFQs, categories, matching, units, and discovery. User-facing language must describe the practical task: for example, “Your request was sent to relevant suppliers,” not “RFQ successfully published.”

## Step 9A scope

Apply the principles first to the critical journey:

```text
Start → describe need → AI clarification → request preview → supplier matching
      → publish → offers → compare → outcome
```

Step 9A is an incremental improvement pass, not a wholesale redesign. Secondary administration screens may remain utilitarian while the main buyer/provider journey is made easier.

## Visual direction: Forest Warm

**Forest Warm** is the official MVP visual direction. CompaSpot should feel warm, premium, trustworthy, calm, modern, simple, and human: closer to high-quality hospitality than a cold enterprise dashboard. Premium comes through restraint, not decoration.

Use these central design tokens rather than scattered hard-coded colours:

| Token | Value | Use |
|---|---:|---|
| Forest Green | `#1E3D2F` | primary actions, active navigation, verified emphasis |
| Forest Light | `#2F6B4F` | supporting interactive states |
| Warm White | `#FAF7F2` | dominant background |
| Sand | `#F2E9D8` | secondary surfaces, AI/helpful grouping |
| Dark Text | `#1F2124` | primary readable text |
| Warm Amber | `#E0A64E` | sparse positive highlights only |

Use Inter (Regular/Medium body, SemiBold headings), generous whitespace, clear hierarchy, rounded cards, subtle borders, very soft shadows, predictable alignment, readable inputs, and obvious primary actions. Do not make every screen dark green, overuse amber, add generic AI purple/gradients, imitate a crowded ERP, or use colour without an icon and label.

Apply Forest Warm first to the critical journey and its trust, Provider-card, navigation, notification, and status surfaces. Keep administration/internal pages utilitarian until the main path is complete. Centralize visual tokens and keep components themeable so future branding does not require business-component rewrites.

## Required experience patterns

- Show useful understanding within seconds, before demanding registration, taxonomy choices, or a long form.
- Let AI act as a procurement copilot: ask one material clarification at a time and avoid questions already answered by an experienced user.
- Reveal advanced commercial detail only when needed. Keep basic decisions—price, delivery, transport, coverage—easy to scan.
- Use visible, editable defaults based on known context. Never silently infer something that could materially change a commercial outcome.
- Reassure users: nothing is sent until confirmation; drafts can be changed; uncertainty is stated honestly; important actions require plain-language confirmation.
- Preserve incomplete work and provide a clear way to resume without repeating information.
- Work with existing habits: copy/paste, shared links, manual supplier invitations, and WhatsApp-friendly sharing. Do not frame the product as replacing these tools.
- End significant flows with a clear, factual sense of completion and the next useful expectation.
- Offer next-step assistance only when relevant, explainable, dismissible, and clearly separate from any future paid placement.

## Integrity constraints

CompaSpot does not use fake urgency or scarcity, misleading confirmations, hidden opt-ins, preselected paid choices, notification spam, engagement loops without user benefit, compulsive streaks, spam-oriented referrals, or undisclosed sponsored ranking. AI must not pretend certainty. The product must never publish automatically without human confirmation.

Trust indicators, when introduced, must represent observable facts such as verification or completed collaborations—not arbitrary popularity points or gamification.

For the Step 9A visual pass, render trust facts as individually readable rows with a meaningful icon/label (for example, a check for verified), not as a dense inline sentence. Do not prefix neutral facts such as membership duration with a generic bullet glyph. Keep the fact, its source category, and any explanation easy to scan without making the card feel like a status dashboard.

## Decision checks for future work

Before adding an interaction, answer:

1. Does it reduce the user's cognitive effort, uncertainty, or work?
2. Does it primarily benefit the user rather than an internal engagement metric?
3. Does it preserve trust, explain uncertainty, and leave the user in control?

If the answer is no, challenge the interaction before implementing it.

## UX measures

Prioritise time to first useful AI response, time to first draft, time to publish, clarification count, abandonment, saved-draft resumption, repeat requests, completed procurements, and completion satisfaction. Do not optimise primarily for session duration, screens visited, or time spent.

The central MVP signal is whether a non-technical person can create and publish a real request without founder assistance.
