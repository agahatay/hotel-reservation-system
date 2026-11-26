/*
    Hotel Reservation System — database schema (SQL Server 2016+ / Express / LocalDB)

    Creates the HotelReservationDB database and its tables. Safe to run more than once:
    existing objects are left untouched.

        sqlcmd -S .\SQLEXPRESS -E -i database\schema.sql
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'HotelReservationDB') IS NULL
    CREATE DATABASE HotelReservationDB;
GO

USE HotelReservationDB;
GO

/* ---------- Users (staff accounts) ---------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users
(
    UserId            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username          nvarchar(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash      nvarchar(200) NOT NULL,          -- PBKDF2-SHA256$iterations$salt$key
    Role              nvarchar(20)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN (N'Admin', N'Staff')),
    IsActive          bit           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    FailedLoginCount  int           NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    LockoutEndUtc     datetime2(0)  NULL,
    MustChangePassword bit          NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (0),  -- temporary / demo password
    CreatedAt         datetime2(0)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSDATETIME())
);
GO

-- Upgrade databases created by an earlier version of this script.
IF COL_LENGTH(N'dbo.Users', N'MustChangePassword') IS NULL
    ALTER TABLE dbo.Users ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (0);
GO

/* ---------- Room types and rooms ---------- */
IF OBJECT_ID(N'dbo.RoomTypes', N'U') IS NULL
CREATE TABLE dbo.RoomTypes
(
    RoomTypeId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomTypes PRIMARY KEY,
    TypeName    nvarchar(50)  NOT NULL CONSTRAINT UQ_RoomTypes_TypeName UNIQUE,
    Capacity    int           NOT NULL CONSTRAINT CK_RoomTypes_Capacity CHECK (Capacity BETWEEN 1 AND 20),
    BasePrice   decimal(12,2) NOT NULL CONSTRAINT CK_RoomTypes_BasePrice CHECK (BasePrice > 0)  -- per night, TRY
);
GO

IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
CREATE TABLE dbo.Rooms
(
    RoomId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
    RoomNumber  nvarchar(10) NOT NULL CONSTRAINT UQ_Rooms_RoomNumber UNIQUE,
    RoomTypeId  int          NOT NULL CONSTRAINT FK_Rooms_RoomTypes REFERENCES dbo.RoomTypes (RoomTypeId),
    Floor       int          NULL,
    IsActive    bit          NOT NULL CONSTRAINT DF_Rooms_IsActive DEFAULT (1)
);
GO

/* ---------- Guests ---------- */
IF OBJECT_ID(N'dbo.Guests', N'U') IS NULL
CREATE TABLE dbo.Guests
(
    GuestId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Guests PRIMARY KEY,
    FirstName   nvarchar(50)  NOT NULL,
    LastName    nvarchar(50)  NOT NULL,
    Phone       nvarchar(20)  NULL,
    Email       nvarchar(100) NULL,
    NationalId  nvarchar(30)  NULL,
    CreatedAt   datetime2(0)  NOT NULL CONSTRAINT DF_Guests_CreatedAt DEFAULT (SYSDATETIME())
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Guests_NationalId')
    CREATE UNIQUE INDEX UX_Guests_NationalId ON dbo.Guests (NationalId) WHERE NationalId IS NOT NULL;
GO

/* ---------- Reservations ---------- */
IF OBJECT_ID(N'dbo.Reservations', N'U') IS NULL
CREATE TABLE dbo.Reservations
(
    ReservationId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reservations PRIMARY KEY,
    GuestId        int           NOT NULL CONSTRAINT FK_Reservations_Guests REFERENCES dbo.Guests (GuestId),
    RoomId         int           NOT NULL CONSTRAINT FK_Reservations_Rooms  REFERENCES dbo.Rooms (RoomId),
    CheckInDate    date          NOT NULL,
    CheckOutDate   date          NOT NULL,
    Status         nvarchar(20)  NOT NULL CONSTRAINT DF_Reservations_Status DEFAULT (N'Booked')
                   CONSTRAINT CK_Reservations_Status CHECK (Status IN (N'Booked', N'CheckedIn', N'Completed', N'Cancelled')),
    TotalPrice     decimal(12,2) NOT NULL CONSTRAINT CK_Reservations_TotalPrice CHECK (TotalPrice >= 0),  -- fixed at booking time
    Currency       char(3)       NOT NULL CONSTRAINT DF_Reservations_Currency DEFAULT ('TRY'),
    CreatedAt      datetime2(0)  NOT NULL CONSTRAINT DF_Reservations_CreatedAt DEFAULT (SYSDATETIME()),
    CancelledAt    datetime2(0)  NULL,
    CONSTRAINT CK_Reservations_Dates CHECK (CheckOutDate > CheckInDate)
);
GO

-- Supports the availability / overlap check (RoomId + date range) and its range locks.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reservations_Room_Dates')
    CREATE INDEX IX_Reservations_Room_Dates ON dbo.Reservations (RoomId, CheckInDate, CheckOutDate) INCLUDE (Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reservations_GuestId')
    CREATE INDEX IX_Reservations_GuestId ON dbo.Reservations (GuestId);
GO

/* ---------- Payments ---------- */
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments
(
    PaymentId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    ReservationId  int           NOT NULL CONSTRAINT FK_Payments_Reservations REFERENCES dbo.Reservations (ReservationId),
    Amount         decimal(12,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    Currency       char(3)       NOT NULL CONSTRAINT CK_Payments_Currency CHECK (Currency IN ('TRY', 'USD', 'EUR', 'GBP')),
    Method         nvarchar(10)  NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'Card')),
    PaidAt         datetime2(0)  NOT NULL CONSTRAINT DF_Payments_PaidAt DEFAULT (SYSDATETIME())
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_ReservationId')
    CREATE INDEX IX_Payments_ReservationId ON dbo.Payments (ReservationId) INCLUDE (Amount, Currency);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_PaidAt')
    CREATE INDEX IX_Payments_PaidAt ON dbo.Payments (PaidAt) INCLUDE (Amount, Currency);
GO

PRINT 'HotelReservationDB schema is ready.';
GO
