# Architecture notes

The README covers what the system does and how to run it. This file covers why
it is built the way it is, for whoever reads the code next — including future me.

## The shape of a request

Every request goes through the same pipe: security headers first, then routing,
authentication, authorization, then the controller. The header middleware sits
before everything else so even error pages and redirects carry nosniff, DENY
framing and the CSP. The CSP allows inline styles because the dashboard charts
set style attributes, but no inline or third-party scripts. That trade-off is
commented in Program.cs so nobody "fixes" it by accident.

Controllers load a record, check access against the loaded record (not the
route), and pick a view. Checking the loaded record is the IDOR defence: editing
an id in the URL gets a 403, not someone else's data. Hiding a sidebar link is
never the protection — the tests POST crafted requests as the wrong role and
expect to be refused.

## Identity and claims

ASP.NET Core Identity with cookie auth, lockout at 5 attempts / 10 minutes.
A custom claims principal factory adds `company_id` and `full_name` at sign-in
so authorization checks don't hit the database on every request. The price of
that decision: a company or role change only reaches open sessions when the
security stamp revalidates (once a minute) or the user signs out. I chose a
minute of staleness over a database hit per request, and the README lists it as
a known limitation instead of hiding it.

Disabling an account rolls its security stamp, which kills existing sessions
within the validation interval. There is a test for exactly that.

## Authorization, two layers

Layer one is role policies on every action: AdminOnly, StaffOnly,
CsrManagerOnly, InvestorOnly, CommunityOnly. Layer two is record level.
`CompanyAccessService` is the single place that answers "may this user touch
this company's record", and `CompanyAccessHandler` plugs it into the
authorization system as a resource handler. CSR managers are bound to one
company, investors to their own donations, community members to their own
volunteer hours and bursary applications.

## Data

EF Core 10 over SQL Server with retry-on-failure (3 attempts, 5 seconds).
Migrations are the source of truth; `database/scripts.sql` and the generated
DDL exist for SSMS and for the submission pack, not the other way round.
Deletes are guarded: a record with dependents cannot be deleted, only
deactivated, so history survives.

The seeder is idempotent and only runs when `Database:SeedDemoData` is true,
which is set in development config only. Restarting never duplicates rows.

## Tests

The suite boots the real application in-process with `WebApplicationFactory`,
swaps SQL Server for a temporary SQLite database, seeds the same demo data and
drives it with a test HTTP client using real cookies and anti-forgery tokens.
So the tests exercise the actual pipeline — headers, auth, policies,
controllers, EF — not mocked services.

SQLite is a trade-off, not an accident: it makes the suite runnable anywhere,
including CI, with zero setup. Provider-specific behaviour was verified
manually against SQLEXPRESS; the README says so plainly.

## Small decisions worth knowing

- Money is always ZAR, formatted through `Services/Money.cs` pinned to `en-ZA`,
  so the server's regional settings can't change what a user sees. CSV keeps
  plain `0.00` numbers so spreadsheets can total them without parsing a symbol.
- CSV export is RFC 4180 with a UTF-8 BOM, because Excel on Windows mangles
  accented names without it.
- The dashboard takes a `TimeProvider` so its date logic has a test seam. The
  rest of the app still calls `DateTime.UtcNow` directly — unifying that is on
  the list below.

## What I'd do differently next time

An audit log and rowversion columns would be first: last-write-wins is fine for
a submission but not for real multi-user data. After that, email delivery for
password resets, one injected clock everywhere, and paging on the remaining
list screens.