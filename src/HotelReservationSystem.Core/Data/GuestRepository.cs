using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public sealed class GuestRepository(SqlConnection con, SqlTransaction? tx = null)
{
    private const string Columns = "GuestId, FirstName, LastName, Phone, Email, NationalId, CreatedAt";

    /// <summary>Lists guests, optionally filtered by name, phone, email or national ID.</summary>
    public List<Guest> Search(string? text)
    {
        var q = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return con.Command($"""
            SELECT {Columns}
            FROM dbo.Guests
            WHERE @q IS NULL
               OR FirstName  LIKE N'%' + @q + N'%'
               OR LastName   LIKE N'%' + @q + N'%'
               OR (FirstName + N' ' + LastName) LIKE N'%' + @q + N'%'
               OR Phone      LIKE N'%' + @q + N'%'
               OR Email      LIKE N'%' + @q + N'%'
               OR NationalId LIKE N'%' + @q + N'%'
            ORDER BY LastName, FirstName, GuestId;
            """, tx)
           .NVarChar("@q", q is null ? null : EscapeLike(q), 110)
           .ReadAll(Map);
    }

    public Guest? GetById(int guestId) =>
        con.Command($"SELECT {Columns} FROM dbo.Guests WHERE GuestId = @id;", tx)
           .Int("@id", guestId)
           .ReadSingle(Map);

    public int Insert(Guest g) =>
        con.Command("""
            INSERT INTO dbo.Guests (FirstName, LastName, Phone, Email, NationalId)
            OUTPUT INSERTED.GuestId
            VALUES (@f, @l, @p, @e, @n);
            """, tx)
           .NVarChar("@f", g.FirstName, 50)
           .NVarChar("@l", g.LastName, 50)
           .NVarChar("@p", g.Phone, 20)
           .NVarChar("@e", g.Email, 100)
           .NVarChar("@n", g.NationalId, 30)
           .ScalarInt();

    public bool Update(Guest g) =>
        con.Command("""
            UPDATE dbo.Guests
            SET FirstName = @f, LastName = @l, Phone = @p, Email = @e, NationalId = @n
            WHERE GuestId = @id;
            """, tx)
           .NVarChar("@f", g.FirstName, 50)
           .NVarChar("@l", g.LastName, 50)
           .NVarChar("@p", g.Phone, 20)
           .NVarChar("@e", g.Email, 100)
           .NVarChar("@n", g.NationalId, 30)
           .Int("@id", g.GuestId)
           .ExecuteNonQuery() == 1;

    public bool HasReservations(int guestId) =>
        con.Command("SELECT COUNT(*) FROM dbo.Reservations WHERE GuestId = @id;", tx)
           .Int("@id", guestId)
           .ScalarInt() > 0;

    public bool Delete(int guestId) =>
        con.Command("DELETE FROM dbo.Guests WHERE GuestId = @id;", tx)
           .Int("@id", guestId)
           .ExecuteNonQuery() == 1;

    /// <summary>Escapes LIKE wildcards so user input is matched literally.</summary>
    internal static string EscapeLike(string s) =>
        s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    private static Guest Map(SqlDataReader r) => new()
    {
        GuestId = r.GetInt32(r.GetOrdinal("GuestId")),
        FirstName = r.GetString(r.GetOrdinal("FirstName")),
        LastName = r.GetString(r.GetOrdinal("LastName")),
        Phone = r.NullableString("Phone"),
        Email = r.NullableString("Email"),
        NationalId = r.NullableString("NationalId"),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };
}
