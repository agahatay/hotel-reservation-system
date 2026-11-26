# Architecture

Hotel Reservation System is a two-tier Windows desktop application: a Windows Forms client that
talks directly to SQL Server. The code is split into two projects so that every business rule can
be tested without a user interface.

```
┌─────────────────────────────── HotelReservationSystem (WinForms) ───────────────────────────────┐
│  Program ─ FrmLogin / FrmFirstRunSetup / FrmDatabaseSettings / FrmPasswordDialog                 │
│  FrmMain (sidebar shell) ─ Pages: Dashboard, Reservations, Guests, Payments, Occupancy, Rooms,   │
│                                   Users                                                         │
│  Common: Theme, Ui (control factory), ErrorHandler, AppServices (composition root)               │
└──────────────────────────────────────────────┬──────────────────────────────────────────────────┘
                                               │ calls services only
┌──────────────────────────────────── HotelReservationSystem.Core ────────────────────────────────┐
│  Services   AuthService, GuestService, RoomService, ReservationService, PaymentService,          │
│             ReportService — authorization, validation, transactions                              │
│  Domain     ReservationRules, PaymentRules, OccupancyCalculator, GuestValidator,                 │
│             RolePermissions — pure functions, no I/O                                             │
│  Security   PasswordHasher (PBKDF2), LoginPolicy                                                 │
│  Data       SqlConnectionFactory, *Repository (parameterized SQL), Transactions,                 │
│             DatabaseInstaller (embedded schema.sql / seed.sql)                                   │
│  Configuration  AppSettings (environment variable → %APPDATA% → appsettings.json)                │
└──────────────────────────────────────────────┬──────────────────────────────────────────────────┘
                                               │ Microsoft.Data.SqlClient, Windows authentication
                                     SQL Server Express / LocalDB
```

## Design decisions

**No ORM.** The data access is small and performance-sensitive in a few places (locking hints,
range locks). Plain ADO.NET with a thin helper (`SqlExtensions`) keeps the SQL visible and every
parameter strongly typed.

**Repositories are per connection.** A repository is created for one `SqlConnection` (and optional
`SqlTransaction`). A service that needs a transaction opens it, creates the repositories inside it
and commits, e.g. `ReservationService.Create`:

1. `BEGIN TRANSACTION` (SERIALIZABLE)
2. read the room `WITH (UPDLOCK, HOLDLOCK)` — all bookings of the same room queue on this row lock
3. count overlapping non-cancelled reservations `WITH (UPDLOCK, HOLDLOCK)`
4. compute the price from the locked room type and insert
5. commit (a deadlock victim is retried up to three times)

Payments use the same pattern on the reservation row, so simultaneous payments cannot exceed the total.
The integration tests prove both: with the locks removed, 12 simultaneous bookings produced 8
reservations for the same room; with the locks, exactly one.

**Rules are pure functions.** `ReservationRules`, `PaymentRules` and `OccupancyCalculator` take
values and return values, which makes them fast to unit-test and easy to read.

**Authorization in services.** Each service method calls `RolePermissions.Require(...)`; the UI
also hides what a role cannot use, but hiding is not the protection.

**Stored price.** `Reservations.TotalPrice` is fixed when the booking is made; changing a room
type's price affects only new bookings.

**One currency per reservation.** Reservations are priced in TRY and payments must match the
reservation currency, so a balance is never calculated across currencies. Income reports group by
currency.

**Occupancy in room-nights.** A stay occupies the nights `[CheckIn, CheckOut)`. Occupancy =
occupied room-nights of active rooms ÷ (active rooms × nights in the period).

**Self-installing database.** `schema.sql` and `seed.sql` are embedded resources of the Core
library. The same code path (`DatabaseInstaller`) is used by the *Database settings* window and by
the integration tests, so the shipped scripts are exercised on every test run.

**UI toolkit.** All pages build their controls through `Ui` and `Theme`, so spacing, colours, grid
behaviour (empty states, selection, column sizing) and accessibility names are consistent.
`Ui.Bind`/`Ui.OnSelected` guarantee that an editor always shows the record highlighted in its grid.

## Known limitations

- Two-tier design: users connect to SQL Server with their Windows account, so SQL permissions
  must be managed for those accounts. A service tier would be needed for internet-facing use.
- Status changes (`CheckedIn`, `Completed`) are date-driven; there is no separate manual
  check-in / check-out step.
- Reservation dates and rooms cannot be edited after booking (cancel and rebook).
