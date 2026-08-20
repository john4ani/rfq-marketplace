# CompaSpot MVP v2 — Implementation plan

## Task 0 — Documentation baseline

Update authoritative direction, domain, UX, architecture, plan, AGENTS and ADRs. No application code. **Stop for review.**

## Task 1 — Stabilization — completed locally, pending deployment

P0/P1 only: AI availability verification, Live Market error handling, form feedback, dashboard/account overlap, embedded critical states, password recovery, and Provider relevance dead-end. No redesign.

Completed locally:

- verified AI availability is checked before assisted intake starts; unavailable configurations keep the user on the complete form rather than a failed AI flow;
- retained explicit Live Market loading, retry and error states;
- retained actionable form validation and error focus/scroll behaviour;
- separated Dashboard from Account: Dashboard is the work/start surface and Account is a dedicated route for profiles, preferences and trust;
- protected Buyer-only requests from rendering blank for a Supplier/direct URL; profile loading now completes before the Buyer route decision;
- replaced Provider relevance dead ends with return-to-requests and company-preferences actions, and pointed stale preference actions to Account;
- documented registration, role selection, adding a second profile and password recovery in the active UX requirements; sign-in and registration errors now use user language rather than development tooling;
- verified password reset has production configuration validation and keeps an enumeration-safe response;
- verified locally with TypeScript, focused Angular tests (18), focused .NET integration tests (11), and browser navigation on the local stack.

Not deployed in this task. Deployment remains an explicit release action after review.

## Task 2 — Procurement Core analysis — complete, pending review

Analysis only. Identify construction-specific assumptions, reusable elements, necessary migration only, generic-need support, retained APIs and construction separation.

Also analyse the current identity/profile model against the target contextual-role model. Before proposing any implementation, document:

- how `/register` works today;
- which entities are created for a Buyer;
- which entities are created for a Provider;
- how `ActiveProfile` works;
- which foreign keys and ownership rules depend on Buyer/Provider;
- what can remain unchanged;
- the smallest safe migration required;
- how existing accounts, ownership, offers, requests, trust data and company associations are preserved.

Do not implement identity, registration or profile changes in Task 2. **Stop for review.**

Analysis outcome and decision gate: [`task-2-procurement-core-analysis.md`](task-2-procurement-core-analysis.md).

## Task 3 — Procurement Core implementation

After approval only: **3A User-owned Procurement Foundation** makes the primary registration contract User-only, permits every authenticated User to create and use procurement, makes Buyer-profile defaults optional, and retargets only `Rfqs.BuyerId` and `AiIntakeSessions.BuyerId` from `BuyerProfiles` to `ApplicationUser`. It preserves all legacy routes, roles and data. **3B Generic Procurement compatibility** removes the shared single-Construction-root category assumption and adds a small provisional general category seed set for alternators, timber, transport, excavator hire and specialist services. It does not rewrite schema, matching, construction templates or construction state. **3C AI interpreter boundary** introduces `IGeneralNeedInterpreter` and `IConstructionProjectInterpreter`, retains one existing OpenAI/Azure transport path, and removes construction examples from the shared General prompt. The Construction contract is injectable/routable only until Task 9; it adds no specialised facts, persistence or Passive House intelligence. **3D-A** adds `CompanyMembership` additively and backfills every existing `SupplierProfile` as its active Owner. **3D-B** moves only company profile, category and delivery-setting endpoint authorization to active membership while retaining explicit company-ID routes. **3D-C** moves open opportunities and provider-side offer operations to resolved company context; it accepts an optional `companyId` query parameter, resolves a sole active membership compatibly, rejects ambiguous implicit use, and prevents a User from offering through any represented Company on that User's own need. Offer ownership remains `Offer.SupplierId` (the legacy `SupplierProfile` company ID). **3D-D** resolves messaging access, company notifications, provider Trust facts and invitation claims through active CompanyMembership. Conversation and offer company IDs remain commercial context; messages, notification recipients and claim/audit actors remain User IDs. It preserves existing claim tokens, invitation RFQ context, shared-key provider records and legacy trust verification data without a destructive migration. Notification preference redesign, outreach channels, Trust schema redesign and claim UX redesign remain deferred. Its verification checkpoint is recorded in [Task 3D-D verification](operations/task-3d-d-verification.md). Subsequent slices: 3E construction compatibility. Introducing `IConstructionProjectInterpreter` before Task 9 preserves the existing Construction state exactly as-is and must not add temporary generic `Rfq` fields for new Construction facts. One slice at a time.

## Task 4 — General Procurement Foundation

Complete the generic procurement foundation without a new aggregate or a universal taxonomy. Reuse `Rfq`, `RfqItem`, `NeedCategory`, publication, matching, discovery, offers and outcome. The provisional compatibility category root adds only the reviewed categories needed to demonstrate alternator, timber, transport, excavator hire, specialist service and pellets/energy needs. General AI is intentionally commercial and conservative: it asks one clarification, never claims cross-industry expertise or web research, and may return a non-binding Construction suggestion without redirecting or converting the request. `NeedType.Project` remains generic; a construction template is explicit compatibility state, not its discriminator. Existing construction requests/templates remain intact. The dedicated Home and end-to-end UX redesign follows separately.

## Task 5 — General Procurement + Construction Entry UX / Easy Experience

Rehabilitate the buyer journey around one dominant job per screen: standalone auth, a General Procurement-first Home, separate explicit Construction entry, conversational clarification, human review before sending, useful request/offers states and responsive critical paths. Account becomes user-first: personal data uses BuyerProfile only as compatibility storage, and represented companies are listed from active CompanyMembership records without a global company switch. Preserve existing routes, API and Construction compatibility; do not redesign Provider UX, Supplier Discovery or Best Value in this task.

## Task 6 — Invitation / Claim

Email/SMS, transactional outbox, suppression, rate limiting, preferences and minimal claim-to-offer continuity.

## Task 7 — Provider UX

Opportunities Home, request detail, offer creation, partial offer, decline, claim-to-offer continuity; test with a small established supplier scenario.

Provider Easy Experience implementation uses explicit route-scoped company context: no company shows a buying-safe explanation, one company resolves only for the current flow, and multiple companies require an explicit choice. The provider journey is Company → Opportunities → Request → Offer review → Submit → Company offers. Final MVP testing must cover zero/one/multiple company memberships, self-offer exclusion, full/partial offer, draft/resume, submission, selection/not-selected status, messaging and mobile layouts.

Task 6B closes the zero-company entry gap with one reusable **Adaugă firma** flow. It uses the current SupplierProfile save contract, which creates the active Owner CompanyMembership transactionally, and composes the existing supplier category and delivery-area settings before linking back to company opportunities. It adds no Supplier account/login, role switch, onboarding wizard, Trust redesign or parallel Claim Company flow.

Task 7 web-discovery extension: when internal matches are insufficient, an Administrator may explicitly run bounded Need-context discovery; Admin **Firme** may also use exact/name/locality discovery without creating a match. Stage 1 records Google Places and Web Search evidence independently, merges/deduplicates it with provenance, and records any source warning without discarding usable evidence. Google Text Search is primary and Nearby Search is a bounded coordinate-based fallback. Stage 2 uses the same Responses transport without tools to produce strict JSON candidates and explicit rejections. Only an Administrator's separate acceptance creates/reuses a `SupplierCandidate`; only Need-context acceptance additionally creates/reuses the normal `OpportunityMatch`. No automatic contact, invitation, Company, membership or arbitrary Company↔Need association is introduced.

## Task 7A — Supplier Discovery v2 + OpportunityMatch + Admin Acquisition Intelligence

Reuse the existing `SupplierCandidate` and claim/invitation compatibility flow. Add additive `OpportunityMatch` history for each published need and exactly one target (registered `SupplierProfile` company or candidate), with filtered database uniqueness per target type. Registered provider matching remains authoritative and is persisted for explanation; candidate matching is deterministic from central-category compatibility and county and starts `PendingReview`. An Administrator approves or rejects candidates per opportunity; approval makes a manual secured invitation eligible but never sends or contacts anyone automatically. Provide an Admin need-centric review screen and a company-centric unclaimed-candidate acquisition view with historical and active opportunity counts. Preserve General Procurement and Construction because both use the same central categories and matching semantics. No external web discovery, email/outreach, CRM, automatic contact, ranking automation or new taxonomy belongs to this task.

## Task 8 — Outreach, Contact Policy, Invitation & Claim Continuity

Build on the approved `OpportunityMatch`: contact remains explicitly Administrator-initiated and opportunity-specific. Persist email, phone and direct-contact history; centralize eligibility, suppression and a conservative email cooldown; use the existing `IEmailSender` for invitation delivery; and expose a public, privacy-safe invitation preview and opaque opt-out route. A candidate can be associated with an existing open request through the same OpportunityMatch model. Preserve the original opportunity through User registration, company creation, claim and offer preparation. No bulk outreach, SMS provider, CRM, tracking, or automatic contact belongs to this MVP slice.

## Task 9 — Construction repositioning

Preserve working construction capability, isolate specialised interpretation, feed the Procurement Core, and retain a dedicated construction journey. Test a general alternator, a roof, and a passive-house request.

Construction Projects MVP alignment: General and Construction are now two **distinct intake workflows** converging on the shared Core. Construction has a real interpreter (`IConstructionProjectInterpreter` → `OpenAiConstructionProjectInterpreter`) with its own project-first prompt, JSON schema and construction clarification, a dedicated route/controller (`/rfqs/new/construction`, `construction-intakes`) and a project-framed intake component (describe → construction clarification → project understanding with visible non-certified assumptions → review → publish). It reuses the generic `AiIntakeSession` (new additive `Kind` discriminator column) and one shared OpenAI transport (`OpenAiResponsesTransport`) extracted from the general provider — no duplicated transport, no new AI provider. Confirmed semantics: one Construction entry derives exactly one shared `Rfq`/Procurement Need on approve; no `ConstructionProject` aggregate; `NeedType.Project` is not the discriminator (origin carried by the session's kind + `ApprovedRfqId`). Category leakage removed by scoping every intake surface to its vertical. The General→Construction suggestion transfers the original text into the Construction intake; AI-unavailable falls back to the manual Construction editor preserving text. Shared Core after publication (matching, Supplier Discovery, OpportunityMatch, invitations/claim, offers, comparison, outcome) is unchanged and reused. Deferred: `ConstructionProject` aggregate, multi-Need decomposition and Passive House intelligence.

## Task 10 — Hardening

Responsive/mobile, accessibility, Forest Warm consistency, recovery states, metrics, latency, logs and documentation synchronization.
