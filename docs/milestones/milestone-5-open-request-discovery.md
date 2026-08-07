# Milestone 5 — Open Request Discovery

Status: Complete

## Outcome

A signed-in Supplier can open **Cereri deschise / Open Requests**, see active published RFQs matched to the supplier's selected product categories and delivery counties, filter the list, and inspect a read-only request detail.

## Matching rules

- The RFQ must be `Published` and its quotation deadline must be in the future.
- At least one of the RFQ's required central Categories must be selected by the active Supplier; the API also reports full versus partial coverage.
- Its county must be configured as a delivery county, unless nationwide delivery is enabled.
- If delivery settings have not yet been saved, the Supplier profile county is used as a conservative fallback.
- Search matches the title, project name, delivery locality, or material name.

## Privacy boundary

The Supplier response includes the Buyer display/company name, delivery locality and county, deadline, budget, description, and material lines. It deliberately excludes the exact delivery address, coordinates, phone number, tax identifier, and private RFQ-level Buyer notes.

## API

```text
GET /api/v1/open-rfqs
GET /api/v1/open-rfqs/{rfqId}
```

Optional list query parameters:

- `search`
- `categoryId`

## Offers and selection

- A provider can save one draft offer per request, price each requested line, add delivery cost and timing, submit revisions, and withdraw.
- A provider can review every offer from **Contul meu → Activitate → Ofertele mele**, filter by lifecycle status, see value and deadlines, and reopen the originating request.
- Provider-facing status language distinguishes drafts, offers awaiting a decision, accepted offers, offers not selected after another winner was chosen, and withdrawn offers.
- A Provider who owns an offer retains read access to its request after the request closes, so accepted and not-selected results never lead to an inaccessible detail page.
- A Buyer can reopen an awarded request when a selection was made by mistake. A reason of 10–500 characters is mandatory; an expired request also requires a new future deadline.
- A request can be manually reopened only once. After a new winner is selected, that selection is final for the MVP.
- Reopening clears the selected offer, restores affected offers to submitted/revised status, and notifies every participating Provider with the Buyer’s reason.
- A buyer sees submitted offers ordered by total cost with delivery and validity terms visible side by side.
- The Buyer offer page also acts as the request-management view: it shows the submitted request, current state, delivery context, budget, fulfilment mode, and material/service positions even when no offers exist.
- While a request is still open, its Buyer can extend the quotation deadline to a later future date or cancel the request through an explicit inline confirmation.
- Missing, stale, and wrong-owner request links show an actionable error instead of an otherwise blank page.
- Selecting a winner atomically marks that offer as selected, marks competing offers as not selected, and closes the RFQ to new offers.
- Draft offers remain private to their provider. Buyers only see submitted or revised offers.

## In-app notifications

- Submitting an offer creates an unread notification for the Buyer.
- Submitting changes to an existing offer creates a separate revision notification.
- Notifications link directly to the Buyer offer-comparison page.
- A unique revision key prevents retries from creating duplicate notifications.
- The header badge polls for unread notifications and supports individual or bulk read state.

## UX and information architecture

The milestone includes a usability pass across the complete request-to-offer journey:

- **Piața Live is the Home page.** Its primary content is matched open requests and clear next actions; marketplace activity is summarized without overwhelming the page.
- The full Buyer/Supplier list moved to the dedicated `/participants` directory. It supports search, prioritizes online and nearby profiles, and hides offline profiles by default.
- **Contul meu** opens on a workflow-oriented activity view. Profile forms and supplier matching preferences are available in a secondary **Profil și preferințe** section.
- Open-request cards expose decision-making information at a glance: category, Buyer, location, deadline urgency, item count, and estimated budget.
- The request detail uses a two-column desktop layout: request context on the left and a persistent offer workspace on the right. Small screens collapse into one linear flow.
- Request positions are collapsible cards with a name/quantity/category summary, and the add-position action follows the list at the bottom.
- Buyers explicitly choose whether one Provider must cover the complete request or whether splitting is allowed.
- Each position may target a Provider category. Full-coverage Providers are prioritized; specialized Providers receive only their matching positions when splitting is allowed and no eligible full-coverage Provider exists.
- Provider onboarding captures supply categories, delivery coverage, and an explicit **can fulfil multi-category requests** capability; it does not require an exhaustive product catalogue.
- Selecting several categories alone does not classify a Provider as full coverage for a multi-category request. The Provider must explicitly enable the capability.
- Full-coverage Providers can choose a quick, all-inclusive total-price offer for the complete request or detailed position-by-position pricing. Specialist matches remain itemized.
- Loading skeletons, actionable empty states, consistent status treatments, and responsive navigation are used throughout the main journey.
- The interface uses industry-neutral language. Construction materials may be seeded as the first category, but no core workflow assumes a specific industry.
- Category duplicate/similarity integration tests delete the temporary categories they create, preventing test data from leaking into the local product directory.

### Primary navigation

```text
Piața Live (/)
├── Cereri deschise (/open-requests)
│   └── Cerere + ofertă (/open-requests/:id)
├── Director participanți (/participants)
└── Contul meu (/dashboard)
    ├── Activitate
    └── Profil și preferințe
```

Users who have both Buyer and Supplier profiles can discover open requests without first changing the active profile. The active-profile selector remains useful inside the account workspace for role-specific management screens.

## Deferred

Messaging, attachments, email delivery through SES, payment processing, post-award fulfilment, and Buyer/Provider ratings belong to later milestones. Ratings must not affect participants until confirmation deadlines, reminders, exceptions, dispute handling, and auditable fulfilment events exist; see ADR-0012.
