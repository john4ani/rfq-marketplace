# CompaSpot MVP v2 — Task 2 Procurement Core analysis

## Status and decision gate

**Analysis complete. No application, schema, registration or profile behaviour was changed in Task 2.**

This document records the current implementation, the reusable core, the construction-specific assumptions, and the smallest compatible evolution for procurement and identity. The proposed implementation must be reviewed before Task 3 starts.

## Executive conclusion

The current `Rfq` aggregate has already evolved into a usable Procurement Core. It supports one or many central categories, product and service lines, full or partial provider coverage, publication, offers, outcome, delivery state and audit history. Creating a new `ProcurementNeed` table or renaming `Rfqs` would add migration risk without product value and is not recommended.

The current identity model is less aligned with MVP v2:

- `ApplicationUser` is the real login identity, but Buyer and Supplier Identity roles are presented as account types;
- `BuyerProfile` and `SupplierProfile` use the same identifier as `ApplicationUser` through one-to-one shared-primary-key relationships;
- procurement ownership and AI intake require a `BuyerProfile`, not merely an authenticated User;
- `SupplierProfile` contains company data but can belong to exactly one User and a User can represent exactly one such company;
- `ActiveProfile` is only transient frontend state, yet it controls which valid actions and screens appear.

The smallest safe target is to keep the current tables and identifiers where they already carry business history, make buying an authenticated User capability, and introduce a membership seam between Users and the existing supplier/company record. This preserves existing accounts, requests, offers and provider configuration while allowing contextual buying and selling.

### Supplier / Company semantic alias

`SupplierProfile` is the **legacy persistence name**. In all new domain, API and UX work, it semantically means **Company / ProviderOrganization**: the selling organization that owns capabilities, delivery settings, offers and company-facing Trust facts. It must not be interpreted as the personal seller profile of `ApplicationUser`. `CompanyMembership` is the association between a User and that Company.

## Evidence reviewed

The analysis traced:

- domain entities and EF Core configurations;
- authentication, profile, procurement, assisted-intake, offer, matching and supplier-discovery services;
- API authorization and request/response contracts;
- Angular registration, authentication and active-profile behaviour;
- the migrations that evolved requests into procurement needs and centralized categories;
- integration tests for authentication, profiles, assisted intake, procurement flow and invitation/claim;
- the active product, domain, architecture, UX and ADR documentation.

## 1. Current Procurement Core

| Concern | Current implementation | Assessment |
|---|---|---|
| Need aggregate | `Rfq`, explicitly documented as the compatible internal implementation of `ProcurementNeed` | Reuse |
| Requirement groups | `NeedCategory` persisted in `ProcurementNeedCategories`, exactly one primary category per need | Reuse as compatibility storage |
| Requirement lines | `RfqItem`, associated with a `NeedCategory`; supports `Product` and `Service` | Reuse; internal names may remain |
| Classification | `Vertical` → `Category`, with parent/child compatibility | Reuse pragmatically; General Procurement is an entry mode, not a permanent Vertical |
| Need shape | `DirectPurchase`, `ShoppingList`, or `Project`; category cardinality is independent of type | Reuse |
| Location and commercial facts | address/locality/county/radius, dates, alternatives, split fulfilment, transport and budget | Reuse for physical/local MVP needs |
| Matching | category hierarchy, delivery area, full/partial coverage, manual concierge override | Reuse |
| Offers | one provider offer per need, item coverage, alternatives, comparison, selection and delivery completion | Reuse |
| Lifecycle | draft, publish, receive responses, award, completion/cancellation, one reopen and explicit outcome | Reuse |
| Traceability | `ProcurementNeedAuditEvents`, product events and notifications | Reuse |
| Assisted intake | one `INeedExtractionProvider` and one prompt operating on current categories | Split by interpretation boundary |
| Construction templates | `ProjectTemplate` belongs to a Vertical, but the service hardcodes Construction | Keep compatible data; isolate construction behaviour |

### Existing migration quality

The earlier procurement migrations already use data-preserving transformations:

- `EvolveProcurementNeedModel` added source, input type, line kind, outcome and audit events, and backfilled audit history for existing requests.
- `AlignCentralCategoryModel` created `ProcurementNeedCategories`, copied the former request/item categories into the new grouping, linked every item to its group, and only then removed the old single-category columns.

Task 3 should continue this additive/backfill-first pattern.

## 2. What is already generic and should remain unchanged

The following behaviour is not construction-specific and should not be rewritten:

- `Rfqs` as the persistence table and existing request identifiers;
- `RfqItems`, `ProcurementNeedCategories` and their existing identifiers;
- one-or-many categories regardless of `NeedType`;
- requirement kind `Product` or `Service`;
- category hierarchy expansion and provider full/partial coverage policy;
- delivery/service area and nationwide matching;
- publication, deadline, offer, comparison, outcome and delivery lifecycle;
- selected-offer, reopen and audit history;
- notification and product-event foundations;
- existing `/api/v1/rfqs`, `/api/v1/open-rfqs`, offer and outcome routes during the compatibility period.

No new `ProcurementNeed` table, broad rename, event-sourcing rewrite, microservice split or new generic repository is justified for MVP v2.

## 3. Construction-specific assumptions found

| Area | Assumption or leakage | Required treatment |
|---|---|---|
| Seed data | Construction is the only active Vertical and all seeded categories belong to it | Do not assume a permanent General Procurement Vertical; add only a clearly provisional placeholder/category support if compatibility requires it, without changing Construction IDs |
| Category administration | The current admin UI and creation service assume the Construction vertical | Keep one central category administration surface; do not encode general-flow rules in a placeholder Vertical |
| Project templates | `ProjectTemplateService` validates only `VerticalConfiguration.ConstructionId` | Move this behaviour behind the Construction boundary |
| Shared need aggregate | `ProjectName`, `NeedType.Project` and `ProjectTemplateId` are stored directly on `Rfq` | Retain as legacy/generic project-shaped compatibility data; `NeedType.Project` must not discriminate the Construction context, and no more construction facts belong on `Rfq` |
| Construction project | No `ConstructionProject` aggregate exists | Add only in Task 9, where it can generate one or more Procurement Needs; until then preserve existing project state exactly as-is |
| AI | One prompt contains custom doors/gates and material-specific guidance; there are no separate General Need and Construction Project interpreters | Introduce separate interpreter contracts and routing only; keep the extraction transport reusable and do not create temporary generic fields for new Construction facts |
| UI language | Multiple screens say materials, timber, roof or construction where the underlying capability is generic | Replace only in the relevant UX slices; keep construction examples inside the construction journey |
| Search display | Search includes `ProjectName` and provider detail describes multi-category needs as projects | Use need/requirement language in shared screens |

The optional physical delivery fields remain acceptable for the first General Procurement slice. A broader fulfilment-location model is not required until a validated use case cannot be represented by the existing location, transport and service-line fields.

## 4. Smallest Procurement Core evolution

### No core schema migration required

General Procurement can use the existing `Rfqs`, `ProcurementNeedCategories`, `RfqItems`, categories, matching and offer schema. The first generic scenarios—such as an alternator, timber, transport or excavator hire—fit the existing structure.

### Additive data/configuration required

- make generic procurement available as an entry mode; reuse existing categories where suitable;
- only if current API/configuration requires a separate root, add an explicitly provisional General Procurement placeholder and a minimal reviewed category set; it must carry no domain or matching rules;
- keep all existing Construction vertical/category identifiers and associations unchanged;
- route general and construction intake through separate interpreters only;
- stop hardcoding Construction in shared category/template operations;
- preserve existing `ProjectName`, `ProjectTemplateId`, project-specific intake state and construction requests exactly as-is throughout the transition;
- do not add temporary generic fields to `Rfq` to host new Construction facts before Task 9.

The current globally unique category slug can remain for MVP if new slugs are globally distinct. A composite vertical/slug migration is not necessary unless a real taxonomy collision appears.

### Construction separation rule

Shared Procurement may know that a need was generated from another workflow, but it must not learn roof pitch, airtightness, structural systems or other construction rules. Construction mode is explicit; it is never inferred from `NeedType.Project`. Before Task 9, keep the existing Construction project state exactly as-is. `IConstructionProjectInterpreter` is an interpretation boundary only, not an aggregate or a reason to add temporary generic `Rfq` fields. In Task 9, `ConstructionProject` owns new specialised facts and generates one or more ordinary Procurement Needs. Existing project-shaped `Rfq` records remain valid generic compatibility records.

## 5. API compatibility

| API surface | Decision |
|---|---|
| `/api/v1/rfqs` and child actions | **Approved compatibility surface:** retain routes and resource names; evolve DTOs additively only where required |
| `/api/v1/open-rfqs` | Retain; matching already works against central categories |
| buyer/provider offer endpoints | Retain routes; later resolve authorization from User/company context |
| `/api/v1/categories` | Retain; do not require a General Procurement Vertical in the API; expose any provisional taxonomy context only where compatibility needs it |
| `/api/v1/project-templates` | Retain for Construction compatibility; do not make it the generic need-type catalog |
| `/api/v1/assisted-intakes` | Retain public contract where practical; route internally to the appropriate interpreter |
| `/api/v1/auth/register/buyer` and `/supplier` | Keep temporarily as legacy compatibility routes; the primary registration contract becomes User registration |
| `/api/v1/auth/profiles` and `/profiles/{accountType}` | Keep temporarily for old clients; stop using them as the primary UX/context model |

`ProcurementNeed` is the product/domain term; it does not require a `/procurement-needs` URL. Breaking route or DTO changes are not required for Task 3 and provide no user value.

## 6. Current identity/profile model

### How `/register` works today

The Angular `/register` screen requires the person to select Buyer, Supplier, or both. Buyer is selected by default; an invitation return URL selects Supplier by default. It sends `email`, `password` and `accountTypes` to `POST /api/v1/auth/register`.

The API also exposes `POST /auth/register/buyer` and `POST /auth/register/supplier`. `IdentityService.RegisterAsync` rejects an empty/unknown role selection, creates one `ApplicationUser`, and assigns the selected ASP.NET Identity roles. Registration does not create a `BuyerProfile` or `SupplierProfile` row.

### Entities created for a Buyer

At registration:

- one `ApplicationUser`;
- one `AspNetUserRoles` association for `Buyer`;
- no `BuyerProfile`.

`BuyerProfile` is created lazily only after `PUT /api/v1/buyers/{currentUserId}`. Its primary key equals the User ID and its current required defaults include display name, phone, city, county and search radius.

### Entities created for a Provider

At registration:

- one `ApplicationUser`;
- one `AspNetUserRoles` association for `Supplier`;
- no `SupplierProfile` and no separate Company.

`SupplierProfile` is created lazily only after `PUT /api/v1/suppliers/{currentUserId}`. It stores company and contact data, and its primary key equals the User ID. Categories, delivery settings and delivery areas are added separately and use that same ID.

### How `ActiveProfile` works

`ActiveProfile` is not a backend entity and is not persisted in the database, JWT, local storage or refresh token. It is an Angular signal inside `AuthService`:

- `/auth/profiles` returns linked accounts derived from Identity roles, not profile-table existence;
- the frontend selects the first returned role when its current selection is invalid;
- switching changes only in-memory client state;
- refresh/navigation can therefore choose a different context based on role ordering;
- many routes, navigation entries and components treat the selected role as the current identity even though both roles use the same User ID.

This is why valid buying or selling actions can disappear without any ownership change in the backend.

### Current authorization and ownership coupling

- Buyer controllers use `[Authorize(Roles = Buyer)]`; Supplier controllers use `[Authorize(Roles = Supplier)]`.
- Controllers pass the JWT subject User ID as `buyerId` or `supplierId`.
- Request ownership is tested as `Rfq.BuyerId == currentUserId`.
- Offer ownership is tested as `Offer.SupplierId == currentUserId`.
- Supplier matching and notification exclude `SupplierProfile.Id == Rfq.BuyerId`, because both currently mean User ID.

The same identifier therefore represents login identity, buyer owner, supplier company and company representative depending on the call site.

## 7. Foreign-key and ownership dependency map

| Data | Current principal/meaning | Compatibility decision |
|---|---|---|
| `BuyerProfiles.Id` | `ApplicationUser.Id`, shared PK | Keep as optional procurement defaults/profile |
| `SupplierProfiles.Id` | `ApplicationUser.Id`, shared PK, but row contains company data | Preserve table/IDs and legacy FK initially; add membership around it before considering independent company identity |
| `Rfqs.BuyerId` | `BuyerProfiles.Id` | Change FK principal to `ApplicationUser`; preserve every value and request ID |
| `AiIntakeSessions.BuyerId` | `BuyerProfiles.Id` | Change FK principal to `ApplicationUser`; preserve every value/session |
| `Offers.SupplierId` | `SupplierProfiles.Id` | Keep; after company evolution it identifies the selling company |
| `SupplierCategories.SupplierId` | `SupplierProfiles.Id` | Keep |
| `SupplierDeliverySettings/Areas.SupplierId` | `SupplierProfiles.Id` | Keep |
| `ConciergeMatchOverrides.SupplierId` | `SupplierProfiles.Id` | Keep |
| future `Rfqs.BuyingCompanyId` | Not present | **Deferred:** add only after organizational buying UX is implemented; existing and MVP-v2 requests remain User-owned |
| `SupplierCandidate/Invitation.ClaimedBySupplierId` | Despite the name, FK points to `ApplicationUser` | Preserve as claim actor during transition; add explicit claimed company association before the new claim flow |
| `Notifications.RecipientId` | `ApplicationUser.Id` | Keep; notifications go to people/accounts |
| `AccountTrustRecords.AccountId` | `ApplicationUser.Id` | Keep all facts; add company-level facts additively when company membership is introduced |
| audit actor IDs / product event actor | User/account actor | Keep |
| conversations | Buyer/Supplier IDs copied from offer/request, without profile FKs | Preserve rows; resolve participant labels/context through compatibility mapping |

## 8. Target contextual identity model

```text
User (ApplicationUser)
├── may create and own Procurement Needs
├── optional BuyerProfile (contact/location defaults, not an account type)
└── CompanyMembership 0..N
       └── Company / selling organization
            ├── compatibility storage: current SupplierProfile
            ├── provider categories and delivery capability
            ├── offers
            └── optional company Trust facts

ProcurementNeed
├── BuyerUserId (current BuyerId value, now FK to User)
└── BuyingCompanyId? (**deferred** until organizational buying UX is implemented)

Offer
└── SupplierCompanyId (current SupplierId and existing SupplierProfile FK)
```

Buyer and Provider are contexts/capabilities, not mutually exclusive identities:

- **Caut ceva** enters procurement as the current User;
- **Oportunități pentru firma mea** enters selling with a selected Company membership;
- if organization context matters, the UX asks for whom the purchase or offer is made;
- no primary flow asks the user to choose a permanent role.

## 9. Smallest safe identity migration

### Slice A — User-owned procurement, required before the new Buyer flow

1. Make the primary registration contract email/password only. Keep the old role-based endpoints temporarily for deployed-client compatibility.
2. Authorize buying actions for any authenticated User instead of requiring the Buyer role. Existing Buyer/Supplier roles and JWT claims remain valid; no role data must be deleted or backfilled.
3. Repoint only the `Rfqs.BuyerId` and `AiIntakeSessions.BuyerId` foreign keys from `BuyerProfiles` to `AspNetUsers`. The GUID values already identify the same Users, so this is a drop/recreate-FK migration with no ownership rewrite.
4. Treat `BuyerProfile` as optional contact/location defaults. Do not fabricate phone or location values. Collect missing facts contextually and create/update the profile transparently when useful.
5. Separate signed-in User state from action context in Angular. Stop using `ActiveProfile` to decide whether an authenticated User may start or view their procurement.

### Slice B — Additive Company membership, required before the new claim/provider flow

1. Keep `SupplierProfiles` and their existing IDs as the compatibility storage for selling companies; do not rename or copy the table during MVP.
2. Add a membership table linking `ApplicationUser` to `SupplierProfile`, with membership role/status and timestamps.
3. Backfill one Owner membership for every existing `SupplierProfile`, using `UserId = SupplierProfile.Id` and `SupplierProfileId = SupplierProfile.Id`.
4. Retain the `SupplierProfiles.Id` → `ApplicationUser.Id` shared-primary-key FK as a legacy constraint for this slice. Membership is additive and already permits additional Users to represent an existing company/profile.
5. Resolve supplier operations from an authorized company membership. During transition, if a User has exactly one backfilled membership and no company context is supplied, select it as the legacy-compatible default.
6. Keep offers, categories, delivery settings/areas and concierge matches attached to `SupplierProfile`; they therefore remain attached to the same company ID.
7. Before changing claim behaviour, add an explicit claimed-company association to candidate/invitation state while retaining the existing `ClaimedBySupplierId` value as the historical User actor. Backfill the company association when an existing same-ID SupplierProfile is present.
8. Replace the current raw-ID self-match check with a membership/company policy: a User must not submit an offer from a company they represent to their own need.

Do not remove the shared-primary-key FK in this slice. Reconsider it only when a concrete scenario requires a Company to have an identity independent of its original User (for example, transfer of ownership or a Company created before any representative exists), and only after every affected EF configuration, validation path, claim flow, authorization assumption and test has been migrated and verified.

### Explicit deferral — organizational buying

Do not add `Rfqs.BuyingCompanyId` in Task 3, and do not add company-purchasing context merely because it may be useful later. For MVP v2, a timber company owner may buy as the authenticated User while their company is represented separately for selling. Introduce organizational buying only after the General Procurement flow works and its UX explicitly asks **Pentru cine faci această achiziție?**. At that point, `BuyerId` remains the accountable User and a nullable company association can be added as a separate, tested slice.

## 10. Existing-data preservation

| Existing data | Preservation rule |
|---|---|
| Users, password hashes, refresh tokens, email confirmation | No identity recreation; unchanged User IDs |
| Buyer/Supplier/Admin roles | Retain; stop exposing Buyer/Supplier as a permanent registration decision |
| Buyer profiles | Keep every row and ID; reinterpret as optional defaults |
| Requests and audit history | Keep every row/ID/BuyerId; change only FK principal |
| AI intake sessions and approved-request links | Keep every row/ID; change only BuyerId FK principal |
| Supplier profiles/company details | Keep every row/ID and retain the shared-PK constraint; backfill memberships additively |
| Supplier categories and delivery capability | Unchanged because SupplierProfile IDs remain stable |
| Offers, offer lines, selection and outcomes | Unchanged because request and SupplierProfile IDs remain stable |
| Supplier candidates/invitations | Keep rows/tokens/history; add company association rather than overwriting actor data |
| Trust and audit facts | Never delete or move destructively; retain User phone/account facts and audit history, then copy the existing company-verification fact into an additive company trust record when company membership is introduced |
| Notifications and conversations | Preserve recipient and historical participant IDs; adapt display/context resolution |

Every migration must use expand → backfill → verify → constrain. Destructive cleanup or legacy-route removal belongs only after deployed compatibility has been verified.

## 11. Risks and explicit review decisions

Task 3 must not start until the following are approved:

1. Keep `Rfqs` as the compatible Procurement Need table; do not create/rename a parallel aggregate.
2. Make procurement ownership User-based by changing the two BuyerProfile foreign keys to User foreign keys.
3. Keep `BuyerProfile` as optional defaults rather than a prerequisite account type.
4. Reuse `SupplierProfile` as the compatibility company record and introduce memberships additively, retaining its shared-PK FK until a concrete independent-company scenario justifies removing it.
5. Keep legacy role/profile routes temporarily while removing them from the primary UX.
6. Keep General Procurement as an entry mode; use only provisional/additive taxonomy support if compatibility requires it, and keep Construction intact.
7. Defer a dedicated `ConstructionProject` aggregate to Task 9; Task 3 only creates the boundary and interpreter separation, while preserving existing Construction project state exactly as-is.
8. Add optional buying-company context without changing the accountable User ownership of existing or new needs.

## 12. Recommended implementation sequence after approval

1. **Identity compatibility foundation:** User registration/auth state, authenticated buying, Buyer foreign-key seam, transparent optional BuyerProfile.
2. **Generic Procurement Core:** General Procurement entry flow, pragmatic category support, and removal of shared construction hardcoding.
3. **Interpreter boundary:** General Need interpreter plus Construction Project interpreter, sharing transport/schema infrastructure where practical.
4. **Offer/matching compatibility:** verify general product, service, full and partial coverage without schema duplication.
5. **Company membership foundation:** introduce membership before the claim/provider UX changes that depend on company context.
6. **Construction compatibility:** prove existing construction requests/templates/offers remain readable and operable.

Each item should be a separate tested slice with a review stop. Task 2 itself makes none of these changes.

## 13. Explicit MVP deferrals and rejected rewrites

| Item | Decision |
|---|---|
| Complete `ConstructionProject` aggregate | **Defer to Task 9** |
| `Rfqs.BuyingCompanyId` | **Defer** until organizational buying UX is implemented and validated |
| Legacy Buyer/Supplier Identity roles | **Defer to post-MVP**; leave internal compatibility roles in place |
| Legacy role-specific registration endpoints | **Defer removal** until deployed clients have migrated |
| `SupplierProfile` rename | **Defer indefinitely**; legacy persistence naming is acceptable |
| `Rfq` rename | **Defer indefinitely**; no real reason currently exists |
| Composite `(VerticalId, Slug)` uniqueness | **Defer** until an actual category-slug collision occurs |
| Broad Company rewrite | **Reject for MVP**: no `SupplierProfile` rename and no Offers, categories or Trust migration |
| Universal taxonomy refactor | **Reject for MVP**: retain the current Vertical/Category model and add only validated categories incrementally |
| Distributed cache / Redis | **Reject for MVP**; introduce only after measured demand demonstrates a need |

## 14. Task 2 acceptance checklist

- [x] Identified construction-specific assumptions.
- [x] Identified reusable Procurement Core elements.
- [x] Identified the minimum procurement migration/data work.
- [x] Confirmed how generic product/service needs fit the current model.
- [x] Identified retained APIs and compatibility boundaries.
- [x] Defined how Construction remains separate from shared Procurement.
- [x] Documented the current `/register` flow.
- [x] Documented Buyer and Provider entity creation.
- [x] Documented `ActiveProfile` behaviour.
- [x] Mapped Buyer/Provider foreign keys and ownership rules.
- [x] Identified what remains unchanged.
- [x] Proposed the smallest safe identity/company migration.
- [x] Defined preservation of accounts, ownership, offers, requests, Trust and company associations.
- [x] No application or schema implementation performed.
