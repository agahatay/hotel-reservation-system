using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelReservationSystem.Core.Configuration;

/// <summary>
/// Resolves the database connection string. First match wins:
/// <list type="number">
/// <item>Environment variable <c>HOTELRES_CONNECTION_STRING</c></item>
/// <item>User file <c>%APPDATA%\HotelReservationSystem\appsettings.json</c> (written by the in-app database settings)</item>
/// <item><c>appsettings.json</c> next to the executable (ships with the release)</item>
/// </list>
/// Integrated Windows authentication is the default; no credentials are stored by the application.
/// </summary>
public sealed class AppSettings
{
    public const string EnvironmentVariable = "HOTELRES_CONNECTION_STRING";
    public const string FileName = "appsettings.json";
    public const string DefaultConnectionString =
        "Server=.\\SQLEXPRESS;Database=HotelReservationDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public required string ConnectionString { get; init; }

    /// <summary>Human readable origin of the connection string (shown in error messages).</summary>
    public required string Source { get; init; }

    public static string UserSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "HotelReservationSystem", FileName);

    public static AppSettings Load(string applicationDirectory)
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env))
            return new AppSettings { ConnectionString = env, Source = $"environment variable {EnvironmentVariable}" };

        foreach (var path in new[] { UserSettingsPath, Path.Combine(applicationDirectory, FileName) })
        {
            if (TryRead(path) is { } cs)
                return new AppSettings { ConnectionString = cs, Source = path };
        }

        return new AppSettings { ConnectionString = DefaultConnectionString, Source = "built-in default" };
    }

    /// <summary>Saves a per-user connection string override.</summary>
    public static void SaveUserConnectionString(string connectionString)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath)!);
        var file = new SettingsFile { ConnectionStrings = new() { HotelDb = connectionString } };
        File.WriteAllText(UserSettingsPath, JsonSerializer.Serialize(file, JsonOptions));
    }

    internal static string? TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path), JsonOptions);
            var cs = file?.ConnectionStrings?.HotelDb;
            return string.IsNullOrWhiteSpace(cs) ? null : cs;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class SettingsFile
    {
        [JsonPropertyName("ConnectionStrings")]
        public ConnectionStringsSection? ConnectionStrings { get; set; }
    }

    private sealed class ConnectionStringsSection
    {
        [JsonPropertyName("HotelDb")]
        public string? HotelDb { get; set; }
    }
}
