using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

/// <summary>
/// Creates or upgrades the application database from the SQL scripts in /database, which are
/// embedded in this assembly. This lets the application set up a fresh SQL Server Express or
/// LocalDB instance without sqlcmd. The scripts are idempotent: existing data is never changed,
/// and demo data is only inserted into a database without users.
/// </summary>
public static partial class DatabaseInstaller
{
    /// <summary>Database name used inside the script files; replaced by the configured name.</summary>
    public const string ScriptDatabaseName = "HotelReservationDB";

    public static void Install(string connectionString, bool includeDemoData)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var database = target.InitialCatalog;
        if (!SafeName().IsMatch(database))
            throw new ArgumentException("The database name may contain only letters, digits and underscores.");

        // Connect to master, because the target database may not exist yet.
        target.InitialCatalog = "master";
        using var con = new SqlConnection(target.ConnectionString);
        con.Open();

        Run(con, ReadScript("schema.sql"), database);
        if (includeDemoData) Run(con, ReadScript("seed.sql"), database);
        SqlConnection.ClearAllPools();
    }

    public static string ReadScript(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Scripts.{name}")
                           ?? throw new InvalidOperationException($"Embedded script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Splits a script on "GO" lines, like sqlcmd and SSMS do.</summary>
    public static IReadOnlyList<string> SplitBatches(string script) =>
        BatchSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();

    private static void Run(SqlConnection con, string script, string database)
    {
        script = script.Replace(ScriptDatabaseName, database, StringComparison.Ordinal);
        foreach (var batch in SplitBatches(script))
        {
            using var cmd = new SqlCommand(batch, con) { CommandTimeout = 120 };
            cmd.ExecuteNonQuery();
        }
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BatchSeparator();

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,100}$")]
    private static partial Regex SafeName();
}
