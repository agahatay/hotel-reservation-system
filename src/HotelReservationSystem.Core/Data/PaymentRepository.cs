using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public sealed class PaymentRepository(SqlConnection con, SqlTransaction? tx = null)
{
    /// <summary>
    /// Reservations whose stay overlaps [from, toExclusive), with the amount paid in the
    /// reservation's own currency.
    /// </summary>
    public List<ReservationBalance> ListBalances(DateTime from, DateTime toExclusive, bool outstandingOnly, string? search)
    {
        var q = string.IsNullOrWhiteSpace(search) ? null : GuestRepository.EscapeLike(search.Trim());
        return con.Command("""
            SELECT r.ReservationId, (g.FirstName + N' ' + g.LastName) AS GuestName, rm.RoomNumber,
                   r.CheckInDate, r.CheckOutDate, r.Status, r.TotalPrice, r.Currency,
                   COALESCE(p.Paid, 0) AS PaidAmount
            FROM dbo.Reservations r
            JOIN dbo.Guests g ON g.GuestId = r.GuestId
            JOIN dbo.Rooms rm ON rm.RoomId = r.RoomId
            OUTER APPLY (SELECT SUM(Amount) AS Paid FROM dbo.Payments
                         WHERE ReservationId = r.ReservationId AND Currency = r.Currency) p
            WHERE r.CheckInDate < @to AND r.CheckOutDate > @from
              AND (@outstanding = 0 OR (r.Status <> N'Cancelled' AND r.TotalPrice > COALESCE(p.Paid, 0)))
              AND (@q IS NULL
                   OR (g.FirstName + N' ' + g.LastName) LIKE N'%' + @q + N'%'
                   OR rm.RoomNumber LIKE N'%' + @q + N'%'
                   OR CAST(r.ReservationId AS nvarchar(12)) = @q)
            ORDER BY r.CheckInDate DESC, r.ReservationId DESC;
            """, tx)
           .Date("@from", from)
           .Date("@to", toExclusive)
           .Bit("@outstanding", outstandingOnly)
           .NVarChar("@q", q, 110)
           .ReadAll(r => new ReservationBalance
           {
               ReservationId = r.GetInt32(r.GetOrdinal("ReservationId")),
               GuestName = r.GetString(r.GetOrdinal("GuestName")),
               RoomNumber = r.GetString(r.GetOrdinal("RoomNumber")),
               CheckInDate = r.GetDateTime(r.GetOrdinal("CheckInDate")),
               CheckOutDate = r.GetDateTime(r.GetOrdinal("CheckOutDate")),
               Status = r.Enum<ReservationStatus>("Status"),
               TotalPrice = r.GetDecimal(r.GetOrdinal("TotalPrice")),
               Currency = r.GetString(r.GetOrdinal("Currency")).Trim(),
               PaidAmount = r.GetDecimal(r.GetOrdinal("PaidAmount"))
           });
    }

    public List<Payment> ListForReservation(int reservationId) =>
        con.Command("""
            SELECT PaymentId, ReservationId, Amount, Currency, Method, PaidAt
            FROM dbo.Payments WHERE ReservationId = @id ORDER BY PaidAt, PaymentId;
            """, tx)
           .Int("@id", reservationId)
           .ReadAll(Map);

    public decimal SumPaid(int reservationId, string currency) =>
        con.Command("SELECT COALESCE(SUM(Amount), 0) FROM dbo.Payments WHERE ReservationId = @id AND Currency = @cur;", tx)
           .Int("@id", reservationId)
           .Char("@cur", currency, 3)
           .ScalarDecimal();

    public int Insert(int reservationId, decimal amount, string currency, PaymentMethod method) =>
        con.Command("""
            INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method)
            OUTPUT INSERTED.PaymentId
            VALUES (@r, @a, @c, @m);
            """, tx)
           .Int("@r", reservationId)
           .Money("@a", amount)
           .Char("@c", currency, 3)
           .NVarChar("@m", method.ToString(), 10)
           .ScalarInt();

    public bool Delete(int paymentId) =>
        con.Command("DELETE FROM dbo.Payments WHERE PaymentId = @id;", tx)
           .Int("@id", paymentId)
           .ExecuteNonQuery() == 1;

    private static Payment Map(SqlDataReader r) => new()
    {
        PaymentId = r.GetInt32(r.GetOrdinal("PaymentId")),
        ReservationId = r.GetInt32(r.GetOrdinal("ReservationId")),
        Amount = r.GetDecimal(r.GetOrdinal("Amount")),
        Currency = r.GetString(r.GetOrdinal("Currency")).Trim(),
        Method = r.Enum<PaymentMethod>("Method"),
        PaidAt = r.GetDateTime(r.GetOrdinal("PaidAt"))
    };
}
