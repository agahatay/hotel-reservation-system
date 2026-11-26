# Testing

| Suite | Project | Needs | Tests |
|---|---|---|---|
| Unit | `tests/HotelReservationSystem.Tests` | nothing | 138 |
| SQL Server integration | `tests/HotelReservationSystem.IntegrationTests` | local SQL Server | 34 |
| Manual UI | checklist below | Windows desktop | 9 scenarios |

```powershell
dotnet test -c Release                                              # both suites
dotnet test tests/HotelReservationSystem.Tests -c Release           # unit only
$env:HOTELRES_TEST_SERVER = "(localdb)\MSSQLLocalDB"                # optional, default .\SQLEXPRESS
dotnet test tests/HotelReservationSystem.IntegrationTests -c Release
```

The integration suite drops and recreates databases whose names end in `Tests`
(`HotelReservationDB_IntegrationTests`, `HotelReservationDB_SeedTests`) using the same
`DatabaseInstaller` the application uses. It refuses to drop any other database.

## Unit tests (business rules)

- **Reservation rules:** overlap detection (inside, start, end, surrounding, identical, back-to-back on
  either side, before/after), time of day ignored, cancelled stays release the room, date validation
  (past check-in, zero/negative length, maximum stay), night counting across month ends, decimal
  pricing, cancellation rules, date-driven status progression.
- **Payments:** balance, full/partial/exact payments, overpayment, fully paid, non-positive amounts,
  more than two decimals, currency mismatch, unsupported currency, cancelled reservation, no
  floating-point drift, cancelled reservations owe nothing.
- **Occupancy:** clipping of stays to a period, room-night rate, rooms outside the set, no double
  counting, empty and full occupancy, check-out night excluded.
- **Security:** PBKDF2 round trip, wrong/blank passwords, salting, no plaintext in hash, malformed and
  plaintext hashes never verify, rehash detection, seed hashes match the documented demo passwords,
  lockout policy, password and username policy, Staff/Admin permissions.
- **Other:** guest validation and normalization, ID masking, natural room-number order, `LIKE`
  escaping, settings file parsing, shipped `appsettings.json` contains no credentials, embedded SQL
  scripts identical to `database/*.sql`, `GO` batch splitting, unsafe database names rejected.

## Integration tests (SQL Server)

- Booking stores the price at booking time; later price changes do not alter it.
- Same room + overlapping dates rejected (4 variants); back-to-back and non-overlapping accepted;
  different rooms on the same dates accepted.
- Cancelled reservation releases the room; double cancel and cancel after check-in rejected.
- Inactive rooms neither listed nor bookable; past/invalid dates rejected; database `CHECK`
  constraint rejects invalid dates even without the application.
- **Concurrency:** 12 simultaneous bookings of one room/date range → exactly 1 succeeds;
  8 simultaneous partially overlapping bookings → no overlap stored.
  (Verified to fail when the locking hints are removed: 8 duplicate bookings were created.)
- **Concurrent payments:** 10 × 300 against a 1,000 total → exactly 3 succeed.
- Partial payments, overpayment, fully paid, payment on cancelled reservation, void (Admin only).
- Dashboard income is grouped per currency; balances only count the reservation's currency.
- Guests: Turkish characters, update clears optional fields, wildcard search, validation, duplicate
  national ID, delete blocked when the guest has reservations.
- Rooms: duplicate number, rooms with history deactivated instead of deleted.
- Auth: login, generic errors, rehash on login, lockout, disabled accounts, last-admin protection,
  Staff cannot create users or manage rooms, initial setup refused once users exist, no plaintext
  passwords stored, forced password change for new/reset accounts.
- Setup scripts: schema + seed create a working demo database (logins work and require a password
  change, 38 reservations, no overlaps, stored totals correct, no overpayment).

## Latest results

Run on 2026-11-26, Windows 11, .NET SDK 10.0.101, SQL Server 2022 Express, Release configuration,
after `dotnet clean` and `dotnet restore`:

```
HotelReservationSystem.Tests.dll             Passed: 138  Failed: 0  Skipped: 0
HotelReservationSystem.IntegrationTests.dll  Passed:  34  Failed: 0  Skipped: 0
```

## Manual UI tests

Performed on the running application (Release build and the published single-file executable),
driven through Windows UI Automation and verified against the database. Fictional data only.

| # | Scenario | Result |
|---|---|---|
| 1 | Clean setup: empty LocalDB → *Database settings* → *Create / update database* with demo data → connection succeeds | Pass |
| 2 | Wrong password rejected; demo `admin` forced to change password; reusing the old password rejected | Pass |
| 3 | Guests: required-field and e-mail validation shown inline; create; edit phone (verified in DB) | Pass |
| 4 | Rooms: invalid price rejected; room type created with `5200,50` (comma decimal); room created; duplicate room number rejected; inactive room offers *Activate* | Pass |
| 5 | Reservations: price preview; reservation created; booked room no longer offered for the same dates | Pass |
| 6 | Payments: overpayment rejected; partial payment `2000,25`; balance 3,200.25 | Pass |
| 7 | Cancellation: disabled for a checked-in stay; future booking cancelled; balance 0 and *Record payment* disabled | Pass |
| 8 | Monthly occupancy figures checked by hand (17 rooms × 30 nights = 510); next month; user created with weak password rejected | Pass |
| 9 | Staff sees only Dashboard, Reservations, Guests, Payments, Monthly occupancy; no *Void payment*; sign out returns to sign-in | Pass |

Visual review at 1920×1080 and at the minimum window size 1100×680. Issues found and fixed during
this review: editor showing a different record than the highlighted row after saving; grids
truncating every column at small widths (now scroll horizontally); payment-history header
truncation; month name shown in the OS language; balance shown for cancelled reservations.
