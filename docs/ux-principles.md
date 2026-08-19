# CompaSpot UX principles

These principles govern all new user-facing work.

## Easy experience

- One dominant action per screen; minimal choices; progressive disclosure.
- Users see practical Romanian language, not internal terms such as RFQ, publication, match score or supplier candidate.
- AI presents visible understanding, assumptions and uncertainty; it asks one meaningful clarification at a time and allows correction in natural language.
- Drafts autosave where practical; back/refresh do not silently destroy work; irreversible actions require explicit human confirmation.
- Every operation has loading, success, empty, error and recovery feedback. No unexplained blank screens.
- The critical flow is comfortable on mobile.

## Primary navigation

Navigation is **contextual, never a permanent mode** (no Buyer/Provider switch, no ActiveProfile, no global active company). The rail follows the current route:

- **Normal user** (default): **Acasă, Cererile mele, Oferte, Cont** + compact unified Messages control and notifications bell.
- **Company selling context** (selling routes): **Oportunități, Ofertele firmei, Mesaje, Firma** + bell, with an **Înapoi în CompaSpot** link back to the user home. Company identity flows through the route's `companyId`; it is never stored as a global active company.
- **Administrare CompaSpot** (`/admin` routes): the operational admin screens (Cereri, Achiziție furnizori, Invitații și recomandări, Firme și verificări, Catalog categorii, Operațiuni) under a clear admin header with an **Înapoi în CompaSpot** button. Admin screens may keep operational terminology.

The brand logo always returns to the user home, so every context has an exit. Administrators reach the admin context through a single **Administrare** entry in the normal-user rail. Legacy surfaces not in MVP v2 (Piața Live, the Furnizori directory) remain reachable by route but are not primary navigation. Error copy never reintroduces "switch to your buyer/supplier profile" language.

## Messaging

Messaging is **procurement-contextual**, never a chat directory: conversations exist only inside a commercial relationship (a submitted offer on a need), and the sender is always the User with the Company as commercial context. A single unified **Mesaje** inbox (💬 in the shell, beside 🔔 in the buyer and company contexts — never admin) lists the User's own conversations whether buying or representing a company, each row showing the other party, the need/offer title with a "Cumpărare · …" or "Company · …" context tag, a last-message preview, relative time and unread state. 💬 is the conversation; 🔔 only signals that something happened and links into it. The inbox complements — never replaces — the contextual entry points (offer → Trimite mesaj, opportunity/offer → Mesaje, notification → conversation). No user directory, presence, typing, receipts, attachments or real-time transport is introduced; the existing poll/refresh and read-tracking are reused.

## General Procurement

Home is led by **Caut ceva**: a natural-language description, `Continuă`, reassurance that AI helps clarify, and a manual alternative. **Am un proiect de construcții** is secondary and distinct. Do not force category, need type, or other internal choices before AI can infer them.

## Registration and account recovery

Registration is deliberately simple: it creates a normal CompaSpot User from an email address and a password of at least eight characters. It does not ask the person to choose Buyer or Provider as a permanent account type. Buying is available to an authenticated User; selling is activated incrementally by creating or claiming a company, or by being added as its representative. Clear, user-language validation, rate-limit feedback and a sign-in alternative must be shown; no browser, API, development-environment or HTTP-status jargon is shown to end users.

The action determines context: **Caut ceva** enters procurement and **Oportunități pentru firma mea** enters selling. When organization context is required, ask **Pentru cine faci această achiziție?** or **Pentru ce firmă trimiți această ofertă?** Never ask **Ce rol ai?** Existing Buyer and Supplier accounts remain valid during the compatible transition.

After successful registration, the user is asked to sign in and returns to a safe requested route where applicable. Password recovery always responds in an enumeration-safe way, explains that instructions will arrive only if the address belongs to an account, and provides a clear retry path for expired or incomplete reset links. Development-only reset tokens must never be exposed in a deployed environment.

## Provider and claim

Provider Home begins with relevant opportunities, not KPIs. Request detail immediately answers what is needed, where, when and what to complete, with `Trimite ofertă`, partial response, and decline actions. Claim follows opportunity → claim/confirm company → verify account/contact → offer with minimum friction.

Provider work is always contextual to a represented company. One active company may be resolved for the current workflow; with multiple companies, CompaSpot asks which company should see opportunities and never stores a global active-company mode. Opportunity and offer screens use plain Romanian, preserve company context in their routes, and show useful loading, empty, error and submitted states.

A User with no represented company is never left in a selling dead end. Account, Home and Opportunities expose the same **Adaugă firma** entry. The compact flow captures required company identity/contact and one-or-more **Categorii de produse** in the same initial form, then continues to delivery-area settings with an explicit return to company opportunities. Company location is labelled **Localitate**, not city, and accepts a village, commune, town or municipality; county is separate. Optional company data is progressively disclosed. Company registration remains distinct from Claim Company, which preserves a Supplier Discovery invitation and its original opportunity.

## Offers and integrity

Comparison emphasises total, coverage, delivery, condition, warranty, transport and trust. It does not label the cheapest offer as best without an explanation. Avoid fake urgency, scarcity, hidden ranking, misleading AI, referral spam, or manipulative prompts.

## Visual direction

Forest Warm remains the MVP direction: warm, calm, trustworthy, premium through restraint, human Romanian, readable Inter typography, generous whitespace and accessible status labels. Standardize only real reused components touched by the current slice.
