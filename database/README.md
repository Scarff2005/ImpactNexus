# Database

Two scripts, one schema. Use whichever matches what you are doing.

| File | Use it when |
|---|---|
| `scripts.sql` | You want to create the database and all 17 tables in **SQL Server Management Studio**, by hand. |
| `ImpactNexus-schema.sql` | You want the DDL exactly as the EF Core migrations emit it — for reading, diffing, or rebuilding on a server. |

You do **not** need either of them to run the application: on first launch the app creates the
database, applies the schema and seeds the demo data by itself.

---

## `scripts.sql`

A complete, self-contained installer: creates the `ImpactNexus` database, then every table, index
and foreign key, then records the migrations as applied.

**How to run it**

1. Open SQL Server Management Studio.
2. Connect to `.\SQLEXPRESS` (or `(localdb)\MSSQLLocalDB`).
3. **File → Open → File…**, choose `scripts.sql`.
4. Press **F5**.
5. The Messages pane ends with `Impact Nexus database is ready.`

**What it produces**

| Measure | Value |
|---|---|
| Tables | 17 (16 domain tables + EF Core's migration ledger) |
| Columns | 129 in the domain tables |
| Foreign keys | 27 |

**It is safe to run twice.** Every step is guarded by an existence check, so a second run prints
`already exists - leaving it in place`, `Impact Nexus database is ready`, and changes nothing.

**A note on what you will see in the output**

The script is the migration history replayed in order, so it creates the original 21 tables and then
applies the second migration, which drops the four Identity tables the application does not use:
`AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins` and `AspNetUserTokens`. That is why you
will see `CREATE TABLE` and `DROP TABLE` for those four. The database you are left with has 17
tables, which is exactly what the application's model expects.

You will also see two warnings about the 900-byte clustered index limit on `PK_AspNetUserLogins`,
`PK_AspNetUserRoles` and `PK_AspNetUserTokens`. They come from the original migration, they are
expected, and they are harmless — those tables either hold no rows or hold a handful.

**What it does not do**

It creates the structure only — no rows. The application seeds the demo data (companies, users,
activities, donations, bursaries, volunteer hours) when it starts. Signing in before that will not
work, because there are no accounts yet.

---

## `ImpactNexus-schema.sql`

The same schema generated with `dotnet ef migrations script --idempotent`. It does **not** create
the database — it assumes the database exists and you have selected it. Use it when you want to see
exactly what the migrations produce.

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -C -d ImpactNexus -i ImpactNexus-schema.sql
```

---

## Verifying the result

In SSMS, against the `ImpactNexus` database:

```sql
SELECT COUNT(*) FROM sys.tables;        -- 17
SELECT COUNT(*) FROM sys.foreign_keys;  -- 27
SELECT * FROM __EFMigrationsHistory;    -- two rows
```

After the application has started once:

```sql
SELECT COUNT(*) FROM dbo.AspNetUsers;   -- 8 demo accounts
SELECT COUNT(*) FROM dbo.Companies;     -- 3 (BMW, Toyota, Volkswagen)
SELECT COUNT(*) FROM dbo.CsrActivities; -- 6 activities
```

---

## Starting over

If you want a clean database, drop it and start the application again — it recreates and reseeds
everything.

In SSMS:

```sql
USE [master];
ALTER DATABASE [ImpactNexus] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [ImpactNexus];
```

Stop the application first: it holds a lock on the database while it runs.
