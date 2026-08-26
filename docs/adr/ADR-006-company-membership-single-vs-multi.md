# ADR-006 — Company representation: single-company now, separate Company entity later

## Decision

CompaSpot keeps the **legacy single-company model for the MVP** (one self-registered company per
account, `SupplierProfile.Id = AspNetUsers.Id`, shared key) and communicates that limitation honestly
in the UI: when a company profile already exists, the interface explains that only one company per
account is currently supported (no "add another company" entry point; `/account/company/new` becomes
the edit path for the existing company).

The sustainable multi-company direction is **Option A — a separate `Company` aggregate**:

- new `Companies` table (own `Id`, registration data, categories/delivery) decoupled from
  `AspNetUsers`;
- `SupplierProfile` keeps its identity but references the company via a new nullable
  `CompanyId` FK (`Company` ← `SupplierProfile`), so a user profile may act for several companies;
- `CompanyMembership` remains the owner/representative link between a user and a company;
- a new `ICurrentCompanyContext` service in .NET 10 resolves the active company from the JWT
  (explicit `companyId` claim or the sole owned company), used by every company-scoped controller.

This is explicitly deferred to the post-MVP refactor, **not** to be implemented during the MVP flow
work. The short-term priority is a stable single-company base for the SEAP + OCR + Claude validation.

## Consequences

- No schema change now; the shared-key FK (`SupplierProfiles.AspNetUsers_Id`) stays until the
  refactor.
- `/account/company/new` doubles as the edit form for the existing company; the UI states this.
- Medium-term refactor must migrate existing `SupplierProfiles` into `Companies`, backfill
  `CompanyMembership`, and repoint all company-scoped queries/endpoints through
  `ICurrentCompanyContext`.
- `CompanyMembership` already supports the plural model, so the authorization layer
  (`CanRepresentCompanyAsync`) is forward-compatible.
