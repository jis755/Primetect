# PrimeTect Property Management

PrimeTect is a property-management platform for properties, tenants, maintenance, utilities, approvals, and future billing workflows.

This repository is intentionally standalone. The first slice includes a branded Next.js frontend, a .NET Clean Architecture API, and PostgreSQL/EF Core persistence wiring. Billing and deployment are not part of this initial setup.

## Repository layout

```text
src/
  frontend/                         Next.js + TypeScript + Tailwind
  backend/
    PrimeTect.Api/                  HTTP entry point and health endpoint
    PrimeTect.Application/          Use cases and application contracts
    PrimeTect.Domain/               Core entities and business rules
    PrimeTect.Infrastructure/       EF Core and PostgreSQL integration
    PrimeTect.Api.Tests/             isolated API workflow tests
```

## Prerequisites

- Node.js 20 or newer and npm
- .NET SDK 10.0 or newer
- PostgreSQL 16 or newer for persistence work

## Local setup

1. Use a dedicated local PostgreSQL database named `primetect`. Inspect first; do not drop, reset, or delete an existing database:

   ```powershell
   psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname = 'primetect';"
   # Only if the query returned no row:
   psql -U postgres -d postgres -c "CREATE DATABASE primetect;"
   ```

   The application never points at a SafeDocs database. PostgreSQL 16+ is supported; PostgreSQL 18 also works.

2. Copy the example API configuration and replace the local-only password with your local PostgreSQL credential. The destination is ignored by Git:

   ```powershell
   Copy-Item src/backend/PrimeTect.Api/appsettings.example.json src/backend/PrimeTect.Api/appsettings.Development.json
   ```

3. Inspect and apply the PrimeTect migration only after confirming the connection string targets the dedicated `primetect` database:

   ```powershell
   dotnet ef migrations list --project src/backend/PrimeTect.Infrastructure --startup-project src/backend/PrimeTect.Api
   dotnet ef database update --project src/backend/PrimeTect.Infrastructure --startup-project src/backend/PrimeTect.Api
   ```

4. Copy `src/frontend/.env.example` to `src/frontend/.env.local` if the API will run on a non-default origin.
5. Optionally start PostgreSQL with the example compose file if Docker is available:

   ```powershell
   docker compose -f docker-compose.example.yml up -d postgres
   ```

6. Start the API from `src/backend`:

   ```powershell
   dotnet run --project PrimeTect.Api
   ```

   The API health endpoint is available at `http://localhost:5000/api/health`.

7. Start the frontend from `src/frontend`:

   ```powershell
   npm install
   npm run dev
   ```

   Open `http://localhost:3000`.

## Build and verify

```powershell
dotnet build src/backend/PrimeTect.sln
dotnet test src/backend/PrimeTect.sln
cd src/frontend
npm run typecheck
npm run build
```

The API tests use a unique in-memory database and do not touch PostgreSQL. The frontend includes a small API status indicator that checks the health endpoint in the browser. No real secrets are committed; local credentials belong in ignored configuration files.

## Current milestone

- Owner registration creates an organization and Admin account in one transaction.
- Identity-backed HttpOnly cookie authentication supports login/logout, Admin invitations, Manager and Building Staff account acceptance, organization isolation, and assigned-property staff access.
- Apartment buildings, individual-room buildings, and shops support configurable floors and units, generated previews, unequal floor layouts, unit enable/archive state, and explicit occupancy status.
- Tenant records allow independently optional name and phone fields, multiple unit assignments, dated history, overlap prevention, and `Not connected` onboarding status.
- Property defaults and unit-level charge settings support explicit inheritance/custom modes and decimal effective billing months. There is no billing calculation engine yet.

Email delivery, email verification, two-factor authentication, billing calculations, and deployment are not implemented in this milestone. Invitation creation returns a one-time token for local setup because email delivery is not connected.

## npm audit review

The current lockfile reports 9 findings: 3 moderate and 6 high, with no critical findings. They are in the frontend build dependency graph:

- `next@15.5.27` carries a vulnerable nested `postcss@8.4.31`; npm proposes `next@16.4.0`, a major upgrade. This is relevant to the Next build/server toolchain, but the reported PostCSS issue is not a browser runtime dependency.
- `tailwindcss@3.4.19` brings `chokidar`, `fast-glob`, `micromatch`, `braces`, `postcss-nested`, and `postcss-selector-parser`. The reported issues affect CSS scanning/parsing and build-time file/pattern processing, not shipped application records. npm proposes `tailwindcss@4.3.3`, a major Tailwind migration.
- The direct `postcss` package is already at `8.5.29`; the remaining PostCSS finding is the nested copy under Next.

No `npm audit fix --force` or blind major upgrade was applied. The recommended follow-up is a planned Next 16/Tailwind 4 migration with a visual regression pass, or a targeted dependency update when compatible releases are confirmed.
