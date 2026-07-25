# ADR-0013: Schedule code-quality hardening after the MVP

Status: Accepted

Date: 2026-07-24

## Context

The MVP has been developed quickly to validate the marketplace workflow. The current implementation works, but parts of the codebase do not yet meet the maintainability standard expected for a long-lived production system.

Known weaknesses include:

- large Angular components with inline templates and mixed UI responsibilities;
- dense and inconsistent formatting that makes review and maintenance harder;
- repeated validation, error-handling, and mapping logic;
- incomplete separation between orchestration, domain rules, persistence, and transport concerns;
- uneven automated coverage, especially for component interactions and complete user journeys;
- limited enforcement through linting, formatting, architecture tests, and continuous-integration quality gates;
- UX and accessibility behavior being corrected reactively instead of consistently through shared patterns.
- the Angular development proxy still prints a raw Vite `ECONNREFUSED` stack for the initial API request when the backend is unavailable, even though the application now opens a circuit and presents a recoverable service-status message.

A broad rewrite during MVP validation would slow learning and introduce unnecessary regression risk. Leaving the current structure as the permanent baseline would make every later feature slower and less reliable.

## Decision

Perform a dedicated code-quality hardening milestone immediately after the MVP feature set and workflow have been validated, and before significant expansion, production scaling, or onboarding additional developers.

This is an incremental refactoring effort, not a ground-up rewrite. Existing behavior must first be protected with characterization and end-to-end tests. Refactoring will then proceed by feature boundary in small, reviewable changes.

Security defects, data-integrity risks, broken migrations, and user-blocking correctness problems are not deferred and must still be fixed as soon as they are discovered. New MVP changes should not intentionally add avoidable technical debt merely because the larger hardening milestone is deferred.

## Required hardening scope

The milestone must include:

1. Apply automated formatting and linting to TypeScript, Angular templates, SCSS, and C#.
2. Split oversized UI components into focused presentational components, form sections, and feature services.
3. Extract shared form-validation, error-summary, notification, loading, and confirmation patterns.
4. Replace weakly typed forms, payloads, and generic error handling with explicit types and stable contracts.
5. Clarify application-service, domain, persistence, and API responsibilities and enforce dependency rules.
6. Remove duplication and dead code without introducing speculative abstractions.
7. Standardize API errors using predictable problem details and field-level validation responses.
8. Add component, domain, integration, migration, and critical browser-journey tests.
9. Add accessibility checks for keyboard use, focus management, semantic labels, validation, and contrast.
10. Add CI quality gates for build, tests, formatting, linting, migrations, and architecture rules.
11. Document the resulting conventions with short examples and review guidance.
12. Replace the raw initial Vite proxy failure with a concise structured development diagnostic while preserving visibility of genuine API and proxy defects.

## Completion criteria

The hardening milestone is complete only when:

- all critical Buyer and Provider journeys have repeatable automated coverage;
- formatting and linting run automatically and pass in CI;
- no known oversized component combines unrelated page, form, data-access, and workflow responsibilities;
- API and UI validation errors are specific, consistent, and testable;
- architecture dependency rules are automatically checked;
- database migrations can be applied from a clean database and upgraded from the current MVP schema;
- the team can add a representative feature without copying page-specific infrastructure or expanding an already oversized component.

## Alternatives considered

- Refactor everything before completing MVP validation: rejected because product behavior is still changing and a large rewrite would protect assumptions that may soon be discarded.
- Accept the current implementation indefinitely: rejected because maintenance cost, regression risk, and onboarding difficulty would grow with every feature.
- Replace the application with a new architecture after MVP: rejected because a rewrite would discard working behavior and repeat already solved problems without evidence that the current technology choices are wrong.

## Consequences

- MVP validation can continue without pretending the current implementation is the final engineering baseline.
- A specific post-MVP milestone must be planned and cannot be silently displaced by new feature work.
- Refactoring work must preserve observable behavior through tests.
- Some short-term duplication and structural weakness remain, but they are now explicitly bounded and have measurable exit criteria.

## Revisit when

Revisit this decision before the hardening milestone only if code quality begins blocking MVP testing, causes repeated production-like failures, creates a security or data-integrity risk, or makes a required MVP change unsafe.
