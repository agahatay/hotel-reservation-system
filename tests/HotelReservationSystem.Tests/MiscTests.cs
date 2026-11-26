using HotelReservationSystem.Core.Configuration;
using HotelReservationSystem.Core.Data;
using HotelReservationSystem.Core.Domain;

namespace HotelReservationSystem.Tests;

internal static class TestPaths
{
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
}

public class RoomNumberComparerTests
{
    [Fact]
    public void Sorts_room_numbers_naturally()
    {
        var rooms = new List<string> { "1001", "201", "A2", "101", "21", "A10" };
        rooms.Sort(RoomNumberComparer.Instance);
        Assert.Equal(["21", "101", "201", "1001", "A10", "A2"], rooms);
    }
}

public class LikeEscapingTests
{
    [Theory]
    [InlineData("100%", "100[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[x]", "[[]x]")]
    [InlineData("plain", "plain")]
    public void Wildcards_are_matched_literally(string input, string expected) =>
        Assert.Equal(expected, GuestRepository.EscapeLike(input));
}

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hrs-settings-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Reads_connection_string_from_appsettings_with_comments()
    {
        var path = Path.Combine(_dir, AppSettings.FileName);
        File.WriteAllText(path, """
            {
              // comment
              "ConnectionStrings": { "HotelDb": "Server=test;Database=X;Integrated Security=True" },
            }
            """);
        Assert.Equal("Server=test;Database=X;Integrated Security=True", AppSettings.TryRead(path));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"ConnectionStrings\": { \"HotelDb\": \"  \" } }")]
    [InlineData("{}")]
    public void Invalid_or_empty_files_are_ignored(string content)
    {
        var path = Path.Combine(_dir, AppSettings.FileName);
        File.WriteAllText(path, content);
        Assert.Null(AppSettings.TryRead(path));
    }

    [Fact]
    public void Shipped_appsettings_uses_integrated_security_and_no_password()
    {
        var path = Path.Combine(TestPaths.RepositoryRoot, "src", "HotelReservationSystem", AppSettings.FileName);
        var cs = AppSettings.TryRead(path);
        Assert.NotNull(cs);
        Assert.Contains("Integrated Security=True", cs);
        Assert.DoesNotContain("Password", cs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID", cs, StringComparison.OrdinalIgnoreCase);
    }
}

public class DatabaseInstallerTests
{
    [Fact]
    public void Embedded_scripts_match_the_repository_scripts()
    {
        foreach (var name in new[] { "schema.sql", "seed.sql" })
        {
            var onDisk = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "database", name));
            Assert.Equal(onDisk, DatabaseInstaller.ReadScript(name));
        }
    }

    [Fact]
    public void Scripts_are_split_on_GO_lines_only()
    {
        var batches = DatabaseInstaller.SplitBatches("SELECT 1;\nGO\n  go  \nSELECT 'GOOD';\r\nGO\r\n");
        Assert.Equal(2, batches.Count);
        Assert.Contains("GOOD", batches[1]);
    }

    [Fact]
    public void Demo_accounts_are_seeded_with_forced_password_change()
    {
        var seed = DatabaseInstaller.ReadScript("seed.sql");
        Assert.Contains("MustChangePassword", seed);
        Assert.Contains("N'Admin', 1)", seed);
        Assert.Contains("N'Staff', 1)", seed);
    }

    [Fact]
    public void Unsafe_database_names_are_rejected() =>
        Assert.Throws<ArgumentException>(() =>
            DatabaseInstaller.Install("Server=.;Database=x];DROP DATABASE y;--;Integrated Security=True", includeDemoData: false));
}
