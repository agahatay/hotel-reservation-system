using HotelReservationSystem.Core.Data;
using HotelReservationSystem.Core.Models;
using HotelReservationSystem.Core.Security;
using HotelReservationSystem.Core.Services;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.IntegrationTests;

/// <summary>
/// Creates a disposable test database from database/schema.sql. Requires a local SQL Server
/// (default <c>.\SQLEXPRESS</c>; override with the HOTELRES_TEST_SERVER environment variable).
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public const string DatabaseName = "HotelReservationDB_IntegrationTests";

    public static string Server => Environment.GetEnvironmentVariable("HOTELRES_TEST_SERVER") is { Length: > 0 } s ? s : @".\SQLEXPRESS";

    public DatabaseFixture()
    {
        RecreateDatabase(DatabaseName, includeSeed: false);
        ConnectionString = ConnectionStringFor(DatabaseName);
        Db = new SqlConnectionFactory(ConnectionString);

        Admin = CreateUser("it_admin", UserRole.Admin);
        Staff = CreateUser("it_staff", UserRole.Staff);
    }

    public string ConnectionString { get; }
    public SqlConnectionFactory Db { get; }
    public AuthenticatedUser Admin { get; }
    public AuthenticatedUser Staff { get; }
    public const string Password = "Password123";

    public AuthService Auth => new(Db);
    public GuestService Guests => new(Db);
    public RoomService Rooms => new(Db);
    public ReservationService Reservations => new(Db);
    public PaymentService Payments => new(Db);
    public ReportService Reports => new(Db);

    private int _counter;

    /// <summary>A unique, active room with the given nightly price.</summary>
    public Room NewRoom(decimal price = 1000m, bool active = true)
    {
        var n = Interlocked.Increment(ref _counter);
        var typeId = Rooms.SaveRoomType(Admin, new RoomType { TypeName = $"IT type {n}", Capacity = 2, BasePrice = price });
        var roomId = Rooms.SaveRoom(Admin, new Room { RoomNumber = $"T{n}", RoomTypeId = typeId, IsActive = active });
        return Rooms.ListRooms().Single(r => r.RoomId == roomId);
    }

    public int NewGuest(string? nationalId = null)
    {
        var n = Interlocked.Increment(ref _counter);
        return Guests.Save(Staff, new Guest { FirstName = "Test", LastName = $"Guest{n}", Email = $"guest{n}@example.com", NationalId = nationalId });
    }

    public AuthenticatedUser CreateUser(string username, UserRole role, string password = Password)
    {
        using var con = Db.Open();
        var id = new UserRepository(con).Insert(username, PasswordHasher.Hash(password, 1_000), role, mustChangePassword: false);
        return new AuthenticatedUser(id, username, role);
    }

    public T Scalar<T>(string sql, params (string Name, object Value)[] args)
    {
        using var con = Db.Open();
        using var cmd = new SqlCommand(sql, con);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    public static string ConnectionStringFor(string database) => new SqlConnectionStringBuilder
    {
        DataSource = Server, InitialCatalog = database, IntegratedSecurity = true,
        Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = true, Pooling = true, MaxPoolSize = 100
    }.ConnectionString;

    /// <summary>Drops (if present) and recreates a test database from the repository's SQL scripts.</summary>
    public static void RecreateDatabase(string database, bool includeSeed)
    {
        if (!database.EndsWith("Tests", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a database that is not a test database.");

        using (var master = new SqlConnection(ConnectionStringFor("master")))
        {
            master.Open();
            Execute(master, $"""
                IF DB_ID(N'{database}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{database}];
                END
                """);
        }
        SqlConnection.ClearAllPools();

        // Same code path the application uses ("Create / update database").
        DatabaseInstaller.Install(ConnectionStringFor(database), includeSeed);
    }

    private static void Execute(SqlConnection con, string sql)
    {
        using var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 };
        cmd.ExecuteNonQuery();
    }

    public static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HotelReservationSystem.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    public void Dispose()
    {
        // The database is left in place for inspection; it is dropped at the start of the next run.
        SqlConnection.ClearAllPools();
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
