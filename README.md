# PrimeTect Property Management

PrimeTect is a property-management platform for properties, tenants, maintenance, utilities, approvals, monthly billing, and manually reported collections.

This repository is intentionally standalone. It includes a branded Next.js frontend, a .NET Clean Architecture API, and PostgreSQL/EF Core persistence wiring. Monthly bills and manual collection reports are separate approval workflows; online payment processing and deployment are not included.

### Repair milestone: live-runtime checks and tenant assignment

The current local GuidedDemo API was inspected at `http://127.0.0.1:5000`.
Its database is the existing isolated GuidedDemo database, not `primetect`;
its migration history currently ends at `20261009221340_PropertyTimeZone`.
The `20261010091817_RoomMeterIssues` and
`20261010184036_FeedbackReportsAndNotifications` migrations are present in
source but are not applied to that existing database. The API now reports a clear
`503 Service Unavailable` for issue-backed room-overview/reporting requests
when that storage is absent; it does not apply the migration automatically.
Apply those migrations only to a newly created disposable/demo database before
using room/meter issue workflows. Do not point local setup at `primetect`.

Collection approval remains independent and server-enforced. A person who
submitted a collection report cannot approve it, including an Admin. The
review screen hides approval for the submitter and explains that another
authorized person must approve; return and history actions remain available.

The property tenant picker now distinguishes **Assign existing tenant**,
**Add tenant**, and **Occupancy without tenant details**. Existing tenants
are selected by stable ID and their current units are shown, so the same
tenant can be assigned to another eligible unit without matching by name or
phone. Name and phone remain independently optional, and occupancy history
continues to be closed rather than overwritten when a tenant moves out.

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
`primetect_demo_guided_20261010_da3c89c`. It is separate from both `primetect` and the earlier collection demo. The
currently running copy has the first seven migrations applied; `RoomMeterIssues` remains unapplied there. Use a new
disposable database for the complete ten-migration issue/feedback workflow. It contains unequal Ground/Upper floors,
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

### Fresh collection and room-overview demo

For the collection-entry, room/meter-issue, and full-property overview walkthrough, a new isolated synthetic database is
prepared at `primetect_demo_overviews_20261010_c8a2b8a`. All eight committed migrations were applied from an empty
database. It is separate from `primetect` and every earlier demo database; do not run migrations or seed commands against
those databases.

Ignored local files are:

- `src/backend/PrimeTect.Api/appsettings.CollectionDemo.json`, containing the local demo connection and CORS origins.
- `src/backend/PrimeTect.Api/collection-demo-credentials.local.json`, containing separate Admin, Manager, Staff, and
  Finance login credentials. View it locally with
  `Get-Content src/backend/PrimeTect.Api/collection-demo-credentials.local.json`; never copy or print its passwords.

The prepared API URL is `http://127.0.0.1:5001` and health is
`http://127.0.0.1:5001/api/health`. Start one API process using the ignored configuration and the existing local-only
`Demo` environment (which uses ephemeral Data Protection keys):

```powershell
$demo = Get-Content src/backend/PrimeTect.Api/appsettings.CollectionDemo.json | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = "Demo"
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__PrimeTectDatabase = $demo.ConnectionStrings.PrimeTectDatabase
$env:AllowedOrigins__0 = "http://127.0.0.1:3001"
$env:AllowedOrigins__1 = "http://localhost:3001"
dotnet run --no-build --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5001
```

In a second terminal, start one frontend process:

```powershell
Set-Location src/frontend
$env:NEXT_PUBLIC_API_BASE_URL = "http://127.0.0.1:5001"
npm.cmd run dev -- --hostname 127.0.0.1 --port 3001
```

Open `http://127.0.0.1:3001`. Stop foreground processes with `Ctrl+C`. If a background process is used, confirm its
listener first with `Get-NetTCPConnection -LocalPort 5001 -State Listen` and stop only the confirmed PrimeTect process.
No email, SMS, payment-provider, SafeDocs, QR, or OCR action is performed.

The fixture includes unequal Ground/Upper floors, five metered units plus fixed-charge-only U-04, a named tenant on two
units, an unnamed tenant, approved Tk200 electricity bills, unpaid/partially confirmed/pending/fully confirmed collection
examples, and acknowledged/open plus resolved room issues. Reading months demonstrate Missing, SavedDraft, Returned,
AwaitingReview, and Approved at the existing billing-cycle approval granularity. Pending reported money remains separate
from confirmed money, and approved bills remain labelled billed rather than collected.

Bilingual manual checklist:

1. Log in with each role from the ignored credential file. Confirm Staff cannot open the restricted annex, Finance cannot
   edit charges/readings or report issues, and Admin can see both properties.
2. As Manager or Staff, open **Meter Readings / মিটার রিডিং দিন** for `Overview Demo Tower`, select August 2026, and
   use **View all rooms / সব রুম দেখুন**. Confirm U-04 is **Not applicable**, not a missing reading, and inspect the
   issue indicator on G-01.
3. Open **Collections / টাকা পাওয়া লিখুন** for August 2026. Confirm G-01 is unpaid, G-02 is partially confirmed,
   U-01/U-02 have pending Tk199/Tk199.00 reports, U-03 is fully confirmed, and the totals reconcile to billed Tk1050,
   confirmed Tk250, pending Tk398, and outstanding Tk800.
4. As Admin, inspect the room/meter issue list: G-01 is acknowledged/open for follow-up and U-04 is resolved. Confirm
   issue reporting did not change any bill, reading, or collection.
5. Repeat at desktop and narrow mobile widths, test filters and validation, and confirm no table requires horizontal
   scrolling. Browser automation was unavailable during preparation, so visual verification remains pending.

### Fresh repaired-workflow recovery demo

The current repaired-workflow demo is a new isolated PostgreSQL database:
`primetect_demo_repaired_20261010_840ff5d8`. All eight committed migrations,
including `RoomMeterIssues`, were applied from an empty database. The existing
`primetect` database was not changed, and its migration status remains unknown.

The active local pair is:

- API: `http://127.0.0.1:5002` (`/api/health`)
- Frontend: `http://127.0.0.1:3002`
- Database: `primetect_demo_repaired_20261010_840ff5d8`
- Credential file: `src/backend/PrimeTect.Api/recovery-demo.credentials.secret.json`
- Configuration file: `src/backend/PrimeTect.Api/appsettings.RecoveryDemo.Local.json`

Both local files are ignored. View the credential file locally when needed;
do not paste its passwords into tracked files or messages. The demo contains
Admin, Manager, Building Staff, and Finance accounts; a two-unit named tenant,
an unnamed tenant, a former-occupant history, and an additional current unit
assignment for the named tenant. Ground has two units and Upper has three.

The January 2026 cycle is approved. February 2026 is a saved partial draft:
one applicable room has a saved reading and the remaining applicable rooms are
still missing. The demo includes a pending Staff collection report and a
separately approved Finance collection report. Room issues are Open,
Acknowledged, and Resolved, with audit history. U-02 is fixed-charge-only and
appears as Not applicable for meter readings. An explicit occupied-without-
details status is retained alongside the unnamed tenant assignment used by
the billing policy.

Port history: the older collection-demo instructions use 5001/3001, while the
existing GuidedDemo uses 5000/3000. Port 5001 is not the active repaired API;
5000 remains the earlier GuidedDemo pair whose database lacks RoomMeterIssues.
The fresh recovery pair uses 5002/3002 so those existing processes and data
are not terminated or changed.

To restart this pair after stopping only the confirmed 5002/3002 processes,
load the ignored configuration and use the current source build:

```powershell
$demo = Get-Content src/backend/PrimeTect.Api/appsettings.RecoveryDemo.Local.json | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = "Demo"
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__PrimeTectDatabase = $demo.connectionString
$env:AllowedOrigins__0 = "http://127.0.0.1:3002"
$env:AllowedOrigins__1 = "http://localhost:3002"
dotnet run --no-build --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5002
```

In a second terminal:

```powershell
Set-Location src/frontend
$env:NEXT_PUBLIC_API_BASE_URL = "http://127.0.0.1:5002"
npm.cmd run dev -- --hostname 127.0.0.1 --port 3002
```

Stop only the confirmed PrimeTect listeners with `Ctrl+C` for foreground
processes, or inspect `Get-NetTCPConnection -LocalPort 5002,3002 -State Listen`
before stopping background processes. No migration command is needed for this
already-migrated demo database.

Recovery-demo manual checklist:

1. Log in separately as Staff and Finance. Confirm Staff can open the January
   room overview and cannot approve its own pending collection; confirm Finance
   can approve the separate Staff report.
2. Open February meter readings. Confirm the saved G-01 reading, missing-room
   states, previous approved readings, and U-02 Not applicable status.
3. As Admin, inspect Open, Acknowledged, and Resolved room issues and their
   audit history. Confirm issue reporting does not alter readings or bills.
4. In tenant management, select Demo Family, verify G-01/G-02/U-03 current
   assignments, and verify the unnamed tenant and occupancy-history examples.
5. Repeat at a narrow mobile width and switch বাংলা / English. Browser
   automation was unavailable, so visual and accessibility verification remain
   pending.

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

The PostgreSQL-specific tests are opt-in and refuse ordinary application connection strings. They require `PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION` to target a local server (`localhost`, `127.0.0.1`, or `::1`) and the administrative database `postgres` or `template1`. The fixture then creates a random `primetect_verification_<guid>` database, applies all twelve committed migrations from empty, runs the integration tests, and drops only that generated database. It never resets or drops `primetect` or any other database.

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
- The dedicated Meter Readings menu reuses the monthly billing cycle and calculation endpoints. It provides one shared property/month context, floor filtering, unit search, desktop/mobile meter entry, reading dates, calculated consumption/rates/amounts, warning states, and the existing draft → submitted → returned → submitted → approved workflow. Fixed-charge-only units are omitted from the meter list. Property rate and warning defaults are edited from the authorized Property overview → Charge defaults action; individual unit overrides remain under Unit charges.
- Approved billing snapshots can be printed as an individual unit bill, a known-tenant multi-unit statement, or a property summary grouped by historical floor. A monthly comparison report uses only approved snapshots, reports billed totals/charge differences and electricity consumption, marks missing or unapproved periods incomplete, and leaves zero-baseline percentage changes unavailable. A4 print styles support browser Print/Save as PDF; legacy approved cycles without property/floor snapshot fields are refused rather than filled from current records.
- Staff invitations are accepted through a secure one-time token form with expiry and reuse protection. Invitation tokens are shared manually because email delivery is not connected.
- Manual collection reporting is linked to one approved historical unit bill and its occupancy snapshot. Building Managers/Building Staff can submit full or partial reports with received date, method, and note. Reports move Pending → Returned → Pending → Approved; pending/returned reports do not reduce confirmed outstanding balances. Finance Managers or Admins independently approve or return them, and self-approval is rejected. Approved reports are immutable; a reversal is a separate reasoned workflow requiring independent approval. No bank/provider verification, automatic allocation across bills, rent invention, transfer to a new occupant, SMS, or email is claimed.
- Property setup and maintenance actions remain scoped by organization and role; only Admins manage structure, tenants, charges, staff roles, and assignments. Building Managers and Building Staff see only assigned properties and operational screens; Finance Managers see only assigned collection-review queues.

Monthly billing intentionally blocks unresolved policy cases instead of inventing allocations: mid-month tenant changes/partial-month charges, vacant-unit charges, and any cycle without a contiguous immediately preceding approved period require explicit review. Meter resets or replacements are not bypassed by the opening-reading field and need a separate documented workflow. Email delivery, email verification, two-factor authentication, billing collection/payment actions, SafeDocs integration, SMS, police actions, and deployment are not implemented.

### Collection entry, issue reports, and room overviews

Collection entry uses the current reportable remaining balance: approved billed amount minus confirmed collections net of approved reversals. Pending or returned reports remain visible but do not reduce that balance; a full receipt means the current remaining amount, not the original bill total. Whole and two-decimal values such as `199`, `199.00`, and `198.01` are accepted. API validation is rendered as plain UI messages, and failed submissions keep the entered value.

Meter Readings has a separate **Report a room/meter issue / রুম বা মিটারের সমস্যা জানান** action. It stores the assigned property/unit, issue type, description, property-local observed date, reporter, server timestamp, status, and audit history without saving or changing a reading, bill, or collection. Staff can report assigned-unit issues; Admins or explicitly enabled reviewers can acknowledge or resolve them. Damaged-meter reports never fabricate readings or reset meters.

**Feedback / Report** is the simplified reporting action. A unit-screen
report carries the stable property and unit ID into a read-only context and
asks only for a note. A home-screen report requires an authorized location:
either a permitted unit or **Building/Common Area**; it never attaches a
building-wide note to an arbitrary unit. The server supplies the submission
timestamp, which is displayed in the property's timezone. There is no
editable date or category in this flow, and the note remains in the form if
the request fails. The list is titled **Feedback & Reports**; financial
reporting remains **Billing Reports**. Existing categorized issue records
retain their historical category and observation date.

Only authorized recipients receive feedback notifications. A notification
identifies the unit when applicable (for example, **New report · Room 6B**),
and opening it marks it read without resolving the report. No email or SMS
delivery is claimed.

### Fresh feedback and notification demo

A new isolated synthetic demo was prepared for the Feedback / Report and
recipient-scoped notification workflows:

- Database: `primetect_demo_feedback_20261011_9a72c4e1`
- API: `http://127.0.0.1:5013`
- Frontend: `http://127.0.0.1:3013`
- Migrations: all eleven current migrations, including
  `FeedbackReportsAndNotifications` and `FeedbackSubmissionIdempotency`, were
  applied from an empty database to this new database only.
- Local configuration: `src/backend/PrimeTect.Api/appsettings.FeedbackDemo.Local.json`
- Local credentials: `src/backend/PrimeTect.Api/feedback-demo.credentials.secret.json`

Both local files are ignored. The credentials file contains separate Admin,
Admin reviewer, Manager, Staff, and Finance accounts; passwords are never
stored in tracked files or printed in this documentation. The Finance account
is assigned only to the second property and is not granted room-feedback
review permission.

The demo contains properties with different assignments, rooms 2A and 6B, a
named tenant occupying multiple units, an unnamed tenant, a saved Draft meter
cycle, an Approved fixed-charge cycle, unit and Building/Common Area feedback,
recipient-specific read/unread notification copies, and acknowledged/resolved
feedback examples. The seed was performed through the existing HTTP APIs.

To start the API without copying a password into a tracked file, derive its
connection string in memory from the existing ignored GuidedDemo configuration
and target only the named demo database:

```powershell
$demo = Get-Content src/backend/PrimeTect.Api/appsettings.FeedbackDemo.Local.json | ConvertFrom-Json
$source = (Get-Content src/backend/PrimeTect.Api/appsettings.GuidedDemo.json | ConvertFrom-Json).ConnectionStrings.PrimeTectDatabase
$target = $source -replace '(?i)(Database|Initial Catalog)\s*=\s*[^;]+', "Database=$($demo.database)"
$env:ASPNETCORE_ENVIRONMENT = "Demo"
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__PrimeTectDatabase = $target
$env:AllowedOrigins__0 = $demo.frontendUrl
dotnet run --project src/backend/PrimeTect.Api --no-build --urls $demo.apiBaseUrl
```

In a second terminal, start the matching frontend:

```powershell
$demo = Get-Content src/backend/PrimeTect.Api/appsettings.FeedbackDemo.Local.json | ConvertFrom-Json
Set-Location src/frontend
$env:NEXT_PUBLIC_API_BASE_URL = $demo.apiBaseUrl
npm.cmd run dev -- --hostname 127.0.0.1 --port 3013
```

The prepared pair is currently running. Shut down only the confirmed PrimeTect
API/frontend process IDs for this pair; do not stop unrelated listeners. The
local `primetect` database was not used or changed, and its migration status
remains **unknown**.

Manual feedback/notification checklist:

1. Sign in as Staff, select the Feedback / Report action from room 6B, confirm
   the property and room are read-only, enter only a note, send it, and open
   **View My Reports**.
2. Sign in as Manager or Admin, open the bell, confirm the notification says
   **New report - Room 6B**, then read it. Confirm only that recipient's copy
   becomes read and reading it does not acknowledge or resolve the report.
3. Acknowledge one report and resolve the common-area report with an authorized
   reviewer; confirm the submitter sees the updated status and audit history.
4. From the home report action, choose **Building/Common Area** and send a note;
   confirm it has no unit assignment. Check loading and failed-save messages.

Browser automation was unavailable during this verification, so desktop,
mobile-width, and Bangla visual checks remain pending.

Meter and collection screens include a property/month **View all rooms** overview. Every accessible enabled unit appears once; fixed-charge-only units are marked Not applicable and are not counted as missing. Reading filters distinguish missing, saved draft, awaiting review, returned, approved, and reported issues. Collection rows distinguish no approved bill, pending reports, confirmed collections, pending reversals, and confirmed outstanding balances; historical occupancy references are not transferred.

### Meter reading month convention

The selected billing month is the month in which the bill is issued. Meter consumption is calculated from the immediately preceding approved period's current reading (or an explicitly entered opening reading when no approved predecessor exists) to the selected cycle's current reading. For example, a reading of 10020 on 1 January 2026 and 10040 on 1 February 2026 is 20 units on the February-issued bill; it is not silently treated as January usage. Reading dates are stored when supplied. Legacy rows without stored dates display “Reading date unavailable.” Missing approved periods remain a gap and never become zero or cumulative multi-month usage.

Electricity amount is rounded to two decimal places using the existing away-from-zero money rule. A low-bill warning is informational only and appears strictly below the effective threshold; the initial default is Tk200, so Tk199 warns while Tk200 does not. It never adds a minimum fee and does not block approval.

The EF Core migrations `InitialPrimeTect`, `MonthlyBillingMilestone`, `PrintableBillingReports`, `ConfigurableElectricityWarningsAndReadingDates`, `ManualCollectionReporting`, `ConfigurableOrganizationPermissions`, `PropertyTimeZone`, `RoomMeterIssues`, `FeedbackReportsAndNotifications`, `FeedbackSubmissionIdempotency`, `QrMeterPhotosAndLocations`, and `UtilityAccountsMainMetersAndSubmeters` are committed for the dedicated PrimeTect database, but this repository does not apply migrations automatically. Their applied status is **unknown** until verified separately against the local `primetect` database before manual use. The opt-in PostgreSQL tests use a newly created disposable database; they do not change the applied-status claim for the local `primetect` database. Printable reports never substitute current property/floor/tenant data for missing historical snapshot fields.

### Simplified workspace navigation

The daily staff/manager landing screen is **My Work / আমার কাজ**. It keeps one property and billing-month context visible and shows only task cards allowed by the effective permissions: **Enter Meter Reading / মিটার রিডিং লিখুন**, **Report Money Received / পাওয়া টাকার হিসাব দিন**, and **Feedback / Report / ফিডব্যাক / রিপোর্ট**. **View All Rooms / সব রুম দেখুন** is secondary. Multiple assigned properties must be selected explicitly; the UI never silently chooses the first property. Reading progress is an applicable-metered-unit count, not a count of visible filtered rows.

The focused room screen saves one reading through the existing draft endpoint and advances only after the server confirms success. It does not submit a month. **Submit for review** is available only in the room overview/monthly submission area, where the server's existing blockers and approval granularity remain authoritative. Collection reporting is separate from review/history: a submitted receipt is **Pending approval**, while only an approved report is **Confirmed received**.

Authentication offers **Log in**, **Create account / Register**, and **Accept staff invitation**. Public registration creates a new organization and its first Admin through the existing owner-registration endpoint; it cannot select an existing organization or privileged staff role. Staff join an existing organization only with the secure one-time invitation flow. Invitation/email delivery is not implemented, so an Admin must share the token manually. Successful owner registration opens the existing property setup workflow; it does not create demo properties or tenants.

The signed-in home uses the effective organization permissions returned by `/api/auth/me`; role names are labels, not
authorization. Staff and Building Managers get the permitted task cards for **Enter meter reading / মিটার রিডিং লিখুন**,
**Report money received / পাওয়া টাকার হিসাব দিন**, and **Report a problem / সমস্যা জানান**, with **View all rooms / সব
রুম দেখুন** as a secondary action. Finance users get separate collection and reversal review tasks. Admins retain the
property overview, billing/collection review, setup, people, permissions, and report access. Property assignment remains
an additional server-enforced restriction.

Meter entry uses one shared property and billing-month context. **Save & Next Room** persists one room only and advances
after the API confirms success; saving never submits the month. Monthly submission is a separate cycle-summary action and
continues to use the existing server eligibility and approval granularity. Collection reporting starts with a blank amount,
shows billed/confirmed/pending/remaining values, and sends one full or partial receipt for independent approval. Pending
receipts are never called paid or confirmed.

Short manual usability checklist:

1. Enter three room readings, switch English / বাংলা, and confirm the selected property, month, room, reading, and
   observation date remain unchanged. Save one room and confirm the success message before the next room opens.
2. Report a damaged meter from the separate problem action without entering a fabricated meter reading; confirm the
   issue report does not change billing data.
3. Report a partial collection against one approved bill, then review it with a different authorized account. Confirm
   pending, confirmed, and outstanding amounts remain distinct.
4. Open **View all rooms** and find a missing reading and an outstanding balance; confirm non-metered units are shown as
   Not applicable rather than missing.
5. Check a returned reading/report, a failed save, keyboard focus, and a narrow mobile width. Browser automation is not
   part of the repository test command, so visual behavior still requires manual browser inspection.

The frontend's dependency-free `npm.cmd run test:ui` covers room-advance, failed-save draft preservation, Bangla numeric
normalization, and current-remaining-balance amount rules. It runs separately from the production build.

### Dashboard polish and verification notes

The simplified staff/manager home now keeps the selected property and billing month in one shared context. The property picker is hidden when only one assigned property is available. Reading progress is loaded from the existing `meter-overview` endpoint and counts only applicable metered units; saved drafts, submitted cycles, returned corrections, and approved cycles remain separate. **View all rooms** and **Open unfinished rooms** keep the selected property and month rather than falling back to the first property.

### Mobile staff dashboard and electricity entry

**My Work / আমার কাজ** is a mobile-first staff dashboard with the existing
notification bell and persistent language switch, explicit property/month
selection, permission-driven Electricity Reading, Report Money Received,
Feedback / Report, and View All Rooms actions, plus Home / Rooms / Account
bottom navigation on narrow screens. The bottom navigation stays available on
the staff meter-reading and collection task screens, not only on Home. It does
not select a first property when more than one assignment is available.
Reading progress uses the existing meter overview: only applicable metered
rooms count, loading/errors never show invented zeroes, and unfinished opens
the filtered room overview with the same property/month context.

Reading navigation is grouped as **Utility Bills / ইউটিলিটি বিল** with the
active **Electricity / বিদ্যুৎ** workflow. Fixed water, gas, service charges,
and billing reports remain existing billing features; this does not introduce
water/gas meter entry.

The focused electricity room screen keeps the existing lifecycle and photo/QR
protections while showing a large room heading and month context, a large bold
previous reading with its distinct approved, opening, or unavailable state and
the available previous reading date, a large decimal-capable current input,
optional meter photo/OCR evidence, calculation details, warning, and Save &
Next Room. A room QR identifies a room; a meter photo is separate evidence and
the two are labelled separately. OCR remains an explicit editable suggestion
and no photo or suggestion can save, submit, approve, or bill automatically.
A meter photo requires the explicit reading confirmation before saving, and a
failed save never advances to the next room. Monthly submission remains only
in View All Rooms and continues to rely on server eligibility checks.

The Admin home uses the existing per-property collection, meter-overview, and room/meter-issue endpoints. It summarizes approved billed totals, approved manual receipts net of approved reversals, confirmed outstanding balances, and pending reported receipts separately. Missing or unapproved billing coverage is labeled incomplete rather than treated as zero. Because there is no aggregate dashboard API in this milestone, the Admin overview loads one aggregate request per property (not one request per unit); larger organizations may need a future server-side aggregate endpoint for faster loading. Property-specific attention buttons always identify the property before opening rooms, reviews, or issues.

The UI tests cover the applicable-unit denominator, distinct draft/review/approved states, permission-driven staff task cards, workflow-context preservation across language changes, distinct approved/opening/unavailable previous-reading labels, explicit meter-photo confirmation, save-before-advance behavior, failed-save draft preservation and no-advance-on-failure, Bangla numeric normalization, monthly-submission placement, and current remaining-balance rules. Run checks sequentially:

```powershell
Push-Location src/frontend
npm.cmd run test:ui
npm.cmd run typecheck
npm.cmd run build
Pop-Location
```

For the existing local GuidedDemo API, verify that the API process is listening on `http://127.0.0.1:5000` and that its ignored `src/backend/PrimeTect.Api/appsettings.GuidedDemo.json` is the intended local configuration before starting a frontend. The current database is missing `RoomMeterIssues` and `FeedbackReportsAndNotifications`, so issue/feedback-backed room overview/reporting remains migration-blocked until a fresh disposable/demo database is prepared. The frontend can use the API directly:

```powershell
Push-Location src/frontend
$env:NEXT_PUBLIC_API_BASE_URL = "http://127.0.0.1:5000"
npm.cmd run dev -- --hostname 127.0.0.1 --port 3000
Pop-Location
```

Open `http://127.0.0.1:3000`. Restart the API from the current build when you need these source changes live. Credentials remain in the ignored local demo credential file documented earlier in this README and are never printed or committed.

Manual usability checklist:

1. As a staff/manager user, confirm the three permitted task cards, property/month context, applicable-unit progress, **View all rooms**, unfinished-room shortcut, and no arbitrary first-property fallback.
2. Enter one reading, switch বাংলা / English, and confirm the room, month, input, and observation date remain unchanged. Save and confirm the success response before moving to the next room.
3. Report a partial collection and confirm the selected bill, remaining amount, pending report, and confirmation text stay distinct.
4. Report a room/meter problem without entering a reading, then open **View all rooms** and confirm the issue indicator and non-metered **Not applicable** rows.
5. As Finance, review/return a collection; as Admin, inspect the property overview, incomplete coverage notice, billing review, collection review, issue view, and responsive layout at a narrow width.

## Browser verification (2026-10-12)

Automated UI verification ran against the running QR/photo demo pair with
Playwright (Chromium 156, headless, one worker, checks run sequentially). The
frontend on `http://127.0.0.1:3014` served current `main` source with
`NEXT_PUBLIC_API_BASE_URL=http://127.0.0.1:5014`, and the API on
`http://127.0.0.1:5014` served the current backend build against only the
synthetic demo database `primetect_demo_qr_photo_20261011_af21907c`. No
migration was applied and no demo data was changed. The frontend process on
3014 was restarted once because an earlier production build had replaced its
`.next` dev cache; only that confirmed PrimeTect process was stopped.

All 17 checks passed:

- Staff **My Work** dashboard at 1440x900 and 390x844: four permission-driven
  cards, one explicit property/month context, notification-bell and language
  controls, and bottom navigation hidden on desktop and visible on mobile with
  48px targets and no horizontal overflow.
- Permission-driven surfaces: Finance sees only the collection/reversal review
  queue and no Electricity Reading card; the notification bell renders only for
  recipients with review permission (Admin) and is hidden for the demo Staff
  account, which has no review permission.
- Two assigned properties (synthetic mock of the properties response) require an
  explicit selection: the placeholder stays empty and the task card does not
  navigate until a property is chosen.
- Property and billing month are preserved when moving between My Work and the
  Electricity Reading screen and back.
- Switching বাংলা/English mid-entry keeps the typed reading; Bangla text wraps
  with 0px horizontal overflow at 390px.
- The previous reading shows the large bold value (`10020 kWh`, 36px/900) with
  the correct **Previous approved reading** (2026-11), **Opening reading**
  (manager, Annex) and **Previous reading unavailable** (`—`) states.
- Decimal and Bangla-digit entry (`10020.5`, `১০০২০.৫`) normalize to `10020.5`.
- Meter photo upload (synthetic), explicit **Use suggestion**, manual
  correction, confirmation reset, and save blocking without confirmation were
  exercised; the upload and the successful save were fulfilled by synthetic
  request interception so the demo database was not written. A real failed save
  (aborted request) preserved the input and did not advance, and the synthetic
  successful save advanced without any unsaved-change prompt.
- **Monthly submission** is hidden on My Work and in the focused room screen and
  appears only after **View all rooms** is opened.
- Staff collection reporting keeps its own report tab while the **Review
  reports** approval tab stays disabled for the Staff account.
- The Feedback / Report form keeps the current room as a locked **selected from
  this room** context.

Screenshots from the synthetic demo are under `%TEMP%\pt-verify\shots`
(workstation-local; not committed).

Defects fixed in this milestone:

- The electricity entry's room QR affordance read like an inactive scanner. It
  now shows the honest bilingual instruction **Use your phone camera to scan the
  room QR / রুম QR স্ক্যান করতে ফোনের ক্যামেরা ব্যবহার করুন।** and is not styled
  as a control.
- The full workspace header did not wrap at narrow widths and overflowed
  horizontally in Bangla (up to 180px at 390px). It now wraps with mobile
  padding, so Bangla and English both render without horizontal overflow.
- Monthly submission was rendered under the reading screen even before **View
  all rooms** was opened; it now appears only after the overview is opened.

Blocked and not verified here:

- Real-phone camera capture, QR scanning on another device, and real-meter OCR
  accuracy still require a reachable device and a real meter.
- Real writes to the demo (a server Save & Next, a month submission, and a real
  photo upload) were replaced with synthetic request interception so the
  existing demo database stayed unchanged; the guarded disposable-database
  workflow was not invoked because no backend persistence or contract changed.

## Utility accounts, shared main meters, and safe submeter replacement

This milestone separates the shared incoming electricity supply from the room billing
submeters and adds a safe replacement workflow. It is additive: units without a submeter
installation keep their existing behaviour, and customer/account details are never required
for ordinary room billing.

### Data model and setup

- **Utility account** (Admin): provider name, customer/account number (stored as text so
  leading zeros survive), and an optional display label, scoped to one property.
- **Shared main meter** (Admin): a meter number (text), optional label and install date,
  linked to one utility account, with an active/ended history. One main meter can supply
  many rooms.
- **Room submeter installation** (Admin): one active electricity submeter per unit, with an
  optional serial number and display label, an optional parent main meter, an installation
  date, an opening reading/date, and an ordered replacement history. A room submeter may be
  saved without a known parent main meter.
- Existing units are adopted explicitly: leaving the serial/installation date blank records
  the identity as **unknown** rather than inventing one, and adoption reuses the valid
  baseline without resetting consumption.

Setup lives under **Utility Bills → Electricity → Meter Setup** and is Admin-only
(`/api/properties/{id}/meter-setup`, `.../utility-accounts`, `.../utility-main-meters`,
`.../meter-installations`). Customer/account numbers are never returned by public QR
responses.

### Replacement calculation

A replacement atomically ends the previous installation (recording its final reading when
available) and creates the replacement installation with its opening baseline, plus an
audit event. Consumption is calculated **per installation segment** and never by subtracting
across two meters:

- Old segment: 10020 → 10040 = 20 units.
- New segment: 35 → 50 = 15 units.
- Combined period: 20 + 15 = 35 units, only when both segments are fully recorded.

A new meter may start at `0` or any other non-negative value; zero is stored distinctly
from a missing opening. Rates, rounding, warning logic, review workflow, and immutable
approved snapshots are reused unchanged; no prorated tariff is introduced. Approved billing
statements and the cycle response now also carry the replacement segments and the submeter
label.

### Guardrails and unresolved periods

- Replacements are **Admin-only**, organization/property scoped, concurrency-guarded
  (`expectedInstallationVersion`), and idempotent (`submissionKey`).
- A replacement that would reinterpret an approved month is rejected, and a **submitted**
  month must be returned for correction first. A draft/returned month is recalculated and
  requires re-review.
- One active submeter per unit is enforced by a filtered unique index; overlapping or
  backdated installations are rejected.
- If the previous meter's final reading is **unavailable**, the replacement and the new
  baseline are preserved but the affected period is review-blocked with a clear message in
  staff and admin views. No consumption is estimated, assumed zero, or approved.
- Old readings, photos, bills, and assignments are never deleted or overwritten.
- Readings and photos saved for a room are associated with the submeter installation in
  effect when captured. After a replacement, a photo captured for the previous meter is
  rejected with a recoverable message instead of being attached to the replacement.

### Verification (2026-10-12)

- Frontend: `npm run test:ui` (14 passed), `npm run typecheck`, `npm run build`.
- Backend: `dotnet build -c Release`; `dotnet test -c Release` (35 passed, 10 skipped).
- Guarded PostgreSQL suite (`PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION` targeting a local
  `postgres` admin database): 10 passed. The fixture creates a random
  `primetect_verification_<guid>` database, applies all **twelve** committed migrations, and
  drops only that database. It includes a real HTTP replacement workflow proving two
  persisted segments (20 + 15 = 35) and the unique active-submeter index.
- No existing demo serves the new version: the running demo API is an older build and its
  database has not had the new migration applied, so it is deliberately not restarted
  against the unapplied schema. The frontend dev server serves the current source but talks
  to that older API.

The new migration is `20261011213752_UtilityAccountsMainMetersAndSubmeters`. Do not apply it
to `primetect` or an existing demo; use a fresh disposable database.

## Fresh utility-metering demo verification (2026-10-12)

A new isolated synthetic PostgreSQL database was created for this milestone:
`primetect_demo_metering_20261012_ae2595d9`. It is not `primetect` and not an
earlier demo. All **twelve** committed migrations were applied to it only
(verified: 12 applied, 0 pending), including
`20261011213752_UtilityAccountsMainMetersAndSubmeters`.

Ignored local files:

- `src/backend/PrimeTect.Api/appsettings.MeteringDemo.Local.json` — derived
  connection source/database name, ports, photo root, public base URL, and
  allowed origins.
- `src/backend/PrimeTect.Api/metering-demo.credentials.secret.json` — synthetic
  Admin, Staff, and Finance credentials. Do not print, commit, or copy the
  passwords. View locally with
  `Get-Content src/backend/PrimeTect.Api/metering-demo.credentials.secret.json`.
- `src/backend/PrimeTect.Api/App_Data/metering-demo-*` — private synthetic photo
  storage (ignored, not served as static content).

Verified running pair:

- Frontend: `http://127.0.0.1:3021`
- API: `http://127.0.0.1:5021` (`/api/health`)

The API runs the current Release build with `ASPNETCORE_ENVIRONMENT=Demo`
(ephemeral data-protection keys) against only the new database. The frontend runs
the current source from an isolated copy under `%TEMP%\primetect-fe-3021` so the
pre-existing `3014`/`5000` demos keep their own `.next` cache.

Synthetic content: one utility account with customer number `0012345` (leading
zero preserved), one main meter `00998877` supplying eight rooms, a standalone
room submeter without a parent main meter, one legacy submeter whose identity is
unknown, a January 2026 approved baseline, and a February 2026 draft showing a
replacement opening at `35` and at `0` (each a complete 20 + 15 = 35-unit
two-segment period) plus one replacement whose old final reading is unavailable
and therefore review-blocks the period. A second property (`Metering Demo Annex`)
has a February cycle left **Submitted** to exercise the submitted-month guard.
Separate Admin, Building Staff, and Finance Manager accounts are created.

### Startup / shutdown

Restart the API (derive the connection in memory; never print the password):

```powershell
$local = Get-Content src/backend/PrimeTect.Api/appsettings.MeteringDemo.Local.json | ConvertFrom-Json
$source = [string](Get-Content (Join-Path src/backend/PrimeTect.Api $local.ConnectionSource) | ConvertFrom-Json).ConnectionStrings.PrimeTectDatabase
$env:ASPNETCORE_ENVIRONMENT = 'Demo'
$env:Database__Provider = 'PostgreSQL'
$env:ConnectionStrings__PrimeTectDatabase = ($source -replace '(?i)(Database|Initial Catalog)=[^;]+', "Database=$($local.ConnectionDatabase)")
$env:AllowedOrigins__0 = [string]$local.AllowedOrigins[0]
$env:AllowedOrigins__1 = [string]$local.AllowedOrigins[1]
$env:PublicAppBaseUrl = [string]$local.PublicAppBaseUrl
$env:MeterPhotos__RootPath = Join-Path (Resolve-Path src/backend/PrimeTect.Api).Path ([string]$local.MeterPhotos.RootPath)
dotnet run --no-build -c Release --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5021
```

Restart the frontend from the isolated copy:

```powershell
$copy = Join-Path $env:TEMP 'primetect-fe-3021'
$env:NEXT_PUBLIC_API_BASE_URL = 'http://127.0.0.1:5021'
Set-Location $copy
npm.cmd run dev -- --hostname 127.0.0.1 --port 3021
```

Stop only the confirmed PrimeTect `5021`/`3021` processes
(`Get-NetTCPConnection -LocalPort 5021,3021 -State Listen`). Do not stop the
pre-existing `5014`/`3014` demo or the unrelated port-3000 app. The demo database
is a fresh disposable database; remove it only if it is no longer needed.

### Stale-photo HTTP test and defect fixed

An HTTP integration test now uploads a real synthetic meter photo through the
actual endpoint, replaces the submeter with an authorized Admin, attempts to save
the stale reading/photo context, and verifies rejection with no change to
readings, bills, photo associations, or history, then uploads a fresh photo and
saves successfully. It runs both in the default InMemory suite and in the guarded
PostgreSQL suite (real persistence, isolated private photo storage that the test
deletes). This exposed a real defect: the in-transaction draft recalculation
during replacement queried storage before the new installation was persisted, so
the replaced room kept the old meter's previous reading (and a later save
reported negative consumption). The replacement now persists the ended and new
installations inside the transaction before recalculating the affected draft. A
second fix lets an adopted submeter whose identity/opening is unknown accept the
opening reading entered through the billing entry instead of silently blocking.

### Browser verification (2026-10-12)

Playwright (one worker, sequential) ran seven real-persistence checks against
only the new `3021`/`5021` demo — no successful mutation responses were mocked:

1. Admin meter setup shows the utility account `0012345` (leading zero), the main
   meter `00998877`, and the unknown-identity legacy submeter.
2. Replacement segments persist: U-01 and U-02 each show two segments
   (20 + 15 = 35), U-02's replacement opening is `0`, and U-03 is review-blocked
   with the missing-final-reading message; the month submission button is
   disabled.
3. A staff reading save persists and reloads (G-01 = 13025, 5 units).
4. Staff cannot administer: no Meter Setup in the UI, and `/meter-setup` and the
   replace endpoint return 403.
5. Billing review is separate: Admin sees the review area, Staff sees no approve
   action, Finance is review-only, and the approved January statement is
   unchanged (real HTTP).
6. A submitted-month replacement is rejected with a clear **bilingual** message
   (English and Bangla).
7. Admin setup and the staff dashboard wrap at 390px with no horizontal overflow
   in English and Bangla.

Screenshots are under `%TEMP%\pt-verify\metering-shots` (workstation-local).
Desktop browser automation does not verify a real-phone camera, external QR
scanning, or real-meter OCR accuracy; those still require a real device and meter.

## Field-testing milestone: staff readings, QR, photos, collections, feedback (2026-10-13)

A fresh isolated field-test database was created:
`primetect_fieldtest_20261012_b09e075b` (not `primetect` or an earlier demo; all
twelve migrations verified applied, 0 pending). Ignored local files:

- `src/backend/PrimeTect.Api/appsettings.FieldTest.Local.json`
- `src/backend/PrimeTect.Api/metering-fieldtest.credentials.secret.json`
- `src/backend/PrimeTect.Api/App_Data/field-test-b09e075b` (private photo storage)

Verified running pair: frontend `http://127.0.0.1:3031`, API
`http://127.0.0.1:5031` (`/api/health`). The API runs the current Release build
with `ASPNETCORE_ENVIRONMENT=Demo`; the frontend runs the current source from an
isolated copy (`%TEMP%\primetect-fe-3031`) so no shared Next.js cache is
disturbed. Seed content: `Field Test Tower` (6 units including a fixed-only unit,
ordinary and replaced submeters, approved January, incomplete/blocked February),
`Field Test Review` (clean submit/return/correct/approve month),
`Field Test Annex` (unassigned) and `Field Test Offsite` (legacy unit, missing
period); separate Admin, Building Staff, and Finance accounts; active, revoked,
and unassigned-property QR links.

### Real persisted browser workflows (20 checks, one worker)

All checks ran against the live 3031/5031 pair with **no mocked successful
mutation responses**; every mutation was confirmed through an API/database-backed
reload. Passed: explicit property/month selection; QR resolution while signed in
and after logging in from the link; safe local return navigation that keeps the
destination and clears the `meterLink` query; revoked QR and unassigned-property
access denied (404/403); decimal and Bangla-digit entry (`3020.5`, `৩০২০.৫`);
replacement openings/segments (U-02 20 + 15 = 35; U-01 opening 7 blocked); real
synthetic photo upload -> preview -> OCR outcome -> manual correction ->
confirmation reset -> save -> authorized retrieval (Staff/Admin 200, Finance 403,
cross-org 404); a saved photo cannot be reused for another room (400); a failed
request preserves the input and does not advance; successful Save & Next persists
and advances once; refresh shows the value and progress; missing readings, missing
final readings, and missing periods remain review-blocked; submit -> return ->
correct -> resubmit -> approve with separate users (staff self-approval 403);
approved readings immutable (409); a partial collection report pends, Finance
approval updates confirmed/outstanding totals, and self-approval is rejected;
room-context feedback reaches the reviewer bell; unauthorized accounts cannot
reach photos/admin/other-property data; Bangla, 390px mobile, keyboard focus,
touch targets, and bottom-navigation overlap.

**Real versus synthetic in this run:** the photo is a synthetic canvas PNG
normalized by the real private-storage pipeline; the local OCR returned *no
suggestion* for that synthetic image, so **explicit adoption via "Use
suggestion" was not exercised by real OCR in this run**. Manual entry,
correction, confirmation reset, save, and authorized/denied retrieval were all
exercised for real. The failed-request check is a network interruption, **not**
offline support. Exact observation time is still not stored.

### Fixes in this milestone

- A room QR link had no way back: a dismissible banner now shows the destination
  and provides **Back to My Work**, which clears the `meterLink` query locally
  (no external redirect) while preserving the selected property/month.
- The staff home shell did not display global errors, so a revoked or
  unauthorized QR link failed silently; the shell now shows the success/error
  notice.

### Real-phone testing preparation (not performed here)

This checkout binds to loopback only. `127.0.0.1` on a phone means the phone
itself. To test on a phone on the same trusted Wi-Fi, an operator must do the
following (these are **not** done and require explicit authorization):

1. Find the workstation LAN IPv4 (`ipconfig`).
2. Bind the API and frontend to the LAN interface with matching origins:
   - API: `--urls http://0.0.0.0:5031` (or `http://<LAN-IP>:5031`) and
     `AllowedOrigins__0=http://<LAN-IP>:3031`.
   - Frontend: `npm.cmd run dev -- --hostname 0.0.0.0 --port 3031` with
     `NEXT_PUBLIC_API_BASE_URL=http://<LAN-IP>:5031`.
   - Set `PublicAppBaseUrl=http://<LAN-IP>:3031` before generating QR labels so
     the label URL is phone-reachable.
3. Allow inbound TCP 3031/5031 in the Windows firewall (not changed here).
4. Open `http://<LAN-IP>:3031` in the phone browser and sign in. Cookies are
   HttpOnly with `SameSite=Lax`; unsafe requests need the CSRF token from
   `/api/auth/csrf` sent as `X-CSRF-TOKEN`, which the app handles automatically.
5. **In-browser camera access requires a secure context.** `https://<LAN-IP>`
   needs a trusted certificate installed on the phone (not done here); plain
   `http://<LAN-IP>` does not grant `getUserMedia`. Do not open a public tunnel
   or install a wildcard/self-signed certificate on devices without authorization.

A **printable/downloadable test QR is available only for synthetic test data**:
as Admin open a unit card -> **Meter QR label**, choose placement, generate, then
**Download SVG** / **Print label**. The QR is public *identification* only;
resolving it still requires sign-in, property assignment, reading permission, and
an explicit billing month.

### Real-meter acceptance checklist (bilingual) — PENDING real device

No real phone camera, external QR scan, or real meter was available in this
environment, so every row below is **pending**. Treat the synthetic OCR run as
storage/plumbing evidence only, never as real-meter accuracy, and never auto-save
OCR output.

| # | Meter / condition | Human-confirmed ground truth | OCR suggestion | Result | Capture & save | Phone/browser | Status |
|---|---|---|---|---|---|---|---|
| 1 | Digital meter, clear display / ডিজিটাল মিটার, পরিষ্কার ডিসপ্লে | | | | | | Pending |
| 2 | Mechanical meter with decimal/red wheel / দশমিক বা লাল চাকাসহ | | | | | | Pending |
| 3 | Off-centre display / ডিসপ্লে কেন্দ্রে নয় | | | | | | Pending |
| 4 | Glare / আলোর প্রতিফলন | | | | | | Pending |
| 5 | Darkness / অন্ধকার | | | | | | Pending |
| 6 | Blur / ঝাপসা | | | | | | Pending |
| 7 | Partially obscured digits / আংশিক ঢাকা অঙ্ক | | | | | | Pending |
| 8 | Manual correction when OCR fails or is wrong / OCR ব্যর্থ বা ভুল হলে হাতে সংশোধন | | | | | | Pending |

Record per row: the human-confirmed reading; the OCR suggestion or "no
suggestion"; exact match / wrong suggestion / no result; the capture and save
outcome; and the phone/browser tested. A no-result outcome with usable manual
entry is acceptable. Do not infer decimal placement or invent ground truth. Do
not send photos to external OCR services or commit personal photos.

### Field-test startup / shutdown

Restart the API (derive the connection in memory; never print the password):

```powershell
$local = Get-Content src/backend/PrimeTect.Api/appsettings.FieldTest.Local.json | ConvertFrom-Json
$source = [string](Get-Content (Join-Path src/backend/PrimeTect.Api $local.ConnectionSource) | ConvertFrom-Json).ConnectionStrings.PrimeTectDatabase
$env:ASPNETCORE_ENVIRONMENT = 'Demo'
$env:Database__Provider = 'PostgreSQL'
$env:ConnectionStrings__PrimeTectDatabase = ($source -replace '(?i)(Database|Initial Catalog)=[^;]+', "Database=$($local.ConnectionDatabase)")
$env:AllowedOrigins__0 = [string]$local.AllowedOrigins[0]
$env:AllowedOrigins__1 = [string]$local.AllowedOrigins[1]
$env:PublicAppBaseUrl = [string]$local.PublicAppBaseUrl
$env:MeterPhotos__RootPath = Join-Path (Resolve-Path src/backend/PrimeTect.Api).Path ([string]$local.MeterPhotos.RootPath)
dotnet run --no-build -c Release --no-launch-profile --project src/backend/PrimeTect.Api --urls http://127.0.0.1:5031
```

Restart the frontend from the isolated copy:

```powershell
$copy = Join-Path $env:TEMP 'primetect-fe-3031'
$env:NEXT_PUBLIC_API_BASE_URL = 'http://127.0.0.1:5031'
Set-Location $copy
npm.cmd run dev -- --hostname 127.0.0.1 --port 3031
```

Stop only the confirmed `5031`/`3031` listeners
(`Get-NetTCPConnection -LocalPort 5031,3031 -State Listen`); do not stop the
pre-existing demos or the unrelated port-3000 app. The database is disposable.

## Mobile-number login and Admin-assisted password recovery (2026-10-14)

Existing users can sign in with their email **or** an Admin-assigned mobile
number. The login field is bilingual (**Email or mobile number / ইমেইল বা মোবাইল
নম্বর**); local Bangladesh `01…` numbers are normalized to `+880…` when
Bangladesh is selected (the default), international numbers use an explicit `+`
country code, and Bangla digits are normalized to ASCII. Numbers are stored
canonically as text and are **globally unique** at the PostgreSQL boundary
(filtered unique index `IX_users_PhoneNumber`), because a user belongs to exactly
one organization and login has no organization selector. There is no
cross-organization membership or shared credential, so one organization's Admin
cannot affect another organization's accounts. Invalid or unassigned numbers
return a generic failure; the server never guesses a country.

**Admin staff sign-in & recovery** (Admin-only, own organization, non-Admin
targets only): the Admin can assign/change a login mobile number and issue a
one-time temporary password. Both actions require the acting Admin's **current
password**, an **exact account-name confirmation**, and are **audited** in
`staff_credential_audit_entries` (actor, target, action, reason, time,
submission key) without any password or token. The number is labelled
**Admin-assigned**, not SMS-verified.

Password reset generates a cryptographically random temporary password (meets
the existing Identity policy), default expiry **24 hours** (configurable via
`Authentication:TemporaryPasswordExpiryHours`), returned **once** with
`Cache-Control: no-store`. It is never stored, logged, placed in a URL, or put in
browser storage; if the response is lost, a new reset must be issued. A repeated
submission key is rejected without another reset, and a later reset invalidates
the earlier temporary password.

Temporary-password login opens only a **restricted recovery session**
(`primetect:recovery` claim). Server-side middleware restricts it to session
status, the required password change, logout, and CSRF bootstrap; all property,
photo, reading, collection, report, and administration endpoints return 403 until
the password is changed. The bilingual change screen provides new/confirm
passwords, show/hide, requirements, and save; the new password cannot reuse the
temporary one. On success the recovery state clears and the user must log in
normally with the new password.

**Session invalidation:** the authentication cookie carries the Identity security
stamp, and every authenticated request re-checks it against the database. A phone
change or password reset updates the target's stamp, so the target's existing
cookies are rejected on the **next** request rather than after an extended
interval. Identity password hashing, lockout (the acting Admin's password check
uses lockout-on-failure), and concurrency stamps are reused.

Not implemented: SMS/email/OTP delivery, 2FA, tenant authentication, Admin
self-recovery, and automatic account linking. “Forgot password?” directs staff to
their organization Admin; no message is sent. Existing email login is unchanged
and does not depend on email delivery.

### Migration and demo

The migration is `20261013174707_StaffPhoneLoginAndPasswordRecovery` (adds
`MustChangePassword`, `TemporaryPasswordExpiresAtUtc`, `AdminAssignedPhoneNumber`
to `users`, the filtered unique login-phone index, and the audit table). Apply it
only to a fresh disposable database; do not apply it to `primetect` or an
existing demo.

Fresh synthetic auth demo: database `primetect_authdemo_20261014_ce242862` (all
**thirteen** migrations verified applied), API `http://127.0.0.1:5041`, frontend
`http://127.0.0.1:3041`. Ignored local files:
`src/backend/PrimeTect.Api/appsettings.AuthDemo.Local.json`,
`src/backend/PrimeTect.Api/metering-authdemo.credentials.secret.json`, and
`src/backend/PrimeTect.Api/App_Data/auth-demo-*`. Do not print the passwords.

### Verification

- Frontend UI tests (15 passed), typecheck, production build.
- Backend build; `dotnet test` (48 passed, 12 skipped).
- Guarded PostgreSQL suite (12 passed) against generated disposable databases;
  the new schema test verifies the unique login-phone index and audit table.
- Real persisted browser acceptance (8 passed, one worker, no mocked successful
  mutations): Admin email login; Admin assigns a login phone through the panel;
  Staff phone login; Admin issues a one-time temporary password; the old
  password/session is rejected; the temporary password opens the restricted
  recovery screen with `/api/properties` returning 403; the Staff changes the
  password (show/hide, requirements); normal login with the new password; and
  the temporary password is rejected afterwards. Screenshots never include
  passwords.

Manual checklist (bilingual): sign in with email, then with an assigned number /
ইমেইল দিয়ে সাইন ইন করুন, তারপর নির্ধারিত নম্বর দিয়ে; as Admin assign a number and
reset a password / অ্যাডমিন হিসেবে নম্বর দিন ও পাসওয়ার্ড রিসেট করুন; confirm the
temporary password screen forces a change and blocks all other screens / সাময়িক
পাসওয়ার্ড বদলাতে বাধ্য করে ও অন্য স্ক্রিন বন্ধ রাখে; confirm the old session ends
immediately / পুরোনো সেশন সাথে সাথে শেষ হয়।

Physical phone camera, external QR scanning, and real-meter OCR testing remain
**pending** and are not part of this milestone.

## Simplified login, create-account, and invitation screens (2026-10-14)

The unauthenticated screens were rebuilt into three simple, bilingual panels on a
clean white background with restrained PrimeTect branding, reusing the existing
authentication APIs (no second credential store, no backend contract changes).
**The attached reference image was not present in the repository**, so the layout
follows the written requirements; visual verification is limited to the automated
desktop/mobile checks below.

- **Log in:** one large labeled **Email address or mobile number** field, a country
  selector (Bangladesh default) that preserves the existing normalization
  (`01…` → `+880…`, international `+` code; invalid numbers rejected), a password
  field with an accessible **Show / Hide** control (`aria-pressed`), a large primary
  **Log in** button, a **Forgot password?** link that explains the current
  Admin-assisted staff recovery and does **not** claim any message was sent, an
  outlined **Create new account** button, a secondary **Accept staff invitation**
  link, and a persistent বাংলা / English toggle. Buttons and fields are at least
  48px; labels and inputs keep `autocomplete` hints for password managers.
- **Create account (two-step):** step 1 collects your name, email, password, and
  confirm password locally; step 2 collects the required organization name and
  submits **once** through the existing `/api/auth/register-owner`. Back and
  language switching preserve the entered data, no incomplete account or invented
  organization name is created, and successful registration lands on the existing
  owner first-property setup. Public signup does not join an existing
  organization and adds no public mobile-number signup.
- **Accept staff invitation:** a separate panel reusing the existing single-use,
  expiring token workflow with role/organization assignment and safe errors; it
  states that the token is shared manually and no invitation email is sent.

Safeguards preserved: generic login failures and lockout, cookie/CSRF protection,
immediate session revocation, the restricted temporary-password session and
mandatory password-change screen, organization isolation and Admin protections,
and QR destination preservation after login. Passwords and temporary credentials
stay in component memory only (never browser storage), duplicate submissions are
blocked by a busy guard, and the staff recovery controls are never shown publicly.

Verification: `npm run test:ui` (17 passed, including auth-navigation,
registration-step, and helper tests), `npm run typecheck`, and `npm run build`.
Backend code did not change. Real persisted browser acceptance (10 passed, one
worker, no mocked successful mutations) against the auth demo DB: labeled login
controls with ≥48px targets; password show/hide; honest forgot-password text;
login/create/accept navigation preserving step-1 input across language switching;
email login; duplicate-email failure preserving fields without a fake success;
multi-step registration creating a persisted owner organization; UI acceptance of
a persisted staff invitation; Admin reset opening the restricted recovery screen
with `/api/properties` returning 403; and 390px mobile English/Bangla with no
horizontal overflow. The running demo (`http://127.0.0.1:3041` / API
`http://127.0.0.1:5041`) serves this UI.

## Opening electricity reading import (2026-10-15)

Admins can seed opening electricity baselines for a whole property in one step instead of opening each room by hand. The import lives in **Utility Bills -> Electricity -> Meter setup** and is visible only to an Admin.

### Workflow

1. Sign in as an Admin and open **Utility Bills -> Electricity**. In **Admin - Meter setup**, find **Import opening readings**.
2. Download the property template as **CSV** (`template.csv`) or **Excel** (`template.xlsx`). The template lists every eligible room for the selected property.
3. Fill `opening_reading` for the rooms that have a baseline. Leave a row blank to skip it.
4. Choose the filled file and click **Validate file (preview)**. The preview lists every row as Ready, Skipped, Conflict, or Error. Previewing writes no application records.
5. Click **Confirm import** to apply every ready row in a single transaction. A failed import is rolled back completely and never reports partial success.

Changing the selected file clears the previous preview, so a stale preview can never be confirmed. Changing the property or month in the surrounding screen reloads the panel and its template.

### Template fields

Both formats share the same columns and are matched **by stable IDs only** - never by name or serial.

| Column | Meaning |
| --- | --- |
| `template_version` | Template format version (`1`). Other versions are rejected. |
| `property_id` | Stable property id; must match the property you are importing into. |
| `property_name` | Human-readable only; not used for matching. |
| `unit_id` | Stable unit id; the row is applied to this room. |
| `unit_name` | Human-readable only; not used for matching. |
| `meter_installation_id` | Active submeter id, or blank for a legacy room with no submeter. |
| `meter_type` | Must be `Electricity`. |
| `opening_reading` | The opening baseline. Blank means missing; `0` is a valid zero baseline. |
| `reading_date` | `YYYY-MM-DD`. Its month must equal `first_billing_month`. |
| `first_billing_month` | `YYYY-MM`; the first period the baseline starts from. |

The CSV template is UTF-8 with a byte-order mark and standard RFC 4180 quoting. The Excel template has a `Readings` sheet plus a bilingual `Instructions` sheet; identifiers are stored as text, and it contains no formulas, macros, or external links.

### Period convention and billing

An opening baseline is **not** an approved monthly reading. It is the starting value for the first billing period. When a billing cycle is generated, electricity resolves its previous reading from the immediately preceding approved period; if none exists it falls back to the installation's opening baseline (`OpeningEntry`), and otherwise reports it as missing. `reading_date` must fall inside `first_billing_month`.

A missing electricity rate never defaults to zero: the baseline is still stored, but the room's electricity line stays blocked until a rate is configured.

### Eligibility and conflict rules

A row is **Ready** when the room is an enabled, non-archived unit that either has an active submeter or a configured electricity charge, has no existing opening baseline or replacement opening, and has no billing period at or after `first_billing_month`. Rooms without a submeter are imported through the existing legacy-adoption path (identity unknown; no serial or installation date is invented).

A row is **Conflict** or **Error** and blocks the whole file when, for example: the row belongs to another property; the unit or `meter_installation_id` is unknown or no longer active; the submeter already has an opening baseline or came from a replacement; the file has a duplicate unit row; a billing period already exists at or after the first billing month; the reading is negative or has more than four decimals; the date is ambiguous or in the wrong month; or the meter type is not electricity. Nothing is ever overwritten and no partial import is performed.

### Limits

- Upload size: 5 MB. Decompressed XLSX: 20 MB with at most 200 internal parts.
- Rows per file: 500. Cell length: 200 characters.
- Reading precision: four decimals, non-negative, up to 14 integer digits. Bangla and Arabic-Indic digits are normalized to ASCII.
- Accepted upload types: `.csv` and `.xlsx`. Encrypted, macro-enabled, formula-bearing, or externally-linked workbooks are rejected; formulas are never evaluated, and spreadsheet formula-injection prefixes are neutralized in generated templates.
- Templates use the maintained `CsvHelper` (Apache-2.0/MS-PL) and `ClosedXML` (MIT) libraries. Uploads are parsed in memory and are not retained; only the sanitized filename, a file hash, the validated content hash, counts, and the outcome are audited.

### Authorization and audit

The template endpoints, preview, and commit require the Admin role and are scoped to the requesting Admin's organization and property. Managers, Finance, building staff, and restricted password-recovery sessions receive 403. Each committed import records the actor, property, timestamp, sanitized filename, file hash, validated content hash, row count, imported count, and outcome - never the raw file contents. A submission key makes retries idempotent: re-submitting the same confirmation returns the original result without applying anything twice.

Opening baselines may also be recorded directly on a single room under **Room submeter setup / adoption** when only one value is known.

### Verification (2026-10-15)

- Backend: the full API test suite passed (`dotnet test`), including new template/parser unit tests and end-to-end import tests (template round-trip, Bangla digits, CSV quoting/BOM, zero vs blank, stable-ID mapping, wrong-property/cross-org, duplicate rows, precision, formula/malformed/oversized files, existing-baseline and billing-history conflicts, stale previews, missing-rate baselines, atomic rollback, idempotent retries, successful import with correct first-period calculation, and Admin-only/restricted-recovery authorization).
- Guarded PostgreSQL suite passed against a freshly generated disposable database (schema, unique submission-key index, and a persisted idempotent import).
- Frontend: `npm run test:ui` and `npm run typecheck` passed; the production `npm run build` succeeded.
- Real persisted browser acceptance (one worker, sequential, no mocked successes): 7 checks passed against a real API and the built UI, covering template download (UTF-8 BOM and stable unit id), preview, confirmation, the persisted baseline, and the blocked repeat upload.

## Dependency upgrade: Next 16 + Tailwind 4 (2026-10-18)

The follow-up recorded under **npm audit review** below was completed: the
frontend toolchain was migrated in one small, fully verified change and the
working tree reports **0 audit vulnerabilities** (previously 3 moderate and 6
high findings).

- `next` 15.5.27 → **16.4.2** (Turbopack is now the default dev/bundler
  toolchain), `react`/`react-dom` → **19.3.1**.
- `tailwindcss` 3.4.19 → **4.3.3** with `@tailwindcss/postcss`; the direct
  `postcss` and `autoprefixer` devDependencies were removed (Tailwind v4
  packages its own PostCSS plugin and prefixing). The vulnerable nested
  `postcss@8.4.31` copy under Next and the vulnerable chokidar/fast-glob/
  micromatch/braces chain brought in by Tailwind 3 no longer exist in the tree.
- `tailwind.config.ts` was deleted; its theme tokens moved into
  `app/globals.css` as CSS-first configuration (`@import "tailwindcss";` plus
  an `@theme` block holding the `ink`/`pine`/`mint`/`sand`/`sun` colors and the
  `shadow-soft` shadow). Observability of custom utilities was verified in the
  compiled dev CSS (`.bg-pine` resolves to `var(--color-pine)` and
  `.shadow-soft` to the token value).
- Next 16 regenerated `next-env.d.ts` (dev routes types) and set the mandatory
  `tsconfig.json` `jsx: react-jsx`; it also generates
  `src/frontend/AGENTS.md`, which is committed so repeated dev runs keep a
  clean tree (`agentRules: false` in `next.config.mjs` would disable it).

Verification (sequential):

- `npm run typecheck` — clean.
- `npm run test:ui` — 20 passed, 0 failed.
- `npm run build` — production build succeeded on Next.js 16.4.2 (Turbopack).
- `npm audit` — **0 vulnerabilities**.
- Dev-server smoke check on a free loopback pair (3071) confirmed HTTP 200,
  rendered PrimeTect content, and compiled Tailwind v4 theme utilities; the
  confirmed node listener was then stopped. No database, demo process, or
  tracked `.env.local` was touched. App code (all four components, the single
  route, and `lib/`) needed no source changes; the usage surface already
  specified explicit border colors and v4-compatible class names.

The application remains on Node 20+ and the backend was not modified by this
work.

## npm audit review

The current lockfile reports 9 findings: 3 moderate and 6 high, with no critical findings. They are in the frontend build dependency graph:

- `next@15.5.27` carries a vulnerable nested `postcss@8.4.31`; npm proposes `next@16.4.0`, a major upgrade. This is relevant to the Next build/server toolchain, but the reported PostCSS issue is not a browser runtime dependency.
- `tailwindcss@3.4.19` brings `chokidar`, `fast-glob`, `micromatch`, `braces`, `postcss-nested`, and `postcss-selector-parser`. The reported issues affect CSS scanning/parsing and build-time file/pattern processing, not shipped application records. npm proposes `tailwindcss@4.3.3`, a major Tailwind migration.
- The direct `postcss` package is already at `8.5.29`; the remaining PostCSS finding is the nested copy under Next.

No `npm audit fix --force` or blind major upgrade was applied. The recommended follow-up is a planned Next 16/Tailwind 4 migration with a visual regression pass, or a targeted dependency update when compatible releases are confirmed. (Follow-up completed 2026-10-18; see **Dependency upgrade: Next 16 + Tailwind 4 (2026-10-18)** above.)

## Step 2: secure meter links, photos, and optional locations

The Step 2 migration is `20261010220000_QrMeterPhotosAndLocations`. It adds nullable property coordinates, revocable opaque meter QR references, and private meter-photo metadata/associations. It must be applied only to a newly created disposable database during verification; the applied status of existing `primetect` and older demos remains **unknown**. Do not start an older demo against a schema that needs this migration.

### Meter QR labels

An authorized property administrator can open a unit card, choose **Meter QR label**, select **Room door** or **Electricity meter**, and generate a label. The response contains an opaque, unguessable navigation URL and an SVG that can be downloaded or printed. Rotation revokes earlier labels without deleting billing history. A QR is not authentication: the staff member must sign in, retain current organization/property and reading permissions, and choose the billing month. The billing month is deliberately not encoded in the label, so it cannot silently change the selected period. Invalid, revoked, disabled, or inaccessible links are rejected.

Set `PublicAppBaseUrl` (or `Frontend:PublicBaseUrl`) to a phone-reachable `http`/`https` application origin before generating labels. `localhost` and `127.0.0.1` labels work only on the same device; they are not reachable from another phone. No tunnel or public deployment is opened by PrimeTect.

QR labels use the existing one-electricity-meter-per-unit model. Multiple physical meters per unit are not represented in this milestone, and QR contents do not contain tenant details or credentials. SVG generation uses the QRCoder library's documented SVG QR path ([QRCoder official repository](https://github.com/Shane32/QRCoder)).

### Meter photos and confirmation

The guided room-entry screen has **Take meter photo** with camera capture where the browser provides it and ordinary file upload as the fallback. JPEG, PNG, and the verified WebP header variant are accepted up to 10 MB with safe generated storage keys and dimension limits. HEIC is rejected. A preview can be removed or retaken. The staff member must confirm the numeric reading separately; an OCR suggestion, when a provider exists, never writes a reading automatically. Saving still uses the existing meter-reading endpoint, concurrency version, negative-consumption checks, opening-reading rules, and draft/return/approval lifecycle.

Development and tests use private local storage under `App_Data/meter-photos` or the configured `MeterPhotos:RootPath`; the directory is not served as static content. New JPEG, PNG, and WebP uploads are decoded with SkiaSharp 4.151.2, limited to 10 MB, 8,000 pixels per side, 25 million decoded pixels, and one frame, then re-encoded without EXIF, GPS, ICC, IPTC, XMP, or other embedded metadata. All eight EXIF orientations, including mirrored variants, are applied before encoding. The stored result is therefore normalized and is not byte-for-byte identical to the source. The accepted formats and dimensions are covered by synthetic tests; HEIC is explicitly rejected. Staged uploads that are not attached are cleaned up after 24 hours by the hosted cleanup service. Database and file writes are not one transaction: a failed database save deletes the staged file and returns an unsaved error. Production needs a durable private storage implementation, backup/retention policy, and deployment-specific cleanup monitoring before this is suitable for durable operations.

The current provider is `unavailable-local-provider`: no OCR engine is configured, no image is sent to a paid or external service, and no confidence score is manufactured. Manual entry remains fully usable. Deterministic fixtures cover the provider boundary and normalization behavior only; they do not establish real-meter OCR accuracy. Any future OCR derivative must remain separate from the normalized source photo. Exact observation time is not stored; the existing observation date and server save/audit timestamps remain separate, and legacy missing dates are not invented.

### Local OCR suggestions

Meter-photo OCR uses the local Windows x64 `TesseractOCR` 5.5.2 package
(Tesseract 5.5.0) and the included official `tessdata_fast` English
`eng.traineddata` model. Both are Apache-2.0; the model license is included at
`src/backend/PrimeTect.Infrastructure/Ocr/tessdata/LICENSE.txt`. Tesseract's
small `tsv` configuration is included from the official Tesseract repository.
The package's native engine requires the Microsoft Visual C++ 2022 x64 runtime.

This checkout verified inference only on **Windows x64**. The packaged binary
is intentionally not a claim of Linux, macOS, container, or mobile deployment
support. On another platform, or when the executable/model is absent, OCR
returns unavailable and manual entry remains available. `MeterOcr:ExecutablePath`,
`MeterOcr:TessdataPath`, and `MeterOcr:TemporaryPath` can override local paths
for a supported deployment; they must remain private local paths.

The server makes a temporary PNG crop of the normalized evidence's bounded
central display region, runs at most two local Tesseract processes at once, and
kills a process on cancellation or after eight seconds. Temporary derivatives
are deleted; the normalized stored photo is never replaced or transmitted.
Only one numeric candidate from that display region is offered. Multiple
separated candidate groups, multiple OCR words, unreadable images, missing
components, or errors yield no suggestion. The provider does not use prior
readings, guess decimal positions/red wheels, or claim Bengali image OCR.
Bangla digits are normalized only if text reaches parsing.

An OCR suggestion is never saved, submitted, approved, or billed automatically.
Staff must explicitly choose **Use suggestion**, may edit it, and must re-check
the photo confirmation before Save & Next. Editing the numeric value, changing
or removing the photo, or changing room/month clears that confirmation. A late
upload response for a replaced room/photo is ignored. Tesseract's returned word
confidence is shown only as engine output, not as a probability or accuracy
guarantee.

Real-meter checklist: take a straight, well-lit photo with the reading in the
central display; verify that labels/serials are outside it; treat rolling or
red-wheel digits as manual entry; retake when the result is absent/ambiguous;
then correct and explicitly confirm the final number. Never use synthetic test
accuracy as a real-meter accuracy claim.

### Optional property locations

Users with property-management permission can enter latitude and longitude together when creating or editing a property. Coordinates are range-validated, remain nullable for existing properties, and appear read-only with an **Open in Maps** action when present. The link uses Google's official directions URL format and does not add a Maps SDK, API key, GPS tracking, proximity enforcement, or embedded map.

### Manual Step 2 checklist

1. On a fresh disposable database, apply all migrations, configure a phone-reachable `PublicAppBaseUrl`, and sign in as an authorized property administrator.
2. Generate a QR label for a named unit, print/download it, rotate it, and confirm the previous link is rejected while the new link still requires login, property access, reading permission, and an explicit billing month.
3. As assigned staff, open the room, capture a supported image or upload one, verify the preview and manual-confirmation checkbox, and confirm a failed upload/save keeps the input without showing **Saved**.
4. Confirm the attached photo is visible only to authorized reading viewers, approved readings remain immutable, and no OCR suggestion is saved without staff confirmation. Treat OCR accuracy and real-phone camera/QR behavior as pending until manually inspected on a reachable device.
5. Add coordinates as an authorized administrator and verify the read-only map action. Leave both fields blank for a property without a known location.

## Fresh QR/photo demo verification (2026-10-11)

The fresh synthetic verification database is `primetect_demo_qr_photo_20261011_af21907c`.
All 11 migrations currently in `src/backend/PrimeTect.Infrastructure/Migrations` were applied to that database only, including `20261010220000_QrMeterPhotosAndLocations`. The existing `primetect` database and earlier demos were not migrated or modified; their applied migration status remains **unknown**.

The ignored local configuration is:

- `src/backend/PrimeTect.Api/appsettings.QrPhotoDemo.Local.json`
- `src/backend/PrimeTect.Api/qr-photo-demo-credentials.secret.json`

The second file contains the synthetic Admin, Manager, Staff, and Finance account credentials. Do not print, commit, or copy its passwords into documentation. The private synthetic photo directory is `src/backend/PrimeTect.Api/App_Data/qr-photo-demo-af21907c`, which is ignored and is not served as static content.

The verified local pair is:

- Frontend: `http://127.0.0.1:3014`
- API: `http://127.0.0.1:5014`

The frontend was started from current source with `NEXT_PUBLIC_API_BASE_URL=http://127.0.0.1:5014`; the API was started from the verified build with the local demo database connection and private photo-root configuration. Health was not used as the database proof: account, property, billing, QR, photo, and migration behavior were checked against the new database.

### Restart the same local demo pair

Do not start an older demo against this schema. From the repository root, derive the connection from the ignored GuidedDemo configuration in memory, replace only its database name with the generated demo name, and assert the target before starting the API:

```powershell
$demo = Get-Content src/backend/PrimeTect.Api/appsettings.QrPhotoDemo.Local.json | ConvertFrom-Json
$sourcePath = Join-Path src/backend/PrimeTect.Api $demo.ConnectionSource
$source = Get-Content $sourcePath | ConvertFrom-Json
$conn = [string]$source.ConnectionStrings.PrimeTectDatabase
$conn = $conn -replace '(?i)(^|;)Database=[^;]*', ('$1Database=' + $demo.ConnectionDatabase)
if ($conn -notmatch ('(?i)(^|;)Database=' + [regex]::Escape($demo.ConnectionDatabase) + '(;|$)')) { throw 'Refusing to start: unexpected database target.' }
$env:ConnectionStrings__PrimeTectDatabase = $conn
$env:MeterPhotos__RootPath = $demo.MeterPhotos.RootPath
dotnet run --project src/backend/PrimeTect.Api --no-build --urls http://127.0.0.1:5014
```

In a second terminal, start the current frontend without changing its tracked `.env.local`:

```powershell
Push-Location src/frontend
$env:NEXT_PUBLIC_API_BASE_URL = 'http://127.0.0.1:5014'
npm.cmd run dev -- --hostname 127.0.0.1 --port 3014
Pop-Location
```

To stop only this pair, inspect the listeners first with `Get-NetTCPConnection -LocalPort 5014,3014` and stop the confirmed PrimeTect owner processes. Do not stop unrelated listeners. The currently running API and frontend launch PIDs are recorded in `%TEMP%\primetect_qr_photo_demo_api_pid.txt` and `%TEMP%\primetect_qr_photo_demo_frontend_pid.txt`.

### Verified synthetic coverage

The new demo includes two properties with separate assignments, unequal floors, units `2A` and `6B`, metered and fixed-charge-only units, a named tenant assigned to both `2A` and `6B`, an occupancy without name or phone, explicit opening readings, draft/returned/submitted/approved examples, optional coordinates, and synthetic private photo evidence. API smoke verification confirmed QR resolution with an explicit month, rotation/revocation, assignment isolation, manual photo save/retrieval, normalized JPEG bytes without EXIF, unavailable OCR, wrong-cycle photo rejection, approved-reading immutability, optional coordinates, and tenant current-assignment preservation.

Supported upload formats are JPEG, PNG, and WebP. HEIC is intentionally unsupported. The normalized source is not byte-for-byte original; its orientation is applied and identifying metadata is removed. OCR remains unavailable and no suggestion is fabricated. The desktop/mobile responsive layout, Bangla wrapping, and the dashboard/entry/overview flows were later verified with headless Playwright against the 3014/5014 pair (see **Browser verification (2026-10-12)**). Real camera capture, QR scanning on another device, and real-meter OCR accuracy still require a reachable device and a real meter.

### Short bilingual manual checklist

1. **English / বাংলা:** sign in from the ignored credential file, select the property and an explicit billing month, then open a QR-linked room. Confirm the unit label before entering a reading / লগইন করে সম্পত্তি ও মাস বেছে রুমটি খুলুন।
2. Generate/download a Room door or Electricity meter QR label as Admin, rotate it, and confirm the old reference is rejected / Admin হিসেবে QR তৈরি ও ঘুরিয়ে পুরনো লিংক বাতিল দেখুন।
3. Capture or upload a JPEG/PNG/WebP, inspect the preview, retake/remove it, manually confirm the reading, and verify save-before-advance / ছবি দেখে রিডিং নিশ্চিত করে সংরক্ষণ করুন।
4. Retrieve the attached photo as authorized staff, then verify an unassigned user is denied. Confirm the normalized result has no EXIF and OCR says unavailable / অনুমোদিত ব্যবহারকারীই ছবি দেখতে পারে।
5. Add optional coordinates for one property and open the directions link; leave an unknown property's coordinates empty / অবস্থান ঐচ্ছিক।

For phone testing, `127.0.0.1` works only on the PC. A separate phone requires a private LAN hostname/IP in `PublicAppBaseUrl`, matching `AllowedOrigins`, and `NEXT_PUBLIC_API_BASE_URL`, with the API/frontend bound for that LAN. Do not open a public tunnel or change the firewall. Camera capture may require a secure browser context; LAN camera behavior and HEIC rejection still need real-device verification.
