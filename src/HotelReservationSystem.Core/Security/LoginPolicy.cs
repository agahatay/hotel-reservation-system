namespace HotelReservationSystem.Core.Security;

public static class LoginPolicy
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    public const int MinPasswordLength = 8;

    public static bool IsLockedOut(DateTime? lockoutEndUtc, DateTime nowUtc) =>
        lockoutEndUtc is { } end && end > nowUtc;

    /// <summary>State after a failed attempt: the counter resets once the account is locked.</summary>
    public static (int FailedCount, DateTime? LockoutEndUtc) AfterFailedAttempt(int failedCount, DateTime nowUtc)
    {
        var count = failedCount + 1;
        return count >= MaxFailedAttempts
            ? (0, nowUtc + LockoutDuration)
            : (count, null);
    }

    /// <summary>Returns an error message or null when the password is acceptable.</summary>
    public static string? ValidateNewPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";
        if (password.Length > 128)
            return "Password cannot exceed 128 characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must contain at least one letter and one digit.";
        return null;
    }

    public static string? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Username is required.";
        var u = username.Trim();
        if (u.Length is < 3 or > 50) return "Username must be 3–50 characters.";
        if (!u.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            return "Username may contain only letters, digits, '.', '_' and '-'.";
        return null;
    }
}
