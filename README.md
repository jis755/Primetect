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

The PostgreSQL-specific tests are opt-in and refuse ordinary application connection strings. They require `PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION` to target a local server (`localhost`, `127.0.0.1`, or `::1`) and the administrative database `postgres` or `template1`. The fixture then creates a random `primetect_verification_<guid>` database, applies all eleven committed migrations from empty, runs the integration tests, and drops only that generated database. It never resets or drops `primetect` or any other database.

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

The EF Core migrations `InitialPrimeTect`, `MonthlyBillingMilestone`, `PrintableBillingReports`, `ConfigurableElectricityWarningsAndReadingDates`, `ManualCollectionReporting`, `ConfigurableOrganizationPermissions`, `PropertyTimeZone`, `RoomMeterIssues`, `FeedbackReportsAndNotifications`, and `FeedbackSubmissionIdempotency` are committed for the dedicated PrimeTect database, but this repository does not apply migrations automatically. Their applied status is **unknown** until verified separately against the local `primetect` database before manual use. The opt-in PostgreSQL tests use a newly created disposable database; they do not change the applied-status claim for the local `primetect` database. Printable reports never substitute current property/floor/tenant data for missing historical snapshot fields.

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

## npm audit review

The current lockfile reports 9 findings: 3 moderate and 6 high, with no critical findings. They are in the frontend build dependency graph:

- `next@15.5.27` carries a vulnerable nested `postcss@8.4.31`; npm proposes `next@16.4.0`, a major upgrade. This is relevant to the Next build/server toolchain, but the reported PostCSS issue is not a browser runtime dependency.
- `tailwindcss@3.4.19` brings `chokidar`, `fast-glob`, `micromatch`, `braces`, `postcss-nested`, and `postcss-selector-parser`. The reported issues affect CSS scanning/parsing and build-time file/pattern processing, not shipped application records. npm proposes `tailwindcss@4.3.3`, a major Tailwind migration.
- The direct `postcss` package is already at `8.5.29`; the remaining PostCSS finding is the nested copy under Next.

No `npm audit fix --force` or blind major upgrade was applied. The recommended follow-up is a planned Next 16/Tailwind 4 migration with a visual regression pass, or a targeted dependency update when compatible releases are confirmed.

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

Supported upload formats are JPEG, PNG, and WebP. HEIC is intentionally unsupported. The normalized source is not byte-for-byte original; its orientation is applied and identifying metadata is removed. OCR remains unavailable and no suggestion is fabricated. Real camera capture, QR scanning on another device, and visual responsive verification remain pending because no browser runtime was available in this environment.

### Short bilingual manual checklist

1. **English / বাংলা:** sign in from the ignored credential file, select the property and an explicit billing month, then open a QR-linked room. Confirm the unit label before entering a reading / লগইন করে সম্পত্তি ও মাস বেছে রুমটি খুলুন।
2. Generate/download a Room door or Electricity meter QR label as Admin, rotate it, and confirm the old reference is rejected / Admin হিসেবে QR তৈরি ও ঘুরিয়ে পুরনো লিংক বাতিল দেখুন।
3. Capture or upload a JPEG/PNG/WebP, inspect the preview, retake/remove it, manually confirm the reading, and verify save-before-advance / ছবি দেখে রিডিং নিশ্চিত করে সংরক্ষণ করুন।
4. Retrieve the attached photo as authorized staff, then verify an unassigned user is denied. Confirm the normalized result has no EXIF and OCR says unavailable / অনুমোদিত ব্যবহারকারীই ছবি দেখতে পারে।
5. Add optional coordinates for one property and open the directions link; leave an unknown property's coordinates empty / অবস্থান ঐচ্ছিক।

For phone testing, `127.0.0.1` works only on the PC. A separate phone requires a private LAN hostname/IP in `PublicAppBaseUrl`, matching `AllowedOrigins`, and `NEXT_PUBLIC_API_BASE_URL`, with the API/frontend bound for that LAN. Do not open a public tunnel or change the firewall. Camera capture may require a secure browser context; LAN camera behavior and HEIC rejection still need real-device verification.
