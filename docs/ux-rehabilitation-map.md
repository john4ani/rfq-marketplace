# CompaSpot UX Rehabilitation Map

> **Scope:** factual inventory of the public MVP as inspected on 11 August 2026. This document is analysis only; it proposes no redesign or implementation. “Implemented” means present in the current Angular route/component and API source, not merely described in a milestone.

## 1. Application structure

| Route | Access | Current purpose and actions | Next destinations | Main UI / API | State |
|---|---|---|---|---|---|
| `/` | Anonymous; authenticated Buyer/Provider | **Piața Live**. Anonymous users can register or sign in. Signed-in providers can open relevant requests; Buyers can start a request. Shows market presence and supplier recommendations. | Register, sign-in, `/open-requests`, `/rfqs/new`, `/participants` | `LiveMarketComponent`; `GET /piata-live`, `POST /piata-live/presence`, `GET /open-rfqs` | Partial: availability/presence errors are silently suppressed; recommendations need a completed Provider profile. |
| `/participants` | All | Public participant directory with filters and participant cards. | Back to Live Market; participant/profile actions where rendered | `ParticipantDirectoryComponent`; live-market data | Partial: a directory, not a full searchable supplier profile journey. |
| `/sign-in` | All (also available to signed-in users) | Email/password sign-in; links to registration and password reset. | Dashboard after login; register; reset | `SignInComponent`; `POST /auth/login`, `/auth/refresh` | Working source flow; no observed live browser session for this audit. |
| `/register` | Anonymous only | Select Buyer, Provider, or both, then email/password registration. | Sign-in / verification | `RegisterComponent`; `POST /auth/register` | Partial: account profile completion happens separately after registration. |
| `/forgot-password`, `/reset-password`, `/verify-email` | All | Password recovery, reset and email confirmation. | Sign-in | Auth components; `/auth/request-password-reset`, `/auth/reset-password`, `/auth/confirm-email` | Partial: recovery includes a development-token link in the UI source. |
| `/supplier-invitations/:token` | All | Claim a secure supplier invitation. | Profile completion / dashboard | `SupplierInvitationComponent`; `GET/POST /supplier-invitations/{token}` | Partial: a token-only entry path, not reachable through ordinary Provider navigation. |
| `/dashboard` | Authenticated | Role-dependent home/workspace. Buyer: need starter, request list and trust/profile settings. Provider: opportunities, offer list, profile/settings. | Live Market, directory, new request, open requests, settings | `DashboardComponent` plus nested list/settings components | Partial: one route contains several distinct screens controlled by local state. |
| `/account` | Authenticated | Same `DashboardComponent`, initially opens **Profil și preferințe**. | Dashboard; back to workflow after profile save | Profile, supplier-settings, trust components | Working but duplicates dashboard implementation and creates “Acasă/Contul meu” conceptual overlap. |
| `/open-requests` | Authenticated; useful only with Provider profile | Relevant published requests, category filter, create-request link for Buyer-capable accounts. | Request detail; dashboard/account settings; assisted request | `OpenRfqListComponent`; `GET /open-rfqs` | Partial: relevance depends on Provider categories/service area; incomplete profile yields an instructional empty/error state. |
| `/open-requests/:rfqId` | Authenticated Provider | Request detail and Provider offer editor; can save/submit/withdraw offer and report delivery after selection. | Open requests, offer conversation | `OpenRfqDetailComponent`, `OfferEditorComponent`; `GET /open-rfqs/{id}`, offer APIs | Partial: access can be denied for profile/zone/category mismatch; offer editor is embedded, not a route. |
| `/rfqs/new` | Authenticated Buyer profile | Assisted need intake: free text → AI clarification(s) → editable draft → save or publish. | Advanced editor, edit, offer comparison | `AssistedIntakeComponent`; `/assisted-intakes/*`, `/rfqs/{id}/publish` | Partial/high dependency: redirects to advanced form if AI unavailable; current live AI configuration was recently repaired but not exercised in this audit. |
| `/rfqs/new/advanced` | Authenticated | Full request editor, including manual creation and resumed AI draft. | Dashboard, request/offers | `RfqEditorComponent`; categories/templates/RFQ APIs | Working source flow; large, single-page form. |
| `/rfqs/:rfqId/edit` | Authenticated Buyer/owner | Edit saved draft or published request where backend permits; publish/cancel/extend actions. | Offer comparison, dashboard | `RfqEditorComponent`; `GET/PUT /rfqs/{id}`, publish/cancel/extend | Partial: same extensive form also serves creation; role/ownership errors are API-driven. |
| `/rfqs/:rfqId/offers` | Authenticated Buyer/owner | Received offers, comparison, select offer, reopen, delivery confirmation and outcome recording. | Offer conversation, dashboard | `BuyerOfferComparisonComponent`, `ProcurementOutcomeComponent`; offers/RFQ APIs | Partial: several terminal-state controls coexist on one screen. |
| `/offers/:offerId/conversation` | Authenticated participant in offer | Buyer/Provider conversation around one offer. | Buyer offer comparison or Provider request detail | `ConversationComponent`; `/offers/{id}/conversation/*` | Partial: not discoverable as a primary area; reached from offer UI. |
| `/admin/invitations-referrals` | Administrator | View invitation/referral operations and link to discovery. | Supplier discovery | `InvitationsReferralsAdminComponent`; discovery APIs | Partial. |
| `/admin/supplier-discovery` | Administrator | Create/review supplier candidates and create secure invitation links against opportunities. | Invitation route | `SupplierDiscoveryAdminComponent`; `/admin/supplier-candidates`, `/admin/discovery-opportunities` | Partial/manual operational tool. |
| `/admin/catalog` | Administrator | Shared category catalog and category creation. | Admin navigation | `CategoryCatalogComponent`; `/categories`, `/project-templates` | Partial: category-creation duplicate/similarity confirmation exists here, while Provider profile only asks to contact admin. |
| `/admin/trust` | Administrator | Verify account facts. | Admin navigation | `TrustAdminComponent`; `/admin/trust/accounts/*` | Partial/manual trust operation. |
| `/admin/concierge` | Administrator | Analytics and manual operations: match, correct request, normalize offer, record outcome. | Admin navigation | `ConciergeAdminComponent`; `/admin/concierge/*` | Partial/internal-only. |
| `**` | All | Redirect unknown routes to `/`. | Live Market | Router redirect | Working, but masks bad/dead URL entry. |

There are no distinct routed pages for Provider dashboard, Provider profile, offer creation, request preview, provider matching, or outcome: these are conditionally rendered areas inside the routes above.

## 2. Role map

| Role | Actual capabilities / landing behaviour |
|---|---|
| Anonymous | Can see Live Market shell, participant directory, register/sign in, recovery, email verification, and invitation claim. Cannot start an RFQ, see request details, or see offers. |
| Buyer | Can create a Buyer profile, start assisted or advanced RFQs, save/edit/publish/cancel/extend them, view offers, select/reopen/record outcome/confirm delivery, message offer participants, and view Buyer trust facts. `/dashboard` shows Buyer home when the active profile is Buyer. |
| Provider | Can complete a company profile, select multiple common catalog categories and service area, see relevant open requests, prepare/submit/withdraw offers, report delivery, message, see offer history and trust facts. `/dashboard` shows Provider home when active profile is Supplier. |
| Administrator | Has all account capabilities plus the five admin routes. The admin role is claim-based; it is not a selectable self-service profile. |

One email can have both Buyer and Provider profiles. Registration submits an array of account types; an existing signed-in account can add a missing profile using the account switcher (`POST /auth/profiles/{accountType}`). The active profile controls the dashboard’s primary view, but the shell exposes both valid cross-role actions where profile existence is checked. Initial post-login navigation is `/dashboard`; the active token/account type determines which dashboard content appears. Profile completion is required before creating a Buyer request or saving Provider categories/delivery settings. Verification is presented as trust information; the inspected frontend does not gate normal Buyer/Provider action on a verified badge.

## 3. Buyer critical journey

1. **Live Market or Buyer Dashboard** → “Cerere nouă” / need text → navigation to `/rfqs/new` (with optional `description` query) → assisted intake.
2. **Assisted intake – description** → enter at least 10 characters and “Continuă cu AI” → `POST /assisted-intakes` → clarification or draft review. “Am deja lista” leads to `/rfqs/new/advanced`.
3. **Clarification** → choose/type one answer → `POST /assisted-intakes/{session}/clarifications` → another clarification or review. Prior questions/answers remain visible in history.
4. **Review** → edit title, one primary category, description, delivery fields and proposed lines → client-side validation → save draft (`POST /assisted-intakes/{session}/approve`) or save then publish (`POST /rfqs/{id}/publish`).
5. **Advanced editor branch** → complete a full RFQ form → `POST /rfqs` / `PUT /rfqs/{id}` → returns to dashboard or offers after publish. AI session can be resumed using `assistedSession` query.
6. **Published status** → Buyer request list on `/dashboard` → action opens `/rfqs/{id}/offers`. There is no separate Buyer request-status route.
7. **Offer comparison** → inspect offers, select one → `POST /rfqs/{id}/offers/{offerId}/select` → selected/closed state, delivery or outcome controls.
8. **Outcome** → record accepted offer or another outcome → `POST /rfqs/{id}/outcome`; request may be reopened once with a reason → `POST /rfqs/{id}/reopen`.

Branches/dead ends: a missing Buyer profile directs the user to `/dashboard?section=settings`; unavailable AI redirects to the advanced form; an invalid review only reports one sequential error at a time; matching is implicit after publication rather than a dedicated Buyer “matching” screen; Provider discovery/invitation has no Buyer-facing progress screen.

## 4. Provider critical journey

1. **Register as Provider or add Provider profile** → auth/profile switch → `/dashboard` in Supplier mode.
2. **Profile and settings** → `/account` or dashboard settings → save company profile, categories and delivery area → `/suppliers/{id}`, `/suppliers/{id}/categories`, `/suppliers/{id}/delivery-settings`.
3. **Provider Dashboard / Live Market** → “Explorează cererile” → `/open-requests` → relevant list.
4. **Open requests** → category change automatically reloads `GET /open-rfqs` → request detail.
5. **Request detail** → embedded offer editor save/submit → `PUT /open-rfqs/{id}/offer`, then `POST .../submit` → stays within request detail; submitted offers appear in dashboard list.
6. **Offer status** → dashboard offer list → return to request or open conversation. Selected offer allows delivery report; Buyer then confirms.
7. **Profile/trust** → `/account` settings → account profile, categories/service area, compact trust summary/facts.

Unnecessary/intermediate structure to review: Provider onboarding, categories and delivery settings are separate cards on the same account route; the offer editor lives within the request-detail screen; Provider “dashboard” is an active-profile variation of the Buyer dashboard rather than a route.

## 5. Screen and form inventory

### Critical screen inventory

- **Dashboard (Buyer):** intro, need starter, three-step strip, workspace bar, two quick-action cards, Buyer request list, and settings/trust when switched. Multiple navigation/CTA locations compete with the primary request starter.
- **Dashboard (Provider):** provider hero, explore CTA, workspace bar, two action cards, submitted-offer list, profile reminder, and settings/trust. No independent incoming-request dashboard section; it links to the list.
- **Advanced RFQ editor:** header/back link; basic/context cards; need type; optional project template; category grouping; split choice; paste-list tool; repeatable item details; delivery/deadline; commercial notes; save/publish actions. It is a long form with collapsible completed item summaries.
- **Open request list/detail:** list has category filtering, explanatory/empty states and cards. Detail combines request information, trust, offer creation/submission and delivery actions.
- **Offer comparison:** comparison cards/table-like offer details, selection/reopen/outcome/delivery elements and offer-specific messaging. Terminal-state guidance and outcome actions can coexist.
- **Account:** profile editor, Provider categories/service area (when active Supplier), and trust summary/facts. This is one route with a role-dependent multi-card form.
- **Admin:** five separate operational pages with tables/forms; only the navigation groups them.

### Form inventory

| Form | Visible fields / required rules | Default / notes |
|---|---|---|
| Registration | Email, password, account-type selection. | Can select Buyer, Provider, or both. |
| Buyer profile | Display name*, Buyer type*, company name/CUI when company, phone*, city*, county*, radius*. | Individual, 30 km. |
| Provider profile | Trading name*, registration no.*, optional CUI/description/website; contact name/email*/phone*; address*, city*, county*. | Requires a separate categories/delivery configuration. |
| Provider categories | Any leaf catalog category; at least one to save. | Shared central categories; UI tells user to contact admin for a missing category. |
| Provider service area | Normal radius*, maximum radius*, optional minimum order, nationwide flag or at least one county. | 50/100 km. |
| Assisted need | Natural-language description (minimum 10); per-AI clarification answer; review title/category/address/locality/county/lines. | AI endpoint is synchronous HTTP per request; manual fallback exists. |
| Advanced RFQ | Title*, need type*, primary category*, one or more item fields (kind/name/quantity/unit/category), delivery address/locality/county*, radius*, quotation deadline*. Optional project context/template, budget, notes, brand/specification, alternatives and split. | DirectPurchase, radius 30 km, deadline +48h; terminology includes internal concept “need type”. |
| Offer | Embedded within open request; line prices/offer commercial fields and submit. | Offer pricing mode must make sense for multi-line requests; no standalone route. |
| Outcome/reopen | Outcome selection and optional note; reopen reason and optional deadline. | Reopen is constrained by backend. |

## 6. AI touchpoints

Only the assisted intake uses AI. Trigger: “Continuă cu AI” on `/rfqs/new`, followed by each clarification response. Backend route/service: `AssistedIntakesController` / `IAssistedIntakeService` and configured `OpenAiNeedExtractionProvider`. Configuration presently identifies an OpenAI Responses endpoint/model, but no secret is recorded here. Calls are synchronous request/response (not streaming); the UI waits with a busy label. Each clarification is a separate sequential call. The response yields draft, one clarification or final review, assumptions, uncertainties and optional research sources. A 503 or unavailable availability check redirects to the manual advanced form with an explanatory message; other errors show a generic/detail message. There is no visible retry control in the intake itself. Manual continuation is possible, including for a session passed to advanced form.

## 7. Known/broken flows observed or evidenced by current source

| Priority | Finding | Evidence / effect |
|---|---|---|
| P0 | AI availability has recently failed publicly when the deployment lacked the secure key. | Configuration was repaired and deployment completed on 10 August; the live assisted flow was not authenticated/exercised in this audit, so it remains a verification risk rather than claimed resolved behaviour. |
| P1 | API errors on Live Market are swallowed. | `loadMarket` uses `error:()=>undefined`; presence counts/community can silently be stale or empty. |
| P1 | Form feedback is inconsistent. | Assisted review validates one condition at a time and scrolls to the alert, not the failing field; profile forms use disabled submit plus generic errors. |
| P1 | `/dashboard` and `/account` are one component with local section state. | Browser history/URLs describe distinct destinations while implementation and much content overlap. |
| P1 | Several critical destinations are embedded, not addressable. | Provider dashboard, matching, offer editor, request preview and outcome cannot be linked to as independent states. |
| P1 | Password recovery exposes a development-token route in UI source. | The recovery component builds a reset link with `developmentToken()`. Requires production behaviour verification. |
| P1 | Provider relevance failure can end discovery. | Category/zone/profile mismatch denies detail access; recovery asks users to navigate back to profile settings. |
| P2 | Internal/domain terminology leaks. | “RFQ”, “Supplier discovery”, “concierge”, “candidate”, “matching”, and taxonomy/category language occur in routes, admin UI and some user paths. |
| P2 | Shell is crowded for Admin/small laptop heights. | Admin links, notifications and account/footer share a constrained fixed rail; responsive CSS contains several height-specific compensations. |

## 8. Current terminology

| Term | Where it appears |
|---|---|
| Piața Live | public/home route, navigation, live-market hero |
| Cerere / cerere de ofertă | Buyer creation, lists, Provider discovery |
| RFQ | routes, API/domain internals; can surface in technical/error language |
| Cumpărător / Furnizor | active profile, dashboard, request/offer journeys |
| Categorie / șablon de proiect | RFQ and Provider settings; admin catalog |
| Potrivire / relevantă | Provider recommendations and profile guidance |
| Ofertă / ofertă selectată | Provider offer editor, Buyer comparison/outcome |
| Încredere / verificări cont | trust summary/facts and admin verification |
| Supplier candidate / Supplier discovery / concierge | admin navigation and internal operations, partially English |
| Publicare / ciornă / rezultat / livrare | RFQ and outcome lifecycle |

## 9. Navigation map

```text
Anonymous
├── Piața Live (/)
├── Furnizori (/participants)
├── Autentificare / Înregistrare / recuperare / verificare
└── Invitație furnizor (/supplier-invitations/:token)

Authenticated Buyer
├── Acasă (/dashboard)
│   ├── Cerere asistată (/rfqs/new)
│   ├── Formular avansat (/rfqs/new/advanced)
│   └── Cererile mele (embedded)
├── Piața Live (/)
├── Furnizori (/participants)
├── Contul meu (/account)
│   └── Profil / încredere (embedded)
├── Editare cerere (/rfqs/:id/edit)
└── Oferte / rezultat (/rfqs/:id/offers)
    └── Mesaje (/offers/:id/conversation)

Authenticated Provider
├── Acasă (/dashboard; Supplier active profile)
├── Piața Live (/)
├── Cereri deschise (/open-requests)
│   └── Cerere + ofertă embedded (/open-requests/:id)
├── Furnizori (/participants)
├── Contul meu (/account; profile/categories/area/trust embedded)
└── Mesaje (/offers/:id/conversation)

Administrator
└── all above, plus invitations/referrals, supplier discovery, catalog, trust and concierge admin routes
```

No routes are declared as dead; the wildcard redirect means mistyped/deprecated paths silently go to home. The invitation route is operationally valid but isolated from ordinary navigation.

## 10. Component/design-system map and responsive behaviour

The app shell is `App`: persistent CompaSpot masthead (handshake SVG mark and “Nevoi. Oameni. Valoare.”), desktop fixed left rail, mobile drawer, notification panel, account shortcut, sign-out, and an API-unavailable banner with manual retry. There is a public header for anonymous users. Feature components use standalone Angular components, mostly inline templates and feature-local SCSS. Reusable functional pieces include account switcher, notification menu, profile editor, supplier settings, trust summary/facts, Buyer/Provider lists, offer editor/outcome, and conversation.

Forest Warm tokens are centralized in `styles.scss`: forest, light forest, warm white, sand, ink, amber, semantic states, soft shadow and card radius. The system is not yet a complete shared component library: many feature components hold their own templates/styles; button, card, status, error and loading patterns are repeated or vary by feature. Inline styles are used in at least notifications and several admin/auth components.

Desktop uses a fixed 16.25rem rail and sticky masthead. Under 52rem the rail becomes a drawer controlled by a scrim; the desktop header/account affordances are reduced. The CSS intentionally hides scrollbars in the mobile navigation. Risks to validate visually: fixed rail height with extensive Admin navigation, notification popover beside the rail, long forms/tables/cards at tablet widths, and comparison/detail content on phones. No rendered desktop/tablet/mobile audit was possible in the available browser session; these are source-based risks, not measured rendering findings.

## 11. Public deployment map

Public URL: `https://compaspot.com`. The Angular SPA is served through CloudFront with custom-domain DNS managed in Cloudflare. Staging API is hosted on AWS Lightsail with PostgreSQL in the deployed stack. Client calls use `/api/v1/*`; the frontend proxy/deployment routing provides the API origin. Authentication uses JWT plus refresh-cookie endpoints; the app posts/removes live-market presence around session lifecycle. Major external dependencies include AWS infrastructure/SSM, PostgreSQL, email delivery configuration and the configured OpenAI Responses API for assisted intake. No credentials, tokens, keys or connection strings are included here.

## 12. Local screenshot capture attempt (11 August 2026)

### Environment evidence

- Local frontend: `http://localhost:4200/` returned **HTTP 200**.
- Local API readiness: `http://localhost:5221/health/ready` returned **HTTP 200** with PostgreSQL healthy.
- Local PostgreSQL Docker service was healthy and reused; no production system, production account, production secret, or public deployment was used.
- The API had initially inherited `Production` and failed readiness validation. It was restarted as `Development` with the normal repository launch profile. This was runtime setup only, not an application/configuration change.

### Capture result

After the frontend and API were both running locally, the in-app browser rendered `http://localhost:4200` successfully. Eight actual local-browser PNGs were captured at the required viewport and stored in `docs/ux-current/`. No public site was used.

The active local session is a Buyer account with one published request whose offer has already been selected. The local session has no active Provider profile, and the locally configured assisted intake is available only at its initial entry state (no existing clarification/review session was present). Those missing states are recorded as unavailable below instead of being fabricated.

### Requested capture manifest

The table records the route/state, role, task and source-indicated CTAs that must be visually checked once a local browser is available. “Not observed” is deliberate: none of these is claimed as rendered evidence.

| File requested / viewport | Route and state | Role | User task | Source-indicated primary CTA | Source-indicated competing CTAs | Rendered-screen result / observable UX problems |
|---|---|---|---|---|---|---|
| `01-buyer-dashboard.png` — 1440×900 | `/dashboard`, active Buyer | Buyer | Start or manage a need | “Creează cererea” | Piața Live; Descoperă furnizori; workspace/profile controls | **Captured.** The dashboard presents the large need starter, two quick-action cards, profile switching and three request columns. Visible density/CTA competition should be reviewed from the PNG. |
| `02-create-request.png` — 1440×900 | `/rfqs/new/advanced` initial | Buyer | Complete a structured request manually | “Verifică înainte de trimitere →” | Save draft; add item; back to account | **Captured.** The screen exposes many fields, category checkboxes and multiple conceptual sections in one desktop viewport. |
| `03-ai-intake.png` — 1440×900 | `/rfqs/new` initial | Buyer | Enter a need in natural language | “Continuă cu AI” | Advanced-list link; back home | **Captured.** The CTA is disabled until enough text is entered; the manual-form branch remains prominent. |
| `04-ai-clarification.png` — 1440×900 | `/rfqs/new?session=…`, clarification present | Buyer | Answer one clarification | Choice / “Răspunde” | Back; contextual details | **Not captured** — requires an existing usable intake session and local browser. |
| `05-request-review.png` — 1440×900 | `/rfqs/new?session=…`, draft ready | Buyer | Review/edit before publishing | “Confirmă și publică” | “Salvează ciorna”; advanced-list link | **Not captured** — requires usable session and local browser. |
| `06-published-request.png` — 1440×900 | `/dashboard`, published request listed with selected offer | Buyer | Check the published request’s status | “Vezi oferta selectată” | Need starter; Live Market; supplier directory | **Captured.** Local data is post-selection rather than merely published; it shows request status in the dashboard column. |
| `07-offers-received.png` — 1440×900 | `/rfqs/2e02c500-2290-4fd2-8672-ea9108a5583b/offers` | Buyer | Inspect offers received | State-level offer review | Reopen; delivery/outcome; messages | **Captured.** One eligible offer is already selected, so this is not the unselected-offers state. |
| `08-offer-comparison.png` — 1440×900 | Same route, scrolled to comparison card | Buyer | Compare price, coverage, trust and terms | Selected-offer state | Message; reopen; delivery actions | **Captured.** The comparison card is rendered for a single selected offer; multi-offer selection controls were not present in the available local data. |
| `09-provider-dashboard.png` — 1440×900 | `/dashboard`, active Supplier | Provider | Find activity and manage submitted offers | “Explorează cererile” | Profile/preferences action; Live Market | **Not captured** — requires existing Provider credentials/session and local browser. |
| `10-open-requests.png` — 1440×900 | `/open-requests`, relevant results | Provider | Find a relevant request | “Vezi detalii și oferă” | Create request (if Buyer profile); preferences links | **Not captured** — requires Provider categories/service area and local browser. |
| `11-request-details.png` — 1440×900 | `/open-requests/:id` | Provider | Understand request before responding | Offer-editor save/submit action | Back; delivery/report actions if eligible | **Not captured** — requires a relevant request and local browser. |
| `12-create-offer.png` — 1440×900 | `/open-requests/:id`, editable offer | Provider | Build/save/submit an offer | “Trimite oferta” | Save draft; back; conversation if existing | **Not captured** — requires eligible Provider/request and local browser. |
| `13-provider-profile-trust.png` — 1440×900 | `/account`, active Supplier | Provider | Complete company, categories/service area and inspect trust | Save profile/settings | Account switcher; multiple independent save actions | **Not captured** — requires Provider session and local browser. |
| `14-login.png` — 1440×900 | `/sign-in` while a local session remains active | Anonymous-oriented sign-in screen | Sign in | “Autentificare” | Forgot password; create account | **Captured.** The sign-in form renders, but the authenticated application shell remains visible because this route is not guest-only. This is an observable state to review. |
| `15-mobile-buyer-dashboard.png` — 390×844 | `/dashboard`, active Buyer | Buyer | Start/manage needs using mobile shell | “Creează cererea” | Drawer navigation, Piața Live, directory/profile controls | **Captured.** The compact masthead/menu appears while the accessibility tree still contains the rail; visual drawer/overflow review should use the PNG. |
| `16-mobile-provider-request.png` — 390×844 | `/open-requests/:id`, active Supplier | Provider | Assess a request and create an offer on mobile | Offer submit | Drawer, back, save draft/delivery actions | **Not captured** — requires Provider/request data and local browser. |

The exact error/output for the browser limitation is also stored with the capture manifest in `docs/ux-current/README.md`.

## 13. Final summary

### A. Buyer critical path (≤15 steps)
1. Sign in/register. 2. Complete Buyer profile if needed. 3. Open dashboard or Live Market. 4. Start request. 5. Describe need. 6. Answer AI clarifications. 7. Review/edit draft. 8. Save or publish. 9. Return to request list. 10. Open offers. 11. Compare. 12. Select, record outcome, or reopen. 13. Confirm delivery if applicable.

### B. Provider critical path (≤15 steps)
1. Register/add Provider profile. 2. Complete company profile. 3. Select categories. 4. Save delivery area. 5. Open Provider dashboard/Live Market. 6. Explore open requests. 7. Open relevant request. 8. Prepare/save offer. 9. Submit. 10. Track status from dashboard. 11. Message if needed. 12. Report delivery if selected. 13. Maintain profile/trust facts.

### C. Top complexity hotspots

1. Dashboard/account dual-purpose structure. 2. Long advanced RFQ form. 3. AI-to-manual transition. 4. Offer comparison plus terminal/outcome actions. 5. Provider relevance/profile prerequisites. 6. Category/template/project vocabulary. 7. Embedded rather than routed workflow states. 8. Multi-profile/active-profile switching. 9. Fixed admin navigation rail. 10. Manual admin discovery/invitation operations.

### D. P0/P1 defects affecting critical paths

The AI availability deployment risk, silent Live Market errors, generic/sequential validation, duplicate dashboard/account structure, unaddressable workflow states, development-token recovery exposure, and Provider relevance dead ends are the current P0/P1 inventory. Each needs live verification before it is described as fixed.

### E. Highest-impact simplification targets

Buyer dashboard/start path; advanced RFQ creation and preview; Provider discovery/detail/offer workflow; offer comparison/outcome state; account/profile setup; the app shell and admin rail.

### F. UI functionality likely unnecessary for first-pilot users

Manual concierge normalization/correction/matching, supplier-candidate administration, invitation/referral administration, project-template administration, and some multi-outcome lifecycle variations are operationally useful but not part of the shortest Buyer–Provider transaction path.
