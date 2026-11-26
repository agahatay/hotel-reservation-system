# Installation

## Requirements

| | |
|---|---|
| Operating system | Windows 10 or 11, 64-bit |
| Database | Microsoft SQL Server 2016 or later — **SQL Server Express** or **SQL Server Express LocalDB** (both free) |
| .NET | Not required for the release package (self-contained). Building from source needs the .NET 10 SDK. |

### Installing SQL Server Express or LocalDB

1. Download SQL Server Express from <https://www.microsoft.com/sql-server/sql-server-downloads>.
2. Choose **Basic** for a full SQL Server Express instance (`.\SQLEXPRESS`), or **Download Media → LocalDB**
   for the lightweight per-user engine (`(localdb)\MSSQLLocalDB`).

LocalDB is the simplest choice for a single computer. Use SQL Server Express when several
computers on a local network share one database.

## Installing the application

1. Download `HotelReservationSystem-v1.0.0-win-x64.zip` from the
   [Releases page](https://github.com/agahatay/hotel-reservation-system/releases).
   Optionally verify it against the `.sha256` file: `Get-FileHash .\HotelReservationSystem-v1.0.0-win-x64.zip`.
2. Extract it to a folder, for example `C:\Apps\HotelReservationSystem`.
3. Start `HotelReservationSystem.exe`.

Windows SmartScreen may warn because the executable is not code-signed; choose *More info → Run anyway*.

## First run: creating the database

If the configured database cannot be opened, the **Database settings** window appears.

1. **Server:** `.\SQLEXPRESS` or `(localdb)\MSSQLLocalDB` (or `SERVER\INSTANCE` on a network).
2. **Database:** `HotelReservationDB`.
3. Click **Create / update database** and choose:
   - **Yes** — load the fictional demo data (16 rooms, 12 guests, 38 reservations and the demo accounts)
   - **No** — empty database; at the next start you create the administrator account
4. Click **Save**. The application restarts its connection and shows the sign-in screen.

The scripts are idempotent: running *Create / update database* against an existing database only
adds missing objects and never changes or deletes data.

### Demo accounts

| Username | Password | Role |
|---|---|---|
| `admin` | `Admin123!` | Admin |
| `staff` | `Staff123!` | Staff |

These credentials are public and exist only in the fictional demo database. A new password must be
chosen at the first sign-in. **Do not use the demo data or these passwords in production.**

### Alternative: creating the database with sqlcmd

```powershell
powershell -ExecutionPolicy Bypass -File database\setup-database.ps1 -Server ".\SQLEXPRESS"
powershell -ExecutionPolicy Bypass -File database\setup-database.ps1 -Server "(localdb)\MSSQLLocalDB" -SkipSeed
```

Or run `database\schema.sql` (and optionally `database\seed.sql`) in SQL Server Management Studio.

## Configuration

Connection string sources, first match wins:

1. Environment variable `HOTELRES_CONNECTION_STRING`
2. `%APPDATA%\HotelReservationSystem\appsettings.json` (saved by *Database settings*)
3. `appsettings.json` next to the executable

```json
{
  "ConnectionStrings": {
    "HotelDb": "Server=.\\SQLEXPRESS;Database=HotelReservationDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
  }
}
```

Windows (integrated) authentication is used; the application never stores a database password.
`TrustServerCertificate=True` is appropriate for a local SQL Server Express/LocalDB with its
self-signed certificate. For a server on a network, install a trusted certificate and set it to `False`.

Logs of unexpected errors are written to `%LOCALAPPDATA%\HotelReservationSystem\logs`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| *The database server could not be reached* | Start the SQL Server service (`services.msc` → *SQL Server (SQLEXPRESS)*) or check the server name. For LocalDB run `sqllocaldb start MSSQLLocalDB`. |
| *Cannot open database "HotelReservationDB" … login failed* | The database does not exist yet — use *Create / update database* — or your Windows account has no access to it. |
| *The certificate chain was issued by an authority that is not trusted* | Tick *Trust server certificate* (local servers) or install a trusted certificate on the server. |
| *Account locked* | Wait 5 minutes or ask an administrator to reset the password. |
| Forgot the only administrator password | Recreate the database without demo data, or use SQL tools to set `MustChangePassword` and a new hash. |

Never expose SQL Server directly to the internet.

## Uninstalling

Delete the application folder and `%APPDATA%\HotelReservationSystem`. Drop the database in
SQL Server Management Studio if it is no longer needed.
