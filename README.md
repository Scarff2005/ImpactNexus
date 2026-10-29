# Impact Nexus — CSR & CSI Management Platform

An ASP.NET Core MVC application for managing Corporate Social Responsibility (CSR) and
Corporate Social Investment (CSI) programmes end to end: companies, donations, activities,
bursaries, beneficiaries, volunteer programmes, locations, dashboards and exportable reports,
with server-enforced role separation and company-level data isolation.

---

## 0. Submission pack

| Artefact | File | What it is |
|---|---|---|
| Application | `ImpactNexus.sln` | The system itself. F5 in Visual Studio, or `dotnet run`. |
| One-click launcher | `Setup-and-Run.cmd` | Finds your SQL Server, points the app at it, builds and starts it. See `START-HERE.md`. |
| Database scripts | `database/scripts.sql` | Creates the whole schema in SQL Server Management Studio. |
| Quick start | `START-HERE.md` | The two-minute version, written for someone who has never seen the project. |

The database documentation, the ERD file, the slide decks, the video script and the
teleprompter are submitted with the written pack, not tracked in this repository. The ERD and
the generated DDL are the only design artefacts needed to build and run the application.

---

## 1. Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (`net10.0`) |
| Web | ASP.NET Core MVC (Razor views) |
| Data | EF Core 10 + SQL Server (SQL Server Express works) |
| Identity | ASP.NET Core Identity (cookie auth, roles, lockout) |
| UI | Bootstrap 5, custom teal/dark-slate design system in `wwwroot/css/site.css` |
| Tests | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + SQLite (in-process integration tests) |

---

## 2. Roles and what each can do

| Capability | IT Administrator | CSR Manager | Investor / Donor | Community / Volunteer |
|---|---|---|---|---|
| Manage users, roles, password resets | ✅ | ❌ | ❌ | ❌ |
| Companies (create/edit/delete) | ✅ all | ✅ own company only | ❌ | ❌ |
| Locations (country → province → town → location) | ✅ | read-only (via forms) | ❌ | ❌ |
| CSR activities | ✅ all | ✅ own company | ❌ | ❌ |
| Donations | ✅ all | ✅ own company | ✅ own contributions (read-only) | ❌ |
| Bursary programmes | ✅ all | ✅ own company | ❌ | ❌ |
| Bursary applications | ✅ all | ✅ own company | ❌ | ✅ own applications |
| Beneficiaries | ✅ all | ✅ own company | ❌ | ❌ |
| Volunteer programmes | ✅ all | ✅ own company | ❌ | ✅ view |
| Volunteer hours | ✅ all | ✅ own company | ❌ | ✅ own hours only |
| Stakeholders | ✅ all | ✅ own company | ❌ | ❌ |
| Reports + CSV export | ✅ all data | ✅ own company | ✅ own donations / funded companies | ✅ own hours / applications |

Authorisation is enforced **server-side** on every action (`[Authorize(Policy = ...)]`) plus
record-level checks in the controllers. Hiding a sidebar link is never the only protection.

### Data isolation rules

- **CSR Managers** are bound to one company (`ApplicationUser.CompanyId`). Every query is
  filtered through `ICompanyAccessService`, and every read/write of a company-owned record is
  rejected with `403`/`AccessDenied` when the record belongs to another company.
- **Investors** can only see donations where they are the donor, and only the companies and
  reports those donations touch.
- **Community members** can only see their own volunteer hours and bursary applications.
- **IDOR protection**: a user cannot reach another user's record by editing the URL; access is
  checked against the loaded record, not the route.

---

## 3. Prerequisites

- .NET SDK 10.0 or newer (`dotnet --version`)
- SQL Server Express (or any SQL Server instance) with Windows authentication
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

The project was developed and verified against `.\SQLEXPRESS`.

---

## 4. Setup

```bash
# 1. Restore
dotnet restore

# 2. Configure the connection string (recommended: user secrets, not source control)
cd src/ImpactNexus.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=ImpactNexus;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
cd ../..

# 3. Apply the schema
dotnet ef database update --project src/ImpactNexus.Web

# 4. Run
dotnet run --project src/ImpactNexus.Web
```

`appsettings.Development.json` already contains a local SQL Express connection string so the
project runs out of the box on a default developer machine. Override it with user secrets or
environment variables (`ConnectionStrings__DefaultConnection`) anywhere else. No production
secret is committed.

### Demo data

`Database:SeedDemoData` is `true` only in `appsettings.Development.json`. On startup the app
migrates the database and seeds roles, users and demo records. Seeding is idempotent — it only
creates what is missing — so restarting never duplicates rows.

If the demo data ever needs to be rebuilt from scratch:

```bash
sqlcmd -S ".\SQLEXPRESS" -E -C -Q "ALTER DATABASE ImpactNexus SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ImpactNexus;"
dotnet ef database update --project src/ImpactNexus.Web
dotnet run --project src/ImpactNexus.Web
```

### Development accounts

| Role | Email | Password |
|---|---|---|
| IT Administrator | `admin@impactnexus.local` | `Admin#2026` |
| CSR Manager (BMW South Africa) | `csr.manager@impactnexus.local` | `Manager#2026` |
| CSR Manager (Toyota South Africa Motors) | `csr.manager2@impactnexus.local` | `Manager#2026` |
| Investor / Donor | `investor@impactnexus.local` | `Investor#2026` |
| Investor / Donor | `investor2@impactnexus.local` | `Investor#2026` |
| Community member / volunteer | `community@impactnexus.local` | `Volunteer#2026` |
| Community member / volunteer | `volunteer@impactnexus.local` | `Volunteer#2026` |

These accounts exist **only** when demo seeding is enabled. Never enable it against a real
database.

---

## 5. Project layout

```
ImpactNexus.sln
src/ImpactNexus.Web/
  Authorization/        Roles + policy names
  Controllers/          One per module, plus Account, Dashboard, Reports
  Data/
    ApplicationDbContext.cs
    DbSeeder.cs         Idempotent roles/users/demo data
    Migrations/
  Models/               Entities + enums
  ViewModels/           Form and dashboard models
  Services/
    CurrentUserService.cs      Claims-based identity snapshot
    CompanyAccessService.cs    Single enforcement point + resource handler
    DashboardService.cs        Four role-specific dashboards
    ReportService.cs           Scoped report queries + RFC 4180 CSV writer (CsvExport)
  Views/                Razor views, role-driven _Layout, shared partials
  wwwroot/              CSS design system, JS, Bootstrap/jQuery
tests/ImpactNexus.Tests/     72 integration tests (xUnit + WebApplicationFactory)
database/
  scripts.sql                Idempotent schema + seed script for SQL Server
  ImpactNexus-schema.sql     Full DDL generated from the migrations
```

### Notable implementation details

- **Claims**: a custom `ApplicationUserClaimsPrincipalFactory` adds `company_id` and `full_name`
  claims at sign-in, so authorisation checks do not hit the database per request. Because the
  claim is issued at login, so a company change is picked up on the next security-stamp check (within a minute) rather than needing a manual sign-out.
- **Deletes are guarded**: a company, activity or programme with dependent records cannot be
  deleted; the UI tells the user to deactivate/cancel instead. This keeps history intact.
- **Anti-forgery** is applied to every POST via `[ValidateAntiForgeryToken]`; missing or stale
  tokens get `400`.
- **Validation** is server-side (`ModelState`) and mirrored client-side with jQuery validation.
- **CSV export** streams `text/csv` with a UTF-8 BOM so Excel opens accented names correctly.
- **Currency**: every monetary value is South African Rand (ZAR). Rendering goes through
  `Services/Money.cs`, which pins formatting to `en-ZA` (`R1 850 000,00`) instead of trusting
  the server's regional settings, so the symbol is identical on every machine. CSV exports keep
  amounts as plain `0.00` numbers so spreadsheets can total them without parsing a currency symbol.

---

## 6. Reports

Every report supports filters (date range, company, status, search) and CSV export:

- CSR activities
- Company contributions
- Donation history
- Bursary summary
- Volunteer hours
- Beneficiary statistics

Reports reuse the same scoping rules as the rest of the app: a CSR Manager's export only
contains their company, an investor's only their own donations, a community member's only
their own hours. This is covered by tests.

CSV export uses `?export=true` on the report URL.

---

## 7. Tests

```bash
dotnet test
```

The suite boots the real application in-process with `WebApplicationFactory`, swaps SQL Server
for a temporary SQLite database, seeds the same demo data, and drives it over HTTP with real
cookies and anti-forgery tokens.

Covered:

- anonymous access redirects to login
- admin can reach user management; manager/investor/community cannot
- manager sees own company only (details, edit, activities, donations)
- investor sees own donations, blocked from other donors and from create/beneficiary pages
- community member sees own volunteer hours only, blocked from another volunteer's record
- anti-forgery token required for POST
- wrong password does not authenticate, lockout behaviour, password change rules
- role dashboards render for the right role and expose only that role's navigation
- company create → details → delete round trip
- required-field validation returns the form with errors
- manager cannot create an activity for another company (attempted via crafted POST)
- manager activity list excludes other companies' activities
- CSV exports return `text/csv` with the expected headers
- investor and community exports contain only their own rows
- report totals recomputed with LINQ match the report service
- database constraints (unique names, foreign keys, delete behaviour) hold when a
  controller is bypassed
- a bursary application outside the programme's open/close window is refused
- an invalid application leaves no orphan beneficiary record behind
- applying twice to the same programme is refused; an open programme accepts one
- volunteer hours cannot be logged for a future date
- `X-Content-Type-Options`, `X-Frame-Options` and CSP headers are present on responses
- the donor dropdown lists investor accounts only; the volunteer dropdown lists community
  members only
- disabling an account rolls its security stamp so existing sessions are invalidated
- a CSR Manager with no company gets an explanatory page instead of a redirect loop

Result on this machine: **72 passed, 0 failed**.

---

## 8. Known limitations (honest list)

1. **No email delivery.** Password reset is performed by the administrator
   (`Users → Reset password`), who then communicates the new password out of band. There is no
   "forgot password" self-service mail flow.
2. **No public registration.** Accounts are created by the administrator by design.
3. **Company and role changes take up to a minute to reach an open session.** Claims are issued at
   sign-in and refreshed by the security-stamp validator, which runs at most once a minute. A
   manual sign-out forces it immediately.
4. **No file uploads** — company logos, bursary supporting documents and volunteer proof are not
   stored; only text fields and references exist.
5. **Reports export CSV only.** There is no PDF or Excel output, and no saved/scheduled reports.
6. **Some list screens are unpaged** (companies, bursary programmes, volunteer programmes,
   countries/provinces/towns/locations) because seed volumes are small. Activities, donations,
   bursary applications, beneficiaries, stakeholders, volunteer hours and users are paged.
7. **No optimistic concurrency.** Entities have no rowversion, so simultaneous edits are
   last-write-wins.
8. **No audit log** of administrative actions beyond timestamps on records.
9. **Lockout is enabled** (5 failed attempts → 10 minute lockout) but there is no 2FA, no
   password history and no forced password rotation.
10. **Timezone**: timestamps are stored in UTC, dates are shown date-only; no per-user timezone.
11. **Tests run against SQLite**, not SQL Server. The provider-specific schema and the real
    `SQLEXPRESS` database were verified manually (migration applied, app run, all roles
    exercised), but CI would need SQL Server for full parity.
12. **Accessibility** is bootstrap-typical: semantic landmarks, labels and focus styles are
    present, but the app has not been through a formal WCAG audit.
13. **Bursary workflow is simple**: staff move an application through statuses and capture an
    award amount; there is no multi-step approval chain or document verification.

---

## 9. Configuration reference

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string (required) |
| `Database:SeedDemoData` | Runs migrations + demo seeding at startup (development only) |

Set values with user secrets, environment variables, or `appsettings.Development.json`.
Environment-variable form: `ConnectionStrings__DefaultConnection`, `Database__SeedDemoData`.
