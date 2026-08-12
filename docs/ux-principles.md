# CompaSpot UX principles

These principles govern all new user-facing work.

## Easy experience

- One dominant action per screen; minimal choices; progressive disclosure.
- Users see practical Romanian language, not internal terms such as RFQ, publication, match score or supplier candidate.
- AI presents visible understanding, assumptions and uncertainty; it asks one meaningful clarification at a time and allows correction in natural language.
- Drafts autosave where practical; back/refresh do not silently destroy work; irreversible actions require explicit human confirmation.
- Every operation has loading, success, empty, error and recovery feedback. No unexplained blank screens.
- The critical flow is comfortable on mobile.

## General Procurement

Home is led by **Caut ceva**: a natural-language description, `Continuă`, reassurance that AI helps clarify, and a manual alternative. **Am un proiect de construcții** is secondary and distinct. Do not force category, need type, or other internal choices before AI can infer them.

## Registration and account recovery

Registration is deliberately simple: it creates a normal CompaSpot User from an email address and a password of at least eight characters. It does not ask the person to choose Buyer or Provider as a permanent account type. Buying is available to an authenticated User; selling is activated incrementally by creating or claiming a company, or by being added as its representative. Clear, user-language validation, rate-limit feedback and a sign-in alternative must be shown; no browser, API, development-environment or HTTP-status jargon is shown to end users.

The action determines context: **Caut ceva** enters procurement and **Oportunități pentru firma mea** enters selling. When organization context is required, ask **Pentru cine faci această achiziție?** or **Pentru ce firmă trimiți această ofertă?** Never ask **Ce rol ai?** Existing Buyer and Supplier accounts remain valid during the compatible transition.

After successful registration, the user is asked to sign in and returns to a safe requested route where applicable. Password recovery always responds in an enumeration-safe way, explains that instructions will arrive only if the address belongs to an account, and provides a clear retry path for expired or incomplete reset links. Development-only reset tokens must never be exposed in a deployed environment.

## Provider and claim

Provider Home begins with relevant opportunities, not KPIs. Request detail immediately answers what is needed, where, when and what to complete, with `Trimite ofertă`, partial response, and decline actions. Claim follows opportunity → claim/confirm company → verify account/contact → offer with minimum friction.

## Offers and integrity

Comparison emphasises total, coverage, delivery, condition, warranty, transport and trust. It does not label the cheapest offer as best without an explanation. Avoid fake urgency, scarcity, hidden ranking, misleading AI, referral spam, or manipulative prompts.

## Visual direction

Forest Warm remains the MVP direction: warm, calm, trustworthy, premium through restraint, human Romanian, readable Inter typography, generous whitespace and accessible status labels. Standardize only real reused components touched by the current slice.
