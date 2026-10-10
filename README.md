# PrimeTect Property Management

PrimeTect is a property-management platform for properties, tenants, maintenance, utilities, approvals, monthly billing, and manually reported collections.

This repository is intentionally standalone. It includes a branded Next.js frontend, a .NET Clean Architecture API, and PostgreSQL/EF Core persistence wiring. Monthly bills and manual collection reports are separate approval workflows; online payment processing and deployment are not included.

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

3. Inspect the PrimeTect migrations only after confirming the connection string targets the dedicated `primetect` database. The migrations include `MonthlyBillingMilestone`, `PrintableBillingReports`, `ConfigurableElectricityWarningsAndReadingDates`, and `ManualCollectionReporting`; this repository does not apply them automatically:

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

### Local printable-report demo

For a disposable local demonstration, use a separate database such as `primetect_demo_print_reports_YYYYMMDD`; do not point the demo at `primetect`. Apply migrations with an explicitly targeted connection, then run the API in the `Demo` environment. `Demo` uses an ephemeral in-process Data Protection key provider only so local authentication does not depend on an inaccessible user key ring; production and normal Development environments keep the configured persistent provider.

```powershell
$demoDbName = "primetect_demo_print_reports_YYYYMMDD"
$env:ASPNETCORE_ENVIRONMENT = "Demo"
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__PrimeTectDatabase = "Host=127.0.0.1;Port=5432;Database=$demoDbName;Username=postgres;Password=<local-only-password>"
$env:AllowedOrigins__0 = "http://localhost:3000"
dotnet ef database update --project src/backend/PrimeTect.Infrastructure --startup-project src/backend/PrimeTect.Api --connection $env:ConnectionStrings__PrimeTectDatabase --no-build
dotnet run --no-build --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5000
```

In a second terminal, run `Set-Location src/frontend; npm.cmd run dev -- --hostname 127.0.0.1 --port 3000` and open `http://localhost:3000`. Stop each process with `Ctrl+C`; remove only the explicitly named demo database after verification if it is no longer needed. No email is sent by the demo, and invitation tokens must be shared manually.

### Prepared local collection demo

This checkout has a separate synthetic demo database prepared on 2026-10-10:
`primetect_demo_collections_20261010_a20fcfd3`. It is not `primetect` and was created from empty before all committed
migrations were applied. The demo contains an unequal Ground/Upper floor layout, a known tenant occupying two units,
an unnamed occupancy, an approved September 2026 billing cycle, an approved partial collection with a pending reversal,
and a pending collection report.

The local-only files are ignored by Git:

- `src/backend/PrimeTect.Api/appsettings.Demo.json` points the `Demo` environment at that database.
- `src/backend/PrimeTect.Api/demo-credentials.local.json` contains separate Admin, Building Manager, Building Staff,
  and Finance Manager login credentials. View it locally with
  `Get-Content src/backend/PrimeTect.Api/demo-credentials.local.json`; never copy its values into a tracked file.

The API is prepared at `http://127.0.0.1:5000` and its health endpoint is
`http://127.0.0.1:5000/api/health`. Start it from the repository root with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Demo"
dotnet run --no-build --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5000
```

In a second terminal, start the frontend with
`Set-Location src/frontend; npm.cmd run dev -- --hostname 127.0.0.1 --port 3000`, then open
`http://localhost:3000`. Stop a foreground server with `Ctrl+C`; for the background API first inspect
`Get-NetTCPConnection -LocalPort 5000 -State Listen` and confirm its owning process is PrimeTect.Api, then run
`Stop-Process -Id <confirmed-api-pid>` (the `dotnet run` host may also be stopped afterward). No email or SMS is sent.

Manual review checklist:

1. Log in as the Building Manager and confirm only the assigned property is shown. Open **Meter Readings** and
   **Collections**; the approved G-01 collection can request a reversal with a required reason, while the pending
   reversal states that the balance is unchanged.
2. Log in as the Finance Manager and open **Collections**. Review the U-01 pending receipt and the G-01 pending
   reversal. Return one with a reason, then resubmit it as the Building Manager; approve it as Finance Manager and
   confirm the confirmed/outstanding totals refresh. Do not double-click review actions.
3. Log in as Building Staff and confirm assigned operational screens remain available while property, tenant, charge,
   staff, bill-approval, and collection-approval controls remain unavailable.
4. Log in as Admin and confirm the organization-wide property/settings, bill-review, and collection-review screens.
5. Repeat at a narrow browser width and check keyboard focus, error text, and touch-target wrapping. Browser automation
   was unavailable during this checkpoint, so desktop/mobile visual verification remains pending.

### Fresh guided-entry demo

The current checkpoint also has a new isolated synthetic demo database:
`primetect_demo_guided_20261010_da3c89c`. It was created from empty and all seven committed migrations were applied
to it. It is separate from both `primetect` and the earlier collection demo. It contains unequal Ground/Upper floors,
mixed fixed and metered charges, inherited/custom/disabled examples, a named tenant occupying multiple units, an
unnamed occupancy, two consecutive approved billing months (September and October 2026), and one pending cash
collection report.

Ignored local files for this demo are:

- `src/backend/PrimeTect.Api/appsettings.GuidedDemo.json`, loaded by the `GuidedDemo` environment.
- `src/backend/PrimeTect.Api/demo-guided-credentials.local.json`, containing separate Admin, Building Manager,
  Building Staff, and Finance Manager accounts. View it locally with
  `Get-Content src/backend/PrimeTect.Api/demo-guided-credentials.local.json`; never copy its values into tracked files.

The prepared local URLs are `http://127.0.0.1:5000` (API),
`http://127.0.0.1:5000/api/health` (health), and `http://127.0.0.1:3000` (frontend). Start one API and one frontend
process only:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "GuidedDemo"
dotnet run --no-build --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5000
Set-Location src/frontend
npm.cmd run dev -- --hostname 127.0.0.1 --port 3000
```

Stop foreground processes with `Ctrl+C`. For the currently backgrounded API, first confirm the listener belongs to
PrimeTect.Api with `Get-NetTCPConnection -LocalPort 5000 -State Listen`, then stop only that confirmed process with
`Stop-Process -Id <confirmed-api-pid>`. Do not point either process at `primetect` or an earlier demo.

Bilingual manual checklist:

1. Log in as Building Manager/Building Staff. Switch **English / বাংলা** and confirm the selected October 2026
   property/month, unit, and unsaved reading input remain unchanged. Open **Meter Readings / মিটার রিডিং দিন**, enter
   a current reading for G-01, choose **Save & Next Room / সেভ করে পরের রুম**, and confirm the save message before moving on.
2. In **Collections / টাকা পাওয়া লিখুন**, select the approved October bill, choose **Full amount / পূর্ণ পরিমাণ** or
   **Other amount / অন্য পরিমাণ**, review the unit/month/method confirmation, and verify the existing pending receipt is
   shown as sent for approval, not paid.
3. Log in as Finance Manager. Confirm only the collection/reversal review queue is available; return a report with a
   reason or approve it independently after checking the exact unit and amount.
4. Log in as Admin. Confirm organization-wide settings, billing review, collection review, and **Roles & permissions**
   remain available. Confirm Building Staff cannot open property, tenant, charge, staff, or approval administration.
5. Repeat at desktop and narrow mobile widths. Check keyboard focus, validation/error states, and that no table causes
   horizontal page scrolling. Browser automation was unavailable for this checkpoint, so visual verification remains pending.

The billing month is the month in which the bill is issued. A February-issued bill uses the immediately preceding
approved period's current reading; observation readings remain date-only and the exact observation time is not currently
stored. Server save/submission audit timestamps remain separate.

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

The PostgreSQL-specific tests are opt-in and refuse ordinary application connection strings. They require `PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION` to target a local server (`localhost`, `127.0.0.1`, or `::1`) and the administrative database `postgres` or `template1`. The fixture then creates a random `primetect_verification_<guid>` database, applies all seven committed migrations from empty, runs the integration tests, and drops only that generated database. It never resets or drops `primetect` or any other database.

PowerShell example (use a local password only; do not put this value in a tracked file):

```powershell
$env:PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=<local-only-password>"
dotnet test src/backend/PrimeTect.Api.Tests/PrimeTect.Api.Tests.csproj --filter FullyQualifiedName~PostgresBillingVerificationTests --nologo --verbosity normal
Remove-Item Env:PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION
```

If the variable is absent, the PostgreSQL facts are reported as skipped with that reason. If it is supplied but the service is unavailable or the safety checks reject the connection, the facts fail with the exact blocker and no existing database is changed. They never fall back to InMemory storage. Before opening a test connection, the fixture verifies that the effective database name is the generated `primetect_verification_<guid>` target; `primetect` and any other existing database are rejected. The tests cover migration application, numeric precision, duplicate cycle constraints, stale versions, organization/property authorization, self-approval, atomic approval/audit failure, immutable snapshots, immediately preceding approved-period readings, missing-period/policy-gap blocking, approved unit/tenant/property statements, multi-unit tenant identity, unnamed tenant details, historical rate changes, zero baselines, and incomplete comparisons. The regular InMemory tests cover the broader workflow matrix, but cannot prove PostgreSQL-specific behavior.

## Current milestone

Collection review screens include the bounded reversal controls: the original approved collection, unit, amount, and
reason are shown; a submitting role can request or resubmit with a reason; an Admin or Finance Manager can return or
approve independently. Pending reversals leave confirmed balances unchanged, while approved reversals refresh the
review totals. Stale versions are surfaced as refresh-required errors and in-flight actions are disabled.

### Configurable permissions and guided staff entry

Admins use **Roles & permissions** to configure organization-wide permission sets for Building Manager, Building Staff, and Finance Manager. The settings are separate for viewing, editing, submitting, and approving. The server rejects unsafe dependencies (for example, edit without view, or submit without edit), preserves Admin access, audits before/after values, and rechecks the current setting on each request. Assigning a property is still required for non-Admin users.

The staff home presents the available actions as **Enter meter reading / মিটার রিডিং দিন** and **Report money received / টাকা পাওয়া লিখুন**. The persistent বাংলা / English switch translates the guided reading and collection labels, statuses, confirmations, validation, permission failures, stale-version messages, and save failures without clearing form input or selected property/month/room. The dedicated Meter Readings screen shows one metered room at a time. **Save & Next Room / সেভ করে পরের রুম** saves only that room through the existing billing draft endpoint and advances only after confirmed server success; it never submits or approves a month. Bangla digits are normalized to the same stored decimal values. A separate “Submit for review” action follows the existing billing lifecycle. Finance users see the collection/reversal review queue when those permissions are enabled.

Properties use the configured `TimeZoneId` when initializing today/month in the guided screen; new properties default to `Asia/Dhaka` for the Bangladesh demo. Live entry initializes the observation date from that property-local date when a reading is first entered. The date stays in the local form state on retries and later saves, and a server reload restores the persisted date; failed saves do not advance to another room. The persisted reading field remains date-only because the existing schema did not record observation times: **the exact observation time is not currently stored**. Server save/submission timestamps remain separate audit data. A date can be corrected through the existing returned-draft reason/audit path, and legacy missing dates remain unavailable rather than invented.

- Owner registration creates an organization and Admin account in one transaction.
- Identity-backed HttpOnly cookie authentication supports login/logout, secure one-time invitations, organization isolation, and assigned-property access. The legacy `Manager` enum is displayed as **Building Manager**. Organization Admins can configure separate view, edit, submit, and approve permissions for Building Manager, Building Staff, and Finance Manager; current defaults keep Managers/Staff on assigned-property reading and collection submission, Finance Managers on collection/reversal review, and Admins on organization-wide administration. Property assignment remains an additional restriction, dependency checks prevent unsafe combinations, and Admin access cannot be disabled. Permission and assignment changes are audited and checked on every request, so revocation takes effect on the next request.
- Apartment buildings, individual-room buildings, and shops support configurable floors and units, generated previews, unequal floor layouts, unit enable/archive state, and explicit occupancy status.
- Tenant records allow independently optional name and phone fields, multiple unit assignments, dated history, overlap prevention, and `Not connected` onboarding status.
- The frontend supports property and floor editing, occupancy history with move-in/move-out closure, property charge defaults, unit-level inherited/custom/disabled charge modes, decimal amounts, and effective billing months. Unsaved charge rows can be removed locally; saved rows use an explicit End charge action that writes a disabled effective-month history row without deleting earlier settings or independent unit overrides.
- Monthly billing supports one draft cycle per property/month, effective charge snapshots, fixed charges, metered electricity, explicit opening readings, approved-period carry-forward, configurable property and per-unit electricity rates, configurable inherited/custom/disabled low-bill warnings, missing-reading and policy-gap blocks, assigned operational submission, and Admin-only bill return/approval. Approved snapshots are immutable and are never labeled as collected.
- The dedicated Meter Readings menu reuses the monthly billing cycle and calculation endpoints. It provides property and month selection, floor filtering, unit search, desktop/mobile meter entry, reading dates, calculated consumption/rates/amounts, warning states, and the existing draft → submitted → returned → submitted → approved workflow. Fixed-charge-only units are omitted from the meter list. Property rate and warning defaults are edited on this screen; individual unit overrides remain under Properties → Unit charges.
- Approved billing snapshots can be printed as an individual unit bill, a known-tenant multi-unit statement, or a property summary grouped by historical floor. A monthly comparison report uses only approved snapshots, reports billed totals/charge differences and electricity consumption, marks missing or unapproved periods incomplete, and leaves zero-baseline percentage changes unavailable. A4 print styles support browser Print/Save as PDF; legacy approved cycles without property/floor snapshot fields are refused rather than filled from current records.
- Staff invitations are accepted through a secure one-time token form with expiry and reuse protection. Invitation tokens are shared manually because email delivery is not connected.
- Manual collection reporting is linked to one approved historical unit bill and its occupancy snapshot. Building Managers/Building Staff can submit full or partial reports with received date, method, and note. Reports move Pending → Returned → Pending → Approved; pending/returned reports do not reduce confirmed outstanding balances. Finance Managers or Admins independently approve or return them, and self-approval is rejected. Approved reports are immutable; a reversal is a separate reasoned workflow requiring independent approval. No bank/provider verification, automatic allocation across bills, rent invention, transfer to a new occupant, SMS, or email is claimed.
- Property setup and maintenance actions remain scoped by organization and role; only Admins manage structure, tenants, charges, staff roles, and assignments. Building Managers and Building Staff see only assigned properties and operational screens; Finance Managers see only assigned collection-review queues.

Monthly billing intentionally blocks unresolved policy cases instead of inventing allocations: mid-month tenant changes/partial-month charges, vacant-unit charges, and any cycle without a contiguous immediately preceding approved period require explicit review. Meter resets or replacements are not bypassed by the opening-reading field and need a separate documented workflow. Email delivery, email verification, two-factor authentication, billing collection/payment actions, SafeDocs integration, SMS, police actions, and deployment are not implemented.

### Meter reading month convention

The selected billing month is the month in which the bill is issued. Meter consumption is calculated from the immediately preceding approved period's current reading (or an explicitly entered opening reading when no approved predecessor exists) to the selected cycle's current reading. For example, a reading of 10020 on 1 January 2026 and 10040 on 1 February 2026 is 20 units on the February-issued bill; it is not silently treated as January usage. Reading dates are stored when supplied. Legacy rows without stored dates display “Reading date unavailable.” Missing approved periods remain a gap and never become zero or cumulative multi-month usage.

Electricity amount is rounded to two decimal places using the existing away-from-zero money rule. A low-bill warning is informational only and appears strictly below the effective threshold; the initial default is Tk200, so Tk199 warns while Tk200 does not. It never adds a minimum fee and does not block approval.

The EF Core migrations `InitialPrimeTect`, `MonthlyBillingMilestone`, `PrintableBillingReports`, `ConfigurableElectricityWarningsAndReadingDates`, `ManualCollectionReporting`, `ConfigurableOrganizationPermissions`, and `PropertyTimeZone` are committed for the dedicated PrimeTect database, but this repository does not apply migrations automatically. Their applied status is **unknown** until verified separately against the local `primetect` database before manual use. The opt-in PostgreSQL tests use a newly created disposable database; they do not change the applied-status claim for the local `primetect` database. Printable reports never substitute current property/floor/tenant data for missing historical snapshot fields.

## npm audit review

The current lockfile reports 9 findings: 3 moderate and 6 high, with no critical findings. They are in the frontend build dependency graph:

- `next@15.5.27` carries a vulnerable nested `postcss@8.4.31`; npm proposes `next@16.4.0`, a major upgrade. This is relevant to the Next build/server toolchain, but the reported PostCSS issue is not a browser runtime dependency.
- `tailwindcss@3.4.19` brings `chokidar`, `fast-glob`, `micromatch`, `braces`, `postcss-nested`, and `postcss-selector-parser`. The reported issues affect CSS scanning/parsing and build-time file/pattern processing, not shipped application records. npm proposes `tailwindcss@4.3.3`, a major Tailwind migration.
- The direct `postcss` package is already at `8.5.29`; the remaining PostCSS finding is the nested copy under Next.

No `npm audit fix --force` or blind major upgrade was applied. The recommended follow-up is a planned Next 16/Tailwind 4 migration with a visual regression pass, or a targeted dependency update when compatible releases are confirmed.
