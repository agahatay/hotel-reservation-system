using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;
using HotelReservationSystem.Core.Security;

namespace HotelReservationSystem.Tests;

public class PasswordHasherTests
{
    private const int FastIterations = 1_000; // keeps the suite fast; the format is identical

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = PasswordHasher.Hash("Correct Horse 1", FastIterations);
        Assert.True(PasswordHasher.Verify("Correct Horse 1", hash));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hash = PasswordHasher.Hash("Secret123", FastIterations);
        Assert.False(PasswordHasher.Verify("secret123", hash));
        Assert.False(PasswordHasher.Verify("Secret123 ", hash));
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes_because_of_the_salt()
    {
        Assert.NotEqual(PasswordHasher.Hash("Secret123", FastIterations), PasswordHasher.Hash("Secret123", FastIterations));
    }

    [Fact]
    public void Hash_never_contains_the_plaintext()
    {
        var hash = PasswordHasher.Hash("Secret123", FastIterations);
        Assert.DoesNotContain("Secret123", hash);
        Assert.StartsWith("PBKDF2-SHA256$1000$", hash);
        Assert.True(hash.Length <= 200); // fits Users.PasswordHash nvarchar(200)
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Admin123!")]                       // legacy plaintext value must not "verify"
    [InlineData("PBKDF2-SHA256$abc$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$1000$not-base64$xx")]
    [InlineData("MD5$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Malformed_or_plaintext_hashes_never_verify(string? stored) =>
        Assert.False(PasswordHasher.Verify("Admin123!", stored));

    [Fact]
    public void Default_hash_uses_current_iterations_and_needs_no_rehash()
    {
        var hash = PasswordHasher.Hash("Secret123");
        Assert.Contains($"${PasswordHasher.DefaultIterations}$", hash);
        Assert.False(PasswordHasher.NeedsRehash(hash));
        Assert.True(PasswordHasher.NeedsRehash(PasswordHasher.Hash("Secret123", FastIterations)));
    }

    [Fact]
    public void Seed_script_demo_hashes_match_documented_passwords()
    {
        var seed = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "database", "seed.sql"));
        var hashes = System.Text.RegularExpressions.Regex.Matches(seed, @"PBKDF2-SHA256\$\d+\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+")
                           .Select(m => m.Value).ToList();
        Assert.Equal(2, hashes.Count);
        Assert.True(PasswordHasher.Verify("Admin123!", hashes[0]));
        Assert.True(PasswordHasher.Verify("Staff123!", hashes[1]));
    }
}

public class LoginPolicyTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Failed_attempts_below_limit_only_increment_the_counter()
    {
        var (count, lockout) = LoginPolicy.AfterFailedAttempt(LoginPolicy.MaxFailedAttempts - 2, Now);
        Assert.Equal(LoginPolicy.MaxFailedAttempts - 1, count);
        Assert.Null(lockout);
    }

    [Fact]
    public void Reaching_the_limit_locks_the_account_and_resets_the_counter()
    {
        var (count, lockout) = LoginPolicy.AfterFailedAttempt(LoginPolicy.MaxFailedAttempts - 1, Now);
        Assert.Equal(0, count);
        Assert.Equal(Now + LoginPolicy.LockoutDuration, lockout);
    }

    [Fact]
    public void Lockout_expires()
    {
        var end = Now.AddMinutes(5);
        Assert.True(LoginPolicy.IsLockedOut(end, Now));
        Assert.False(LoginPolicy.IsLockedOut(end, end));
        Assert.False(LoginPolicy.IsLockedOut(null, Now));
    }

    [Theory]
    [InlineData("short1", false)]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    [InlineData("letters123", true)]
    [InlineData("Admin123!", true)]
    public void New_password_policy(string password, bool valid) =>
        Assert.Equal(valid, LoginPolicy.ValidateNewPassword(password) is null);

    [Theory]
    [InlineData("ab", false)]
    [InlineData("admin", true)]
    [InlineData("front.desk_01", true)]
    [InlineData("bad name", false)]
    [InlineData("x'; DROP TABLE Users;--", false)]
    public void Username_policy(string username, bool valid) =>
        Assert.Equal(valid, LoginPolicy.ValidateUsername(username) is null);
}

public class RolePermissionsTests
{
    [Theory]
    [InlineData(Permission.ViewDashboard, true)]
    [InlineData(Permission.ManageGuests, true)]
    [InlineData(Permission.ManageReservations, true)]
    [InlineData(Permission.TakePayments, true)]
    [InlineData(Permission.VoidPayments, false)]
    [InlineData(Permission.ManageRooms, false)]
    [InlineData(Permission.ManageUsers, false)]
    public void Staff_permissions(Permission permission, bool expected) =>
        Assert.Equal(expected, RolePermissions.Has(UserRole.Staff, permission));

    [Fact]
    public void Admin_has_every_permission() =>
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(RolePermissions.Has(UserRole.Admin, p)));

    [Fact]
    public void Require_throws_for_missing_permission_or_anonymous_user()
    {
        var staff = new AuthenticatedUser(2, "staff", UserRole.Staff);
        Assert.Throws<AuthorizationException>(() => RolePermissions.Require(staff, Permission.ManageRooms));
        Assert.Throws<AuthorizationException>(() => RolePermissions.Require(null, Permission.ViewDashboard));
        RolePermissions.Require(staff, Permission.TakePayments); // does not throw
    }
}
