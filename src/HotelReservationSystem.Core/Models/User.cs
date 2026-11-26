namespace HotelReservationSystem.Core.Models;

public sealed class UserAccount
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>The password is temporary (demo account, new account or admin reset) and must be changed at sign-in.</summary>
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>The signed-in user for the current application session.</summary>
public sealed record AuthenticatedUser(int UserId, string Username, UserRole Role);
