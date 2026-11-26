using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

/// <summary>Single place where SQL Server connections are created.</summary>
public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public SqlConnection Open()
    {
        var con = new SqlConnection(_connectionString);
        con.Open();
        return con;
    }

    /// <summary>Server and database names only — never credentials — for display and logs.</summary>
    public string Describe()
    {
        var b = new SqlConnectionStringBuilder(_connectionString);
        return $"{b.DataSource} / {b.InitialCatalog}";
    }

    public bool TryConnect(out string? error)
    {
        try
        {
            using var con = Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users;", con);
            cmd.ExecuteScalar();
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is SqlException or ArgumentException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }
}
