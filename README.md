# RFQ Marketplace

A production-oriented, industry-neutral marketplace for requests for quotation. Buyers publish structured needs, relevant providers submit comparable offers, and buyers select the best fit. Construction materials are the first category, while the product model is designed to expand across markets and industries.

The current MVP journey centers on **Piața Live**: providers discover matched open requests, inspect the need, submit or revise an offer, and Buyers receive an in-app notification that links to side-by-side offer comparison. The account dashboard separates day-to-day activity from profile and matching settings, while the participant directory lives on its own focused page.

The repository foundation, identity, Buyer/Supplier profiles, supplier settings, “Piața Live”, RFQ publication, provider offers, buyer comparison, winner selection, in-app notifications, private offer messaging, production hardening, password recovery, and the repeatable MVP acceptance gate are complete through Milestone 7. Milestone 8 adds the implemented AWS deployment foundation and the live production environment. See [the Milestone 8 record](docs/milestones/milestone-8-aws-deployment.md) and the [environment naming note](docs/operations/environments.md).

## Technology baseline

- .NET 10 LTS / ASP.NET Core 10
- Entity Framework Core 10 with PostgreSQL 17
- Angular 21 LTS with standalone components and strict TypeScript
- Docker Compose for local PostgreSQL

Angular 21 is intentionally pinned because the available Node 24.14 runtime does not meet Angular 22's Node 24.15 minimum. Upgrade Angular and Node together in a reviewed dependency change.

## Prerequisites

- .NET SDK 10.0.201 or a compatible 10.0 patch
- Node.js supported by Angular 21
- pnpm 11
- Docker with Compose

## Start locally

1. Copy `.env.example` to `.env`.
2. Replace the local database password in `.env`.
3. Start PostgreSQL:

   ```powershell
   docker compose up -d postgres
   ```

4. Apply database migrations:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/Marketplace.Infrastructure --startup-project src/Marketplace.Api
   ```

5. Store the development OpenAI key outside the repository:

   ```powershell
   dotnet user-secrets set "AiIntake:ApiKey" "<your-api-key>" --project src/Marketplace.Api
   dotnet user-secrets set "GooglePlaces:ApiKey" "<your-google-places-api-key>" --project src/Marketplace.Api
   ```

6. Start the API:

   ```powershell
   dotnet run --project src/Marketplace.Api
   ```

7. Install and start Angular:

   ```powershell
   cd src/marketplace-web
   pnpm install
   pnpm start
   ```

Local endpoints:

- Angular: `http://localhost:4200`
- API: `http://localhost:5221`
- OpenAPI JSON: `http://localhost:5221/openapi/v1.json`
- Liveness: `http://localhost:5221/health/live`
- Readiness: `http://localhost:5221/health/ready`

The liveness endpoint checks the process only. Readiness also checks PostgreSQL connectivity.
The Angular shell can start while the API is unavailable. It shows one service-status alert, suppresses further API calls until an explicit retry, and reloads authenticated data when readiness is restored. The initial failed proxy request remains visible in the development terminal as the diagnostic that opened the circuit; replacing its raw Vite `ECONNREFUSED` stack with a concise message is deferred to the post-MVP hardening work in ADR-0013.

## Build and test

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build

cd src/marketplace-web
pnpm install --frozen-lockfile
pnpm exec ng test --watch=false
pnpm build
```

## Solution structure

```text
src/
  Marketplace.Api/             HTTP composition root
  Marketplace.Application/     Use cases and application contracts
  Marketplace.Domain/          Aggregates and business rules
  Marketplace.Infrastructure/  EF Core and external adapters
  marketplace-web/             Angular SPA
tests/
  Marketplace.Domain.Tests/
  Marketplace.Application.Tests/
  Marketplace.IntegrationTests/
docs/
  architecture/
  decisions/
```

Dependency direction is documented in `docs/architecture/folder-structure.md`.

## Configuration

Configuration uses standard ASP.NET Core precedence. Environment variables override JSON using double underscores, for example:

```text
ConnectionStrings__MarketplaceDatabase
Frontend__Origin
```

Assisted intake defaults to direct OpenAI with `AiIntake__Provider=OpenAi`, `https://api.openai.com/v1/responses`, and `gpt-5.6-luna`. Azure OpenAI uses the same application adapter: set `AiIntake__Provider=AzureOpenAi`, the Azure Responses endpoint, deployment name, and key. Do not store either provider key in tracked configuration.

Outside Development, the API also requires production `AllowedHosts`, a non-placeholder JWT key, SMTP password-reset configuration, and an explicit trusted Caddy proxy IP. See [Milestone 7](docs/milestones/milestone-7-mvp-acceptance-and-production-hardening.md) and `appsettings.Example.json`.

Do not commit `.env`, local settings, credentials, certificates, or production configuration.

## Database migrations

Migrations are owned by `Marketplace.Infrastructure`. Create a migration with:

```powershell
dotnet ef migrations add <Name> --project src/Marketplace.Infrastructure --startup-project src/Marketplace.Api --output-dir Persistence/Migrations
```

Production migrations run as an explicit, idempotent deployment step before the new API release becomes active. The API does not silently migrate the production database on startup.

## AWS deployment

Milestone 8 uses Terraform, GitHub Actions OIDC, CloudFront with a private S3 frontend, and a low-cost Lightsail host running Caddy, the API, and PostgreSQL. Start with the [AWS deployment runbook](docs/operations/aws-deployment-runbook.md), the [backup and restore runbook](docs/operations/database-backup-and-restore.md), and the [Milestone 8 record](docs/milestones/milestone-8-aws-deployment.md).

The bootstrap Terraform root also manages the account-wide monthly AWS cost budget and its direct email alerts.

## Architecture

Start with [the architecture index](docs/architecture/README.md) and [the ADR index](docs/decisions/README.md). Design-affecting changes must update the relevant documents and diagrams.
