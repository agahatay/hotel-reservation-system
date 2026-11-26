using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public sealed class UserRepository(SqlConnection con, SqlTransaction? tx = null)
{
    private const string Columns =
        "UserId, Username, PasswordHash, Role, IsActive, FailedLoginCount, LockoutEndUtc, MustChangePassword, CreatedAt";

    public int Count() =>
        con.Command("SELECT COUNT(*) FROM dbo.Users;", tx).ScalarInt();

    public int CountActiveAdmins() =>
        con.Command("SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin' AND IsActive = 1;", tx).ScalarInt();

    public UserAccount? GetByUsername(string username) =>
        con.Command($"SELECT {Columns} FROM dbo.Users WHERE Username = @u;", tx)
           .NVarChar("@u", username, 50)
           .ReadSingle(Map);

    public UserAccount? GetById(int userId) =>
        con.Command($"SELECT {Columns} FROM dbo.Users WHERE UserId = @id;", tx)
           .Int("@id", userId)
           .ReadSingle(Map);

    public List<UserAccount> List() =>
        con.Command($"SELECT {Columns} FROM dbo.Users ORDER BY Username;", tx).ReadAll(Map);

    public int Insert(string username, string passwordHash, UserRole role, bool mustChangePassword) =>
        con.Command("""
            INSERT INTO dbo.Users (Username, PasswordHash, Role, MustChangePassword)
            OUTPUT INSERTED.UserId
            VALUES (@u, @h, @r, @m);
            """, tx)
           .NVarChar("@u", username, 50)
           .NVarChar("@h", passwordHash, 200)
           .NVarChar("@r", role.ToString(), 20)
           .Bit("@m", mustChangePassword)
           .ScalarInt();

    public void RecordFailedLogin(int userId, int failedCount, DateTime? lockoutEndUtc) =>
        con.Command("UPDATE dbo.Users SET FailedLoginCount = @c, LockoutEndUtc = @l WHERE UserId = @id;", tx)
           .Int("@c", failedCount)
           .DateTime2("@l", lockoutEndUtc)
           .Int("@id", userId)
           .ExecuteNonQuery();

    public void RecordSuccessfulLogin(int userId) =>
        con.Command("UPDATE dbo.Users SET FailedLoginCount = 0, LockoutEndUtc = NULL WHERE UserId = @id;", tx)
           .Int("@id", userId)
           .ExecuteNonQuery();

    public void UpdatePasswordHash(int userId, string passwordHash, bool mustChangePassword) =>
        con.Command("""
            UPDATE dbo.Users
            SET PasswordHash = @h, MustChangePassword = @m, FailedLoginCount = 0, LockoutEndUtc = NULL
            WHERE UserId = @id;
            """, tx)
           .NVarChar("@h", passwordHash, 200)
           .Bit("@m", mustChangePassword)
           .Int("@id", userId)
           .ExecuteNonQuery();

    public void Update(int userId, UserRole role, bool isActive) =>
        con.Command("UPDATE dbo.Users SET Role = @r, IsActive = @a WHERE UserId = @id;", tx)
           .NVarChar("@r", role.ToString(), 20)
           .Bit("@a", isActive)
           .Int("@id", userId)
           .ExecuteNonQuery();

    private static UserAccount Map(SqlDataReader r) => new()
    {
        UserId = r.GetInt32(r.GetOrdinal("UserId")),
        Username = r.GetString(r.GetOrdinal("Username")),
        PasswordHash = r.GetString(r.GetOrdinal("PasswordHash")),
        Role = r.Enum<UserRole>("Role"),
        IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
        FailedLoginCount = r.GetInt32(r.GetOrdinal("FailedLoginCount")),
        LockoutEndUtc = r.NullableDateTime("LockoutEndUtc"),
        MustChangePassword = r.GetBoolean(r.GetOrdinal("MustChangePassword")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };
}
