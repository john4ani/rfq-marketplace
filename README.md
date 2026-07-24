# RFQ Marketplace

A production-oriented, industry-neutral marketplace for requests for quotation. Buyers publish structured needs, relevant providers submit comparable offers, and buyers select the best fit. Construction materials are the first category, while the product model is designed to expand across markets and industries.

The repository foundation, identity, Buyer/Supplier profiles, supplier settings, and “Piața Live” are complete. Milestone 4 adds Buyer-owned RFQ drafts, material lines, pasted spreadsheet input, publication, and cancellation.

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

5. Start the API:

   ```powershell
   dotnet run --project src/Marketplace.Api
   ```

6. Install and start Angular:

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

Do not commit `.env`, local settings, credentials, certificates, or production configuration.

## Database migrations

Migrations are owned by `Marketplace.Infrastructure`. Create a migration with:

```powershell
dotnet ef migrations add <Name> --project src/Marketplace.Infrastructure --startup-project src/Marketplace.Api --output-dir Persistence/Migrations
```

Production migration execution will be added to the deployment workflow. The API does not silently migrate the production database on startup.

## Architecture

Start with [the architecture index](docs/architecture/README.md) and [the ADR index](docs/decisions/README.md). Design-affecting changes must update the relevant documents and diagrams.
