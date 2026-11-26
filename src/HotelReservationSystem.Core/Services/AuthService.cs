using System.Data;
using HotelReservationSystem.Core.Data;
using HotelReservationSystem.Core.Security;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Services;

public sealed record LoginResult(AuthenticatedUser? User, string? Error, bool MustChangePassword = false)
{
    public bool Succeeded => User is not null;
}

public sealed class AuthService(SqlConnectionFactory db, TimeProvider? clock = null)
{
    private const string InvalidCredentials = "Invalid username or password.";

    // Verified against when the username does not exist, so both paths cost the same time.
    private static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash(Guid.NewGuid().ToString()));

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public bool AnyUsers()
    {
        using var con = db.Open();
        return new UserRepository(con).Count() > 0;
    }

    public LoginResult Login(string username, string password)
    {
        username = username?.Trim() ?? "";
        if (username.Length == 0 || string.IsNullOrEmpty(password))
            return new(null, "Enter your username and password.");

        using var con = db.Open();
        var users = new UserRepository(con);
        var user = users.GetByUsername(username);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (user is null)
        {
            PasswordHasher.Verify(password, DummyHash.Value);
            return new(null, InvalidCredentials);
        }

        if (LoginPolicy.IsLockedOut(user.LockoutEndUtc, now))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutEndUtc!.Value - now).TotalMinutes));
            return new(null, $"Too many failed attempts. Try again in {minutes} minute(s).");
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            var (count, lockoutEnd) = LoginPolicy.AfterFailedAttempt(user.FailedLoginCount, now);
            users.RecordFailedLogin(user.UserId, count, lockoutEnd);
            return new(null, lockoutEnd is null
                ? InvalidCredentials
                : $"Too many failed attempts. The account is locked for {LoginPolicy.LockoutDuration.TotalMinutes:0} minutes.");
        }

        if (!user.IsActive)
            return new(null, "This account has been disabled. Contact an administrator.");

        users.RecordSuccessfulLogin(user.UserId);
        if (PasswordHasher.NeedsRehash(user.PasswordHash))
            users.UpdatePasswordHash(user.UserId, PasswordHasher.Hash(password), user.MustChangePassword);

        return new(new AuthenticatedUser(user.UserId, user.Username, user.Role), null, user.MustChangePassword);
    }

    /// <summary>First-run setup: creates the first administrator. Fails if any user already exists.</summary>
    public AuthenticatedUser CreateInitialAdmin(string username, string password)
    {
        username = ValidateCredentials(username, password);
        var hash = PasswordHasher.Hash(password);

        var id = Transactions.Run(db, IsolationLevel.Serializable, (con, tx) =>
        {
            var users = new UserRepository(con, tx);
            if (users.Count() > 0)
                throw new BusinessRuleException("The application has already been set up.");
            return users.Insert(username, hash, UserRole.Admin, mustChangePassword: false); // chosen by the admin
        });
        return new AuthenticatedUser(id, username, UserRole.Admin);
    }

    public List<UserAccount> ListUsers(AuthenticatedUser actor)
    {
        RolePermissions.Require(actor, Permission.ManageUsers);
        using var con = db.Open();
        return new UserRepository(con).List();
    }

    public int CreateUser(AuthenticatedUser actor, string username, string password, UserRole role)
    {
        RolePermissions.Require(actor, Permission.ManageUsers);
        username = ValidateCredentials(username, password);
        var hash = PasswordHasher.Hash(password);
        try
        {
            using var con = db.Open();
            // The administrator set this password, so the new user must replace it at first sign-in.
            return new UserRepository(con).Insert(username, hash, role, mustChangePassword: true);
        }
        catch (SqlException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"The username '{username}' is already taken.");
        }
    }

    public void UpdateUser(AuthenticatedUser actor, int userId, UserRole role, bool isActive)
    {
        RolePermissions.Require(actor, Permission.ManageUsers);
        if (userId == actor.UserId && (!isActive || role != UserRole.Admin))
            throw new BusinessRuleException("You cannot disable or demote your own account.");

        Transactions.Run(db, IsolationLevel.Serializable, (con, tx) =>
        {
            var users = new UserRepository(con, tx);
            var target = users.GetById(userId) ?? throw new BusinessRuleException("User not found.");
            var removesAdmin = target is { Role: UserRole.Admin, IsActive: true } && (role != UserRole.Admin || !isActive);
            if (removesAdmin && users.CountActiveAdmins() <= 1)
                throw new BusinessRuleException("At least one active administrator is required.");
            users.Update(userId, role, isActive);
        });
    }

    public void ResetPassword(AuthenticatedUser actor, int userId, string newPassword)
    {
        RolePermissions.Require(actor, Permission.ManageUsers);
        RequireValidPassword(newPassword);
        using var con = db.Open();
        var users = new UserRepository(con);
        if (users.GetById(userId) is null) throw new BusinessRuleException("User not found.");
        users.UpdatePasswordHash(userId, PasswordHasher.Hash(newPassword), mustChangePassword: true);
    }

    public void ChangeOwnPassword(AuthenticatedUser actor, string currentPassword, string newPassword)
    {
        RequireValidPassword(newPassword);
        using var con = db.Open();
        var users = new UserRepository(con);
        var user = users.GetById(actor.UserId) ?? throw new BusinessRuleException("User not found.");
        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            throw new BusinessRuleException("The current password is incorrect.");
        if (currentPassword == newPassword)
            throw new BusinessRuleException("The new password must be different from the current one.");
        users.UpdatePasswordHash(user.UserId, PasswordHasher.Hash(newPassword), mustChangePassword: false);
    }

    private static string ValidateCredentials(string username, string password)
    {
        if (LoginPolicy.ValidateUsername(username) is { } userError) throw new BusinessRuleException(userError);
        RequireValidPassword(password);
        return username.Trim();
    }

    private static void RequireValidPassword(string password)
    {
        if (LoginPolicy.ValidateNewPassword(password) is { } error) throw new BusinessRuleException(error);
    }
}
