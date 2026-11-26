using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public sealed class RoomRepository(SqlConnection con, SqlTransaction? tx = null)
{
    private const string RoomSelect = """
        SELECT r.RoomId, r.RoomNumber, r.RoomTypeId, r.Floor, r.IsActive,
               rt.TypeName, rt.Capacity, rt.BasePrice
        FROM dbo.Rooms r
        JOIN dbo.RoomTypes rt ON rt.RoomTypeId = r.RoomTypeId
        """;

    // ---------- Room types ----------

    public List<RoomType> ListRoomTypes() =>
        con.Command("SELECT RoomTypeId, TypeName, Capacity, BasePrice FROM dbo.RoomTypes ORDER BY BasePrice, TypeName;", tx)
           .ReadAll(r => new RoomType
           {
               RoomTypeId = r.GetInt32(0),
               TypeName = r.GetString(1),
               Capacity = r.GetInt32(2),
               BasePrice = r.GetDecimal(3)
           });

    public int InsertRoomType(RoomType t) =>
        con.Command("""
            INSERT INTO dbo.RoomTypes (TypeName, Capacity, BasePrice)
            OUTPUT INSERTED.RoomTypeId
            VALUES (@n, @c, @p);
            """, tx)
           .NVarChar("@n", t.TypeName, 50)
           .Int("@c", t.Capacity)
           .Money("@p", t.BasePrice)
           .ScalarInt();

    /// <summary>
    /// Changing the base price affects only future bookings — each reservation
    /// stores its own TotalPrice when it is created.
    /// </summary>
    public bool UpdateRoomType(RoomType t) =>
        con.Command("UPDATE dbo.RoomTypes SET TypeName = @n, Capacity = @c, BasePrice = @p WHERE RoomTypeId = @id;", tx)
           .NVarChar("@n", t.TypeName, 50)
           .Int("@c", t.Capacity)
           .Money("@p", t.BasePrice)
           .Int("@id", t.RoomTypeId)
           .ExecuteNonQuery() == 1;

    public bool RoomTypeInUse(int roomTypeId) =>
        con.Command("SELECT COUNT(*) FROM dbo.Rooms WHERE RoomTypeId = @id;", tx)
           .Int("@id", roomTypeId)
           .ScalarInt() > 0;

    public bool DeleteRoomType(int roomTypeId) =>
        con.Command("DELETE FROM dbo.RoomTypes WHERE RoomTypeId = @id;", tx)
           .Int("@id", roomTypeId)
           .ExecuteNonQuery() == 1;

    // ---------- Rooms ----------

    public List<Room> ListRooms(bool activeOnly = false)
    {
        var rooms = con.Command($"{RoomSelect} WHERE (@activeOnly = 0 OR r.IsActive = 1);", tx)
                       .Bit("@activeOnly", activeOnly)
                       .ReadAll(MapRoom);
        rooms.Sort((a, b) => RoomNumberComparer.Instance.Compare(a.RoomNumber, b.RoomNumber));
        return rooms;
    }

    /// <summary>
    /// Active rooms with no non-cancelled reservation overlapping [checkIn, checkOut).
    /// Informational only — the booking itself re-checks availability inside a transaction.
    /// </summary>
    public List<Room> ListAvailableRooms(DateTime checkIn, DateTime checkOut)
    {
        var rooms = con.Command($"""
            {RoomSelect}
            WHERE r.IsActive = 1
              AND NOT EXISTS (
                    SELECT 1 FROM dbo.Reservations res
                    WHERE res.RoomId = r.RoomId
                      AND res.Status <> N'Cancelled'
                      AND res.CheckInDate < @checkOut
                      AND res.CheckOutDate > @checkIn);
            """, tx)
           .Date("@checkIn", checkIn)
           .Date("@checkOut", checkOut)
           .ReadAll(MapRoom);
        rooms.Sort((a, b) => RoomNumberComparer.Instance.Compare(a.RoomNumber, b.RoomNumber));
        return rooms;
    }

    /// <summary>
    /// Reads the room and takes an update lock on its row until the transaction ends.
    /// All bookings of the same room serialize on this lock.
    /// </summary>
    public Room? GetRoomForBooking(int roomId) =>
        con.Command("""
            SELECT r.RoomId, r.RoomNumber, r.RoomTypeId, r.Floor, r.IsActive,
                   rt.TypeName, rt.Capacity, rt.BasePrice
            FROM dbo.Rooms r WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.RoomTypes rt ON rt.RoomTypeId = r.RoomTypeId
            WHERE r.RoomId = @id;
            """, tx)
           .Int("@id", roomId)
           .ReadSingle(MapRoom);

    public bool RoomNumberExists(string roomNumber, int? excludingRoomId = null) =>
        con.Command("SELECT COUNT(*) FROM dbo.Rooms WHERE RoomNumber = @n AND (@id IS NULL OR RoomId <> @id);", tx)
           .NVarChar("@n", roomNumber, 10)
           .Int("@id", excludingRoomId)
           .ScalarInt() > 0;

    public int InsertRoom(Room room) =>
        con.Command("""
            INSERT INTO dbo.Rooms (RoomNumber, RoomTypeId, Floor, IsActive)
            OUTPUT INSERTED.RoomId
            VALUES (@n, @t, @f, @a);
            """, tx)
           .NVarChar("@n", room.RoomNumber, 10)
           .Int("@t", room.RoomTypeId)
           .Int("@f", room.Floor)
           .Bit("@a", room.IsActive)
           .ScalarInt();

    public bool UpdateRoom(Room room) =>
        con.Command("UPDATE dbo.Rooms SET RoomNumber = @n, RoomTypeId = @t, Floor = @f, IsActive = @a WHERE RoomId = @id;", tx)
           .NVarChar("@n", room.RoomNumber, 10)
           .Int("@t", room.RoomTypeId)
           .Int("@f", room.Floor)
           .Bit("@a", room.IsActive)
           .Int("@id", room.RoomId)
           .ExecuteNonQuery() == 1;

    public bool SetRoomActive(int roomId, bool isActive) =>
        con.Command("UPDATE dbo.Rooms SET IsActive = @a WHERE RoomId = @id;", tx)
           .Bit("@a", isActive)
           .Int("@id", roomId)
           .ExecuteNonQuery() == 1;

    public bool RoomHasReservations(int roomId) =>
        con.Command("SELECT COUNT(*) FROM dbo.Reservations WHERE RoomId = @id;", tx)
           .Int("@id", roomId)
           .ScalarInt() > 0;

    public bool DeleteRoom(int roomId) =>
        con.Command("DELETE FROM dbo.Rooms WHERE RoomId = @id;", tx)
           .Int("@id", roomId)
           .ExecuteNonQuery() == 1;

    private static Room MapRoom(SqlDataReader r) => new()
    {
        RoomId = r.GetInt32(r.GetOrdinal("RoomId")),
        RoomNumber = r.GetString(r.GetOrdinal("RoomNumber")),
        RoomTypeId = r.GetInt32(r.GetOrdinal("RoomTypeId")),
        Floor = r.NullableInt("Floor"),
        IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
        TypeName = r.GetString(r.GetOrdinal("TypeName")),
        Capacity = r.GetInt32(r.GetOrdinal("Capacity")),
        BasePrice = r.GetDecimal(r.GetOrdinal("BasePrice"))
    };
}
