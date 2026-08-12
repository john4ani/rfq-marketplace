# CompaSpot MVP v2 — Product direction

## Decision

CompaSpot is procurement-first. Its primary promise is: **„Spune ce ai nevoie. Primește oferte relevante din zona ta.”**

There are two user-facing entry modes and one Procurement Core:

```text
General Procurement ─┐
                     ├─ Procurement Core → matching → discovery → invitations → offers → outcome
Construction Projects┘
```

General Procurement provides breadth: products, materials, parts, equipment, transport, and services across industries. Construction Projects provides depth through construction-specific vocabulary, rules, project decomposition, and AI assistance. Construction remains in MVP, but is not the default CompaSpot experience.

## Experience

Buyer Home presents two choices:

1. **Caut ceva** — the visually dominant general-procurement entry, with natural-language input and one `Continuă` action.
2. **Am un proiect de construcții** — a secondary entry into specialised project assistance.

If a general request looks like construction, AI may suggest Construction Projects discreetly. It must not redirect automatically; the user decides whether to switch or continue as a simple request.

Users may receive value through description, clarification and review before verification. Publishing requires the required account/contact confirmation. A person or company may buy and sell; profile management belongs in Account, not in the first-task experience.

## Identity, Registration and Buyer/Provider Evolution

### Current state

The current MVP distinguishes Buyer and Provider profiles and exposes Buyer/Provider selection and switching in the UX. This must evolve for the procurement-first product.

### Target product rule

A **User** is the identity. Buyer and Provider are not mutually exclusive account types: a person or company may buy, sell or do both. For example, a timber producer may sell timber while buying logs, spare parts, transport and services.

Registration creates a normal CompaSpot user account. It remains minimal and must not force a permanent Buyer-versus-Provider decision.

### Buying

A registered user can start a Procurement Need directly. Where the current backend requires a `BuyerProfile`, it is reused or created transparently; that internal requirement must not be exposed in the UX.

### Selling

Selling capabilities become available incrementally when the user creates or registers a company, claims an existing `SupplierCandidate`, or is added as a company member or representative. Company setup must not block unrelated buying activity.

For a real supplier opportunity, the invitation and claim flow is:

```text
Opportunity → account confirmation/creation if required → Claim Company
→ minimum required company verification → Offer
```

The original opportunity is preserved throughout this flow. An invited supplier must not be diverted through the generic Buyer/Provider registration decision.

### Existing model and migration safety

Reuse the existing `BuyerProfile`, `ProviderProfile` and active-profile mechanisms where practical. Do not perform a broad identity rewrite merely for architectural purity. The smallest compatible evolution must allow one User to buy, let that User represent a selling Company, let that Company/User later create Procurement Needs, and remove Buyer-versus-Provider identity decisions from the main UX.

Existing Buyer and Provider accounts must continue to work. No migration may invalidate users or require account recreation; ownership, offers, requests, trust data and company associations must be preserved.

### UX rule

Remove Buyer/Provider role switching from the primary Home experience. The user action determines context:

- **Caut ceva** → buying/procurement context.
- **Oportunități pentru firma mea** → provider/selling context.

If organisational context is necessary, ask **„Pentru cine faci această achiziție?”** or **„Pentru ce firmă trimiți această ofertă?”** Never ask **„Ce rol ai?”**.

## MVP scope

Included: General Procurement Need, AI clarification, requirements/location/publish, provider matching, human-approved Supplier Discovery, invitations and company claim, offers/comparison/outcome, trust, notification preferences and suppression, basic product analytics, and specialised Construction Projects that generate one or more procurement needs.

Excluded: inventory, ERP/catalog/stock integrations, payments, company offering, automatic discovery outreach, complex CRM, non-construction vertical AI assistants, broad design-system rewrite, and speculative platform infrastructure.

## Product integrity

AI never invents stock, price, certifications, provider capability, or engineering certainty. It asks one relevant question at a time, exposes assumptions and uncertainty, and never publishes without human confirmation. Construction assistance never claims to replace architects, engineers, designers, or certification professionals.

Supplier candidates are never contacted before Administrator approval. Contact is deduplicated, rate-limited, suppression-aware, and limited to real opportunities.
