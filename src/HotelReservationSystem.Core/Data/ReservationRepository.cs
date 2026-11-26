using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public sealed class ReservationRepository(SqlConnection con, SqlTransaction? tx = null)
{
    private const string ListSelect = """
        SELECT r.ReservationId, r.GuestId, (g.FirstName + N' ' + g.LastName) AS GuestName,
               r.RoomId, rm.RoomNumber, r.CheckInDate, r.CheckOutDate, r.Status,
               r.TotalPrice, r.Currency, r.CreatedAt
        FROM dbo.Reservations r
        JOIN dbo.Guests g ON g.GuestId = r.GuestId
        JOIN dbo.Rooms rm ON rm.RoomId = r.RoomId
        """;

    public List<ReservationListItem> List(ReservationStatus? status, string? search)
    {
        var q = string.IsNullOrWhiteSpace(search) ? null : GuestRepository.EscapeLike(search.Trim());
        return con.Command($"""
            {ListSelect}
            WHERE (@status IS NULL OR r.Status = @status)
              AND (@q IS NULL
                   OR (g.FirstName + N' ' + g.LastName) LIKE N'%' + @q + N'%'
                   OR rm.RoomNumber LIKE N'%' + @q + N'%'
                   OR CAST(r.ReservationId AS nvarchar(12)) = @q)
            ORDER BY r.CheckInDate DESC, r.ReservationId DESC;
            """, tx)
           .NVarChar("@status", status?.ToString(), 20)
           .NVarChar("@q", q, 110)
           .ReadAll(Map);
    }

    /// <summary>Reservations whose check-in date falls in [from, toExclusive).</summary>
    public List<ReservationListItem> ListByCheckIn(DateTime from, DateTime toExclusive, int top) =>
        con.Command($"""
            SELECT TOP (@top) * FROM ({ListSelect}) x
            WHERE x.CheckInDate >= @from AND x.CheckInDate < @to
            ORDER BY x.CheckInDate DESC, x.ReservationId DESC;
            """, tx)
           .Int("@top", top)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .ReadAll(Map);

    public ReservationListItem? GetById(int reservationId, bool forUpdate = false) =>
        con.Command($"""
            SELECT r.ReservationId, r.GuestId, (g.FirstName + N' ' + g.LastName) AS GuestName,
                   r.RoomId, rm.RoomNumber, r.CheckInDate, r.CheckOutDate, r.Status,
                   r.TotalPrice, r.Currency, r.CreatedAt
            FROM dbo.Reservations r {(forUpdate ? "WITH (UPDLOCK, HOLDLOCK)" : "")}
            JOIN dbo.Guests g ON g.GuestId = r.GuestId
            JOIN dbo.Rooms rm ON rm.RoomId = r.RoomId
            WHERE r.ReservationId = @id;
            """, tx)
           .Int("@id", reservationId)
           .ReadSingle(Map);

    /// <summary>
    /// Counts non-cancelled reservations of a room overlapping [checkIn, checkOut).
    /// Uses range locks so no conflicting row can be inserted until the transaction ends.
    /// </summary>
    public int CountOverlapping(int roomId, DateTime checkIn, DateTime checkOut) =>
        con.Command("""
            SELECT COUNT(*)
            FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK)
            WHERE RoomId = @roomId
              AND Status <> N'Cancelled'
              AND CheckInDate < @checkOut
              AND CheckOutDate > @checkIn;
            """, tx)
           .Int("@roomId", roomId)
           .Date("@checkIn", checkIn)
           .Date("@checkOut", checkOut)
           .ScalarInt();

    public int Insert(NewReservation r, decimal totalPrice, string currency) =>
        con.Command("""
            INSERT INTO dbo.Reservations (GuestId, RoomId, CheckInDate, CheckOutDate, Status, TotalPrice, Currency)
            OUTPUT INSERTED.ReservationId
            VALUES (@g, @r, @in, @out, N'Booked', @total, @cur);
            """, tx)
           .Int("@g", r.GuestId)
           .Int("@r", r.RoomId)
           .Date("@in", r.CheckInDate)
           .Date("@out", r.CheckOutDate)
           .Money("@total", totalPrice)
           .Char("@cur", currency, 3)
           .ScalarInt();

    /// <summary>Cancels only if the reservation is still Booked (guards against concurrent changes).</summary>
    public bool Cancel(int reservationId) =>
        con.Command("""
            UPDATE dbo.Reservations
            SET Status = N'Cancelled', CancelledAt = SYSDATETIME()
            WHERE ReservationId = @id AND Status = N'Booked';
            """, tx)
           .Int("@id", reservationId)
           .ExecuteNonQuery() == 1;

    /// <summary>Applies <see cref="ReservationRules.StatusForDate"/> to all open reservations.</summary>
    public int RefreshStatuses(DateTime today) =>
        con.Command("""
            UPDATE dbo.Reservations SET Status = N'Completed'
            WHERE Status IN (N'Booked', N'CheckedIn') AND CheckOutDate <= @today;

            UPDATE dbo.Reservations SET Status = N'CheckedIn'
            WHERE Status = N'Booked' AND CheckInDate <= @today AND CheckOutDate > @today;
            """, tx)
           .Date("@today", today)
           .ExecuteNonQuery();

    internal static ReservationListItem Map(SqlDataReader r) => new()
    {
        ReservationId = r.GetInt32(r.GetOrdinal("ReservationId")),
        GuestId = r.GetInt32(r.GetOrdinal("GuestId")),
        GuestName = r.GetString(r.GetOrdinal("GuestName")),
        RoomId = r.GetInt32(r.GetOrdinal("RoomId")),
        RoomNumber = r.GetString(r.GetOrdinal("RoomNumber")),
        CheckInDate = r.GetDateTime(r.GetOrdinal("CheckInDate")),
        CheckOutDate = r.GetDateTime(r.GetOrdinal("CheckOutDate")),
        Status = r.Enum<ReservationStatus>("Status"),
        TotalPrice = r.GetDecimal(r.GetOrdinal("TotalPrice")),
        Currency = r.GetString(r.GetOrdinal("Currency")).Trim(),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };
}
