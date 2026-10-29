# Impact Nexus — start here

A Corporate Social Responsibility and Corporate Social Investment platform: companies, projects,
donations, bursaries, beneficiaries, volunteers, dashboards and reports — with four user roles and
server-enforced data isolation.

This folder is everything: the application, its tests, the database scripts, the ERD, the
documentation and the slide deck.

---

## The short version

1. **Extract the zip** to a normal folder, for example `C:\Projects\ImpactNexus`.
   Do not run it from inside the zip — Windows shows a read-only copy there.
2. **Double-click `Setup-and-Run.cmd`.**
3. Wait. The first launch takes **2 to 5 minutes** — it downloads packages, builds, creates the
   database and fills it with demo data. When it is ready your browser opens on the login page.

Then sign in with the administrator account:

| Role | Email | Password |
|---|---|---|
| IT Administrator | `admin@impactnexus.local` | `Admin#2026` |
| CSR Manager (BMW) | `csr.manager@impactnexus.local` | `Manager#2026` |
| Investor | `investor@impactnexus.local` | `Investor#2026` |
| Community / Volunteer | `community@impactnexus.local` | `Volunteer#2026` |

To stop the application, press **Ctrl+C** in the black window, or close it.

---

## What has to be installed

Two things. The launcher checks both and tells you exactly what is missing.

### 1. .NET 10 SDK

Download: <https://dotnet.microsoft.com/download/dotnet/10.0> — pick **SDK x64** for Windows.

After installing, **close every terminal window and open a new one**, otherwise the `dotnet`
command is still not on the path.

### 2. SQL Server

Either one works. The launcher finds whichever is present, in this order.

**Option A — SQL Server Express** (what the project was developed against)

Download: <https://www.microsoft.com/sql-server/sql-server-downloads> → *Express* → *Download now*.
Install with the default settings. The instance name must stay **SQLEXPRESS**.

You do not need SQL Server Management Studio to run the app. Install it only if you want to look
at the tables directly — it is free: <https://aka.ms/ssmsfullsetup>.

**Option B — SQL Server LocalDB**

If you already have Visual Studio, LocalDB is probably installed already. Check by opening a
terminal and typing:

```powershell
sqllocaldb info
```

If it prints `MSSQLLocalDB`, you are done — the launcher will find it. If the command is not
recognised, install LocalDB from
<https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb>.

---

## What the launcher actually does

`Setup-and-Run.cmd` runs `Setup-and-Run.ps1`, which:

1. checks for the .NET 10 SDK;
2. finds `.\SQLEXPRESS` or `(localdb)\MSSQLLocalDB` and starts the service if it is stopped;
3. sets the connection string **through an environment variable for that session only** — no file
   in the project is edited, so your copy is identical to the original;
4. runs `dotnet restore`, then `dotnet run`;
5. the application creates the `ImpactNexus` database itself, applies the schema and seeds the
   demo data;
6. opens the browser once the site answers on its port.

If you would rather do it by hand:

```powershell
dotnet restore
dotnet run --project src/ImpactNexus.Web
```

The connection string lives in `src/ImpactNexus.Web/appsettings.Development.json` and can be
overridden with the `ConnectionStrings__DefaultConnection` environment variable or user secrets.

---

## If it does not start

| What you see | What it means | Fix |
|---|---|---|
| `'dotnet' is not recognized` | The .NET SDK is missing, or the terminal was open before the install | Install the SDK, close the window, open a new one |
| `The .NET 10 SDK is not installed` | An older SDK is on the machine | Install .NET 10; the project targets `net10.0` |
| `No SQL Server instance was found` | Neither Express nor LocalDB is installed | Install one of the two options above |
| `Could not start the service` | The SQL Server service needs administrator rights | Open *Services*, start *SQL Server (SQLEXPRESS)*, run the launcher again |
| `Cannot open database "ImpactNexus"` | The database exists but your Windows account cannot reach it | Delete the database in SSMS and let the app recreate it |
| A page shows `500` after you edited code | The app was rebuilt while it was running | Stop it, run the launcher again |
| Browser does not open | It opened too late, or a firewall blocked it | Open <http://localhost:5199> by hand — the exact URL is printed in the black window |
| Port already in use | Another copy is already running | The launcher picks the next free port automatically; read the URL in the window |

If the app prints an error and exits, the first red line in the window is the real cause. Copy it
and send it on.

---

## The database

You have two ways to get the database, and they produce the same result. **Use the first one.**

1. **Let the application do it.** Start the app. It creates `ImpactNexus`, applies the schema and
   seeds the demo data. Nothing to run by hand.
2. **Create it first in SQL Server Management Studio.** Open
   `database/scripts.sql`, press **F5**. Then start the app — it detects the existing schema and
   only seeds the data.

Use the second way when you want to look at the tables *before* the app has ever run, or when you
are restoring onto a server. Details in `database/README.md`.

---

## What is in the folder

| Path | What it is |
|---|---|
| `ImpactNexus.sln` | Open this in Visual Studio 2022 or later |
| `src/ImpactNexus.Web/` | The application |
| `tests/ImpactNexus.Tests/` | 72 integration tests — `dotnet test` |
| `database/scripts.sql` | Creates the whole schema in SSMS |
| `database/ImpactNexus-schema.sql` | The same schema, generated from the EF Core migration |
| `docs/` | Database documentation, ERD, decks and video script — submitted with the written pack, not tracked in the repository |
| `README.md` | The full technical readme |

---

## Opening it in Visual Studio

1. Open `ImpactNexus.sln`.
2. Set `ImpactNexus.Web` as the startup project (it usually is already).
3. Press **F5**. Visual Studio starts the app and opens the browser.

Visual Studio uses `src/ImpactNexus.Web/appsettings.Development.json`, which points at
`.\SQLEXPRESS`. If you are on LocalDB, change that one line to:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ImpactNexus;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## Running the tests

```powershell
dotnet test ImpactNexus.sln
```

The suite boots the real application in-process, swaps SQL Server for a temporary SQLite database,
and drives it over HTTP. It does not touch your `ImpactNexus` database. Expected result:
**72 passed, 0 failed**.
