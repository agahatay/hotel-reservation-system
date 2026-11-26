/*
    Hotel Reservation System — fictional demonstration data.

    All people, phone numbers, e-mail addresses (example.com) and ID numbers are invented.
    Dates are relative to the day the script runs so the dashboard always shows current data.
    Runs only on an empty database (no users); it never modifies existing data.

    DEMONSTRATION ACCOUNTS — for the fictional demo database only. These passwords are
    public; never use them (or this seed script) in a production environment.
    Both accounts must choose a new password at their first sign-in.
        admin / Admin123!   (Admin)
        staff / Staff123!   (Staff)

        sqlcmd -S .\SQLEXPRESS -E -i database\seed.sql
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

USE HotelReservationDB;
GO

IF EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    PRINT 'Seed skipped: the database already contains users.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @today date = CAST(GETDATE() AS date);

INSERT INTO dbo.Users (Username, PasswordHash, Role, MustChangePassword) VALUES
 (N'admin', N'PBKDF2-SHA256$600000$5h0UH3yxU9boCuTd8s66sw==$Zs9EUoSbjufTLBfCZXG1rmXSDZvKAxSD7W39zvHGce0=', N'Admin', 1),
 (N'staff', N'PBKDF2-SHA256$600000$OBTkSXNZOAI69VwWLWgzMg==$nlCFHwFAOw2eTT5/Jwo2cmZRhSZAsSyqrWG7TWOzriU=', N'Staff', 1);

INSERT INTO dbo.RoomTypes (TypeName, Capacity, BasePrice) VALUES
 (N'Twin Room (2 Single Beds)',          2, 3000.00),
 (N'Double Room (1 Double Bed)',         2, 3800.00),
 (N'Triple Room (1 Double + 1 Single)',  3, 4000.00),
 (N'Family Suite',                       4, 6500.00);

INSERT INTO dbo.Rooms (RoomNumber, RoomTypeId, Floor, IsActive)
SELECT v.RoomNumber, rt.RoomTypeId, v.Floor, v.IsActive
FROM (VALUES
    (N'101', N'Twin Room (2 Single Beds)',         1, 1),
    (N'102', N'Twin Room (2 Single Beds)',         1, 1),
    (N'103', N'Double Room (1 Double Bed)',        1, 1),
    (N'104', N'Double Room (1 Double Bed)',        1, 1),
    (N'105', N'Triple Room (1 Double + 1 Single)', 1, 1),
    (N'201', N'Twin Room (2 Single Beds)',         2, 1),
    (N'202', N'Double Room (1 Double Bed)',        2, 1),
    (N'203', N'Double Room (1 Double Bed)',        2, 1),
    (N'204', N'Triple Room (1 Double + 1 Single)', 2, 1),
    (N'205', N'Triple Room (1 Double + 1 Single)', 2, 1),
    (N'301', N'Double Room (1 Double Bed)',        3, 1),
    (N'302', N'Double Room (1 Double Bed)',        3, 1),
    (N'303', N'Family Suite',                      3, 1),
    (N'304', N'Family Suite',                      3, 0),   -- under renovation
    (N'401', N'Family Suite',                      4, 1),
    (N'402', N'Twin Room (2 Single Beds)',         4, 1)
) v (RoomNumber, TypeName, Floor, IsActive)
JOIN dbo.RoomTypes rt ON rt.TypeName = v.TypeName;

INSERT INTO dbo.Guests (FirstName, LastName, Phone, Email, NationalId, CreatedAt) VALUES
 (N'Elif',    N'Demir',     N'+90 555 010 0101', N'elif.demir@example.com',     N'D10000001', DATEADD(day, -60, SYSDATETIME())),
 (N'Mert',    N'Kaya',      N'+90 555 010 0102', N'mert.kaya@example.com',      N'D10000002', DATEADD(day, -58, SYSDATETIME())),
 (N'Zeynep',  N'Aydın',     N'+90 555 010 0103', N'zeynep.aydin@example.com',   N'D10000003', DATEADD(day, -50, SYSDATETIME())),
 (N'Can',     N'Yıldız',    N'+90 555 010 0104', NULL,                          N'D10000004', DATEADD(day, -45, SYSDATETIME())),
 (N'Selin',   N'Arslan',    N'+90 555 010 0105', N'selin.arslan@example.com',   N'D10000005', DATEADD(day, -40, SYSDATETIME())),
 (N'Emre',    N'Çelik',     N'+90 555 010 0106', N'emre.celik@example.com',     NULL,         DATEADD(day, -32, SYSDATETIME())),
 (N'Anna',    N'Schmidt',   N'+49 30 5550 1107', N'anna.schmidt@example.com',   N'P7000108',  DATEADD(day, -30, SYSDATETIME())),
 (N'James',   N'Carter',    N'+44 20 5550 1108', N'james.carter@example.com',   N'P7000109',  DATEADD(day, -25, SYSDATETIME())),
 (N'Deniz',   N'Şahin',     N'+90 555 010 0109', N'deniz.sahin@example.com',    N'D10000010', DATEADD(day, -20, SYSDATETIME())),
 (N'Lucía',   N'García',    N'+34 91 555 0110',  N'lucia.garcia@example.com',   N'P7000111',  DATEADD(day, -14, SYSDATETIME())),
 (N'Burak',   N'Öztürk',    N'+90 555 010 0111', N'burak.ozturk@example.com',   N'D10000012', DATEADD(day, -7,  SYSDATETIME())),
 (N'Ayşe',    N'Koç',       N'+90 555 010 0112', N'ayse.koc@example.com',       N'D10000013', DATEADD(day, -3,  SYSDATETIME()));

-- (email, room, check-in offset, nights, status) — statuses are consistent with the dates.
DECLARE @stays TABLE (Email nvarchar(100), NationalId nvarchar(30), RoomNumber nvarchar(10), InOffset int, Nights int, Status nvarchar(20));
INSERT INTO @stays VALUES
 (N'elif.demir@example.com',   NULL,         N'101', -40, 3, N'Completed'),
 (N'mert.kaya@example.com',    NULL,         N'103', -35, 5, N'Completed'),
 (N'zeynep.aydin@example.com', NULL,         N'204', -28, 2, N'Completed'),
 (NULL,                        N'D10000004', N'202', -21, 4, N'Completed'),
 (N'selin.arslan@example.com', NULL,         N'303', -18, 6, N'Completed'),
 (N'elif.demir@example.com',   NULL,         N'102', -12, 2, N'Completed'),
 (N'emre.celik@example.com',   NULL,         N'301', -10, 3, N'Cancelled'),
 (N'anna.schmidt@example.com', NULL,         N'105',  -6, 4, N'Completed'),
 (N'james.carter@example.com', NULL,         N'203',  -2, 5, N'CheckedIn'),
 (N'deniz.sahin@example.com',  NULL,         N'104',  -1, 3, N'CheckedIn'),
 (N'lucia.garcia@example.com', NULL,         N'401',   0, 7, N'CheckedIn'),
 (N'burak.ozturk@example.com', NULL,         N'201',   2, 2, N'Booked'),
 (N'ayse.koc@example.com',     NULL,         N'302',   5, 3, N'Booked'),
 (N'mert.kaya@example.com',    NULL,         N'205',   9, 4, N'Booked'),
 (N'zeynep.aydin@example.com', NULL,         N'101',  14, 2, N'Cancelled'),
 (N'selin.arslan@example.com', NULL,         N'303',  20, 5, N'Booked'),
 (N'deniz.sahin@example.com',  NULL,         N'101',  -8, 3, N'Completed'),
 (N'anna.schmidt@example.com', NULL,         N'101',   3, 4, N'Booked'),
 (N'james.carter@example.com', NULL,         N'102',  -4, 5, N'CheckedIn'),
 (N'emre.celik@example.com',   NULL,         N'102',   6, 3, N'Booked'),
 (N'ayse.koc@example.com',     NULL,         N'103', -15, 4, N'Completed'),
 (N'burak.ozturk@example.com', NULL,         N'103',  -3, 4, N'CheckedIn'),
 (N'lucia.garcia@example.com', NULL,         N'103',  10, 2, N'Booked'),
 (N'mert.kaya@example.com',    NULL,         N'201', -14, 6, N'Completed'),
 (N'zeynep.aydin@example.com', NULL,         N'201',  -5, 3, N'Completed'),
 (N'elif.demir@example.com',   NULL,         N'202',  -9, 5, N'Completed'),
 (N'selin.arslan@example.com', NULL,         N'202',   1, 5, N'Booked'),
 (NULL,                        N'D10000004', N'204',  -7, 6, N'Completed'),
 (N'james.carter@example.com', NULL,         N'204',   4, 3, N'Booked'),
 (N'anna.schmidt@example.com', NULL,         N'205', -16, 3, N'Completed'),
 (N'ayse.koc@example.com',     NULL,         N'205',  -1, 4, N'CheckedIn'),
 (N'deniz.sahin@example.com',  NULL,         N'301', -25, 4, N'Completed'),
 (N'mert.kaya@example.com',    NULL,         N'301',   1, 3, N'Booked'),
 (N'lucia.garcia@example.com', NULL,         N'302', -20, 3, N'Completed'),
 (N'emre.celik@example.com',   NULL,         N'302',  -6, 4, N'Completed'),
 (N'burak.ozturk@example.com', NULL,         N'402', -11, 5, N'Completed'),
 (N'elif.demir@example.com',   NULL,         N'402',  -2, 6, N'CheckedIn'),
 (N'zeynep.aydin@example.com', NULL,         N'303',  -6, 5, N'Completed');

INSERT INTO dbo.Reservations (GuestId, RoomId, CheckInDate, CheckOutDate, Status, TotalPrice, Currency, CreatedAt, CancelledAt)
SELECT g.GuestId, r.RoomId,
       DATEADD(day, s.InOffset, @today),
       DATEADD(day, s.InOffset + s.Nights, @today),
       s.Status,
       s.Nights * rt.BasePrice,
       'TRY',
       DATEADD(day, s.InOffset - 10, CAST(@today AS datetime2(0))),
       CASE WHEN s.Status = N'Cancelled' THEN DATEADD(day, s.InOffset - 3, CAST(@today AS datetime2(0))) END
FROM @stays s
JOIN dbo.Guests g ON g.Email = s.Email OR g.NationalId = s.NationalId
JOIN dbo.Rooms r ON r.RoomNumber = s.RoomNumber
JOIN dbo.RoomTypes rt ON rt.RoomTypeId = r.RoomTypeId
ORDER BY s.InOffset;

-- Payments: completed stays fully paid, current stays partially paid (deposit), future stays some deposits.
INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method, PaidAt)
SELECT ReservationId, TotalPrice, Currency, N'Card', DATEADD(hour, 10, CAST(CheckOutDate AS datetime2(0)))
FROM dbo.Reservations WHERE Status = N'Completed' AND ReservationId % 2 = 0;

INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method, PaidAt)
SELECT ReservationId, ROUND(TotalPrice * 0.4, 2), Currency, N'Cash', DATEADD(hour, 14, CAST(CheckInDate AS datetime2(0)))
FROM dbo.Reservations WHERE Status = N'Completed' AND ReservationId % 2 = 1;

INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method, PaidAt)
SELECT ReservationId, TotalPrice - ROUND(TotalPrice * 0.4, 2), Currency, N'Card', DATEADD(hour, 10, CAST(CheckOutDate AS datetime2(0)))
FROM dbo.Reservations WHERE Status = N'Completed' AND ReservationId % 2 = 1;

INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method, PaidAt)
SELECT ReservationId, ROUND(TotalPrice * 0.3, 2), Currency, N'Card', DATEADD(hour, 15, CAST(CheckInDate AS datetime2(0)))
FROM dbo.Reservations WHERE Status = N'CheckedIn';

INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method, PaidAt)
SELECT TOP (2) ReservationId, ROUND(TotalPrice * 0.2, 2), Currency, N'Card', CreatedAt
FROM dbo.Reservations WHERE Status = N'Booked' ORDER BY CheckInDate;

COMMIT TRANSACTION;

PRINT 'Demo data inserted.';
GO
