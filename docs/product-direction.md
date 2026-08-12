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

## MVP scope

Included: General Procurement Need, AI clarification, requirements/location/publish, provider matching, human-approved Supplier Discovery, invitations and company claim, offers/comparison/outcome, trust, notification preferences and suppression, basic product analytics, and specialised Construction Projects that generate one or more procurement needs.

Excluded: inventory, ERP/catalog/stock integrations, payments, company offering, automatic discovery outreach, complex CRM, non-construction vertical AI assistants, broad design-system rewrite, and speculative platform infrastructure.

## Product integrity

AI never invents stock, price, certifications, provider capability, or engineering certainty. It asks one relevant question at a time, exposes assumptions and uncertainty, and never publishes without human confirmation. Construction assistance never claims to replace architects, engineers, designers, or certification professionals.

Supplier candidates are never contacted before Administrator approval. Contact is deduplicated, rate-limited, suppression-aware, and limited to real opportunities.
