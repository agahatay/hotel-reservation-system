using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

/// <summary>Read-only queries for the dashboard and occupancy screens.</summary>
public sealed class ReportRepository(SqlConnection con, SqlTransaction? tx = null)
{
    /// <summary>Reservation counts per status for reservations checking in during [from, toExclusive).</summary>
    public Dictionary<ReservationStatus, int> CountByStatus(DateTime from, DateTime toExclusive) =>
        con.Command("""
            SELECT Status, COUNT(*) FROM dbo.Reservations
            WHERE CheckInDate >= @from AND CheckInDate < @to
            GROUP BY Status;
            """, tx)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .ReadAll(r => (Status: Enum.Parse<ReservationStatus>(r.GetString(0), true), Count: r.GetInt32(1)))
           .ToDictionary(x => x.Status, x => x.Count);

    /// <summary>Payments received in [from, toExclusive), one total per currency — never mixed.</summary>
    public List<CurrencyTotal> IncomeByCurrency(DateTime from, DateTime toExclusive) =>
        con.Command("""
            SELECT Currency, SUM(Amount) FROM dbo.Payments
            WHERE PaidAt >= @from AND PaidAt < @to
            GROUP BY Currency ORDER BY Currency;
            """, tx)
           .DateTime2("@from", from.Date)
           .DateTime2("@to", toExclusive.Date)
           .ReadAll(r => new CurrencyTotal(r.GetString(0).Trim(), r.GetDecimal(1)));

    /// <summary>Non-cancelled stays overlapping [from, toExclusive).</summary>
    public List<StayInterval> Stays(DateTime from, DateTime toExclusive) =>
        con.Command("""
            SELECT RoomId, CheckInDate, CheckOutDate FROM dbo.Reservations
            WHERE Status <> N'Cancelled' AND CheckInDate < @to AND CheckOutDate > @from;
            """, tx)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .ReadAll(r => new StayInterval(r.GetInt32(0), r.GetDateTime(1), r.GetDateTime(2)));

    public List<RoomUsage> TopRooms(DateTime from, DateTime toExclusive, int top) =>
        con.Command("""
            SELECT TOP (@top) rm.RoomNumber, COUNT(*) AS Reservations,
                   SUM(DATEDIFF(day, r.CheckInDate, r.CheckOutDate)) AS Nights
            FROM dbo.Reservations r
            JOIN dbo.Rooms rm ON rm.RoomId = r.RoomId
            WHERE r.Status <> N'Cancelled' AND r.CheckInDate >= @from AND r.CheckInDate < @to
            GROUP BY rm.RoomNumber
            ORDER BY COUNT(*) DESC, Nights DESC, rm.RoomNumber;
            """, tx)
           .Int("@top", top)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .ReadAll(r => new RoomUsage(r.GetString(0), r.GetInt32(1), r.GetInt32(2)));

    public (string Name, int Count)? TopGuest(DateTime from, DateTime toExclusive)
    {
        var rows = con.Command("""
            SELECT TOP (1) g.FirstName + N' ' + g.LastName, COUNT(*)
            FROM dbo.Reservations r
            JOIN dbo.Guests g ON g.GuestId = r.GuestId
            WHERE r.Status <> N'Cancelled' AND r.CheckInDate >= @from AND r.CheckInDate < @to
            GROUP BY g.GuestId, g.FirstName, g.LastName
            ORDER BY COUNT(*) DESC, g.LastName;
            """, tx)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .ReadAll(r => (r.GetString(0), r.GetInt32(1)));
        return rows.Count == 0 ? null : rows[0];
    }
}
