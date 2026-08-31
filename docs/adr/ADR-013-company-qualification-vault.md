# ADR-013: Company SEAP qualification vault — legal data, portfolio, certificates, key experts

- Status: accepted
- Date: 2026-08-31
- Area: Company profile / SEAP auto-bidding (Company Vault capabilities)

## Decision

The company's SEAP qualification data is an **additive extension of the existing `SupplierProfile`
aggregate**, named `CompanyQualification` in code to avoid colliding with the existing `CompanyVault`
(document vault, „Depozit de Documente"). Four new entity types live in `Marketplace.Domain.Qualification`:

- `CompanyLegalProfile` (1-to-0..1 with `SupplierProfile`) — legal representative name/role, legal form and
  the IBAN accounts (`BankAccount` owned collection). **CUI, RegCom and the registered address stay on
  `SupplierProfile` as the single source of truth**; the read DTO composes them.
- `CompanyExperience` (portfolio) — client, contract title, contract number/date, value (`decimal`),
  currency, CPV code, completion date (`DateOnly`), document reference URL.
- `CompanyCertificate` — `CompanyCertificateType` enum (`Iso9001`, `Iso14001`, `Iso45001`, `Anre`,
  `Cnsas`, `Other`) persisted as string, serial number, issuer, validity date.
- `CompanyPersonnel` (key experts) — full name, role, `ProfessionalCertificates` (`text[]`, the existing
  Npgsql convention), CV URL.

All four navigations are on `SupplierProfile` (collections backed by private lists, `PropertyAccessMode.Field`).
All collections are **optional and empty by default**, so existing B2B companies are unaffected — no backfill.

The API surface is a new `CompanyQualificationController` at
`/api/v1/companies/{companyId}/qualification` (not `/companies/vault`, which the document vault already
owns), with `GET` (whole vault), `PUT legal`, and per-item `POST`/`PUT`/`DELETE` for experience,
certificates and personnel. Every endpoint is guarded by `CanRepresentCompany` and returns **404 for any
non-represented caller** (no existence leak), mirroring the document vault. The UI entry is a new
„Calificare & Date Firmă (SEAP)" section on the „Firma mea" page (4 cards: legal, portfolio, certificates,
team), served by a new `CompanyQualificationService`.

## Motivation

Auto-bid proposal generation (`.docx`/`.xlsx`) needs the company's real capability data — legal
particulars, similar contracts, certificates and key experts — so the generated documents are 100%
personalized and the AI never invents certifications or capability (product principle: „AI never invents
stock, price, certifications, provider capability"). The existing `CompanyVault` stores raw documents, not
structured capability records.

## Alternatives considered

- **Extending `CompanyVault` (the document vault) with the capability records**: rejected — the document
  vault is a storage/ingestion aggregate; mixing structured capability data into it would couple two
  different concerns and reuse the wrong aggregate.
- **A separate aggregate root `CompanyQualificationProfile` (1-to-0..1, mirroring `CompanyVault`)**: not
  chosen — the user instruction and the aggregate's nature (capability of the company itself) favour direct
  navigations on `SupplierProfile`, keeping the aggregate boundary simple.
- **Duplicating CUI/RegCom/address on `CompanyLegalProfile`**: rejected — creates two sources of truth that
  can diverge; the read DTO composes the legal record with the `SupplierProfile` fields.

## Consequences

- New additive migration `AddCompanyQualificationEntities` (4 tables + owned bank-account table); existing
  rows and flows are untouched.
- EF Core 10 quirk: a new entity added to an already-tracked collection can be classified `Modified`
  (no-op UPDATE). `CompanyQualificationService` applies the same guard the `CompanyVaultService` uses —
  explicit `DbSet.Add` when the state is not `Added`.
- The SPA „Firma mea" page shows the qualification cards („Calificare & Date Firmă (SEAP)").
- **The vault feeds the auto-bid proposal generation** (`BiddingGenerationService`): the auto-bid body
  carries the requesting `companyId` (validated with `CanRepresentCompany`; the paid path carries it through
  the Stripe checkout `Metadata` so the webhook uses the same company). The qualification data is injected
  as a „Date de calificare ale firmei" block in the LLM prompt with strict non-hallucination rules — the LLM
  uses only real legal data/IBAN, matches the best portfolio contract to experience requirements, names only
  listed experts, and writes exact „[De completat: …]" placeholders when data is missing. Generation never
  blocks when the vault is empty.
