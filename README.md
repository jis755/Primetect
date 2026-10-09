# PrimeTect Property Management

PrimeTect is a property-management platform for properties, tenants, maintenance, utilities, approvals, and future billing workflows.

This repository is intentionally standalone. It includes a branded Next.js frontend, a .NET Clean Architecture API, and PostgreSQL/EF Core persistence wiring. The current billing slice is a bounded monthly draft/review workflow; collection, payment processing, and deployment are not included.

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

   `appsettings.json` and `Properties/launchSettings.json` are intentionally local-only and ignored. A fresh clone does not need either file committed: use the sanitized example above and pass the API URL explicitly when running locally.

3. Inspect the PrimeTect migrations only after confirming the connection string targets the dedicated `primetect` database. The billing migrations are `MonthlyBillingMilestone` and `PrintableBillingReports`; this repository does not apply them automatically:

   ```powershell
   dotnet ef migrations list --project src/backend/PrimeTect.Infrastructure --startup-project src/backend/PrimeTect.Api
   dotnet ef database update --project src/backend/PrimeTect.Infrastructure --startup-project src/backend/PrimeTect.Api
   ```

4. Copy `src/frontend/.env.example` to `src/frontend/.env.local` if the API will run on a non-default origin.
5. Optionally start PostgreSQL with the example compose file if Docker is available:

   ```powershell
   docker compose -f docker-compose.example.yml up -d postgres
   ```

6. Start the API from the repository root with the same origin used by the frontend:

   ```powershell
   dotnet run --project src/backend/PrimeTect.Api --urls http://localhost:5000
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
dotnet test src/backend/PrimeTect.sln --no-build --nologo --verbosity minimal
Set-Location src/frontend
npm run typecheck
npm run build
```

Run these commands one at a time to limit memory use. The normal API workflow tests use unique in-memory EF Core databases and do not touch PostgreSQL. The frontend includes a small API status indicator that checks the health endpoint in the browser. No real secrets are committed; local credentials belong in ignored configuration files. Keep the checks sequential when PostgreSQL is enabled.

### Disposable PostgreSQL verification

The PostgreSQL-specific tests are opt-in and refuse ordinary application connection strings. They require `PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION` to target a local server (`localhost`, `127.0.0.1`, or `::1`) and the administrative database `postgres` or `template1`. The fixture then creates a random `primetect_verification_<guid>` database, applies all committed migrations from empty, runs the integration tests, and drops only that generated database. It never resets or drops `primetect` or any other database.

PowerShell example (use a local password only; do not put this value in a tracked file):

```powershell
$env:PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=<local-only-password>"
dotnet test src/backend/PrimeTect.Api.Tests/PrimeTect.Api.Tests.csproj --filter FullyQualifiedName~PostgresBillingVerificationTests --nologo --verbosity normal
Remove-Item Env:PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION
```

If the variable is absent, the PostgreSQL facts are reported as skipped with that reason. If it is supplied but the service is unavailable or the safety checks reject the connection, the facts fail with the exact blocker and no existing database is changed. They never fall back to InMemory storage. Before opening a test connection, the fixture verifies that the effective database name is the generated `primetect_verification_<guid>` target; `primetect` and any other existing database are rejected. The tests cover migration application, numeric precision, duplicate cycle constraints, stale versions, organization/property authorization, self-approval, atomic approval/audit failure, immutable snapshots, immediately preceding approved-period readings, missing-period/policy-gap blocking, approved unit/tenant/property statements, multi-unit tenant identity, unnamed tenant details, historical rate changes, zero baselines, and incomplete comparisons. The regular InMemory tests cover the broader workflow matrix, but cannot prove PostgreSQL-specific behavior.

## Current milestone

- Owner registration creates an organization and Admin account in one transaction.
- Identity-backed HttpOnly cookie authentication supports login/logout, Admin invitations, Manager and Building Staff account acceptance, organization isolation, and assigned-property staff access.
- Apartment buildings, individual-room buildings, and shops support configurable floors and units, generated previews, unequal floor layouts, unit enable/archive state, and explicit occupancy status.
- Tenant records allow independently optional name and phone fields, multiple unit assignments, dated history, overlap prevention, and `Not connected` onboarding status.
- The frontend supports property and floor editing, occupancy history with move-in/move-out closure, property charge defaults, unit-level inherited/custom/disabled charge modes, decimal amounts, and effective billing months.
- Monthly billing supports one draft cycle per property/month, effective charge snapshots, fixed charges, metered electricity, explicit opening readings, approved-period carry-forward, warnings below Tk200, missing-reading and policy-gap blocks, staff submission, Manager/Admin return, and Manager/Admin approval. Approved snapshots are immutable and are never labeled as collected.
- Approved billing snapshots can be printed as an individual unit bill, a known-tenant multi-unit statement, or a property summary grouped by historical floor. A monthly comparison report uses only approved snapshots, reports billed totals/charge differences and electricity consumption, marks missing or unapproved periods incomplete, and leaves zero-baseline percentage changes unavailable. A4 print styles support browser Print/Save as PDF; legacy approved cycles without property/floor snapshot fields are refused rather than filled from current records.
- Staff invitations are accepted through a secure one-time token form with expiry and reuse protection. Invitation tokens are shared manually because email delivery is not connected.
- Property setup and maintenance actions remain scoped by organization and role; Building Staff see only assigned properties while Admin and Manager manage structure and charges.

Monthly billing intentionally blocks unresolved policy cases instead of inventing allocations: mid-month tenant changes/partial-month charges, vacant-unit charges, and any cycle without a contiguous immediately preceding approved period require explicit review. Meter resets or replacements are not bypassed by the opening-reading field and need a separate documented workflow. Email delivery, email verification, two-factor authentication, billing collection/payment actions, SafeDocs integration, SMS, police actions, and deployment are not implemented.

The EF Core migrations `InitialPrimeTect`, `MonthlyBillingMilestone`, and `PrintableBillingReports` are committed for the dedicated PrimeTect database, but this repository does not apply migrations automatically. Their applied status is **unknown** until verified separately against the local `primetect` database before manual use. The opt-in PostgreSQL tests use a newly created disposable database; they do not change the applied-status claim for the local `primetect` database. Printable reports never substitute current property/floor/tenant data for missing historical snapshot fields.

## npm audit review

The current lockfile reports 9 findings: 3 moderate and 6 high, with no critical findings. They are in the frontend build dependency graph:

- `next@15.5.27` carries a vulnerable nested `postcss@8.4.31`; npm proposes `next@16.4.0`, a major upgrade. This is relevant to the Next build/server toolchain, but the reported PostCSS issue is not a browser runtime dependency.
- `tailwindcss@3.4.19` brings `chokidar`, `fast-glob`, `micromatch`, `braces`, `postcss-nested`, and `postcss-selector-parser`. The reported issues affect CSS scanning/parsing and build-time file/pattern processing, not shipped application records. npm proposes `tailwindcss@4.3.3`, a major Tailwind migration.
- The direct `postcss` package is already at `8.5.29`; the remaining PostCSS finding is the nested copy under Next.

No `npm audit fix --force` or blind major upgrade was applied. The recommended follow-up is a planned Next 16/Tailwind 4 migration with a visual regression pass, or a targeted dependency update when compatible releases are confirmed.
