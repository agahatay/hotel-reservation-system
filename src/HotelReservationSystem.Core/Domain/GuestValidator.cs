using System.Text.RegularExpressions;

namespace HotelReservationSystem.Core.Domain;

public static partial class GuestValidator
{
    /// <summary>Trims all fields and turns blank optional fields into null.</summary>
    public static Guest Normalize(Guest g) => new()
    {
        GuestId = g.GuestId,
        FirstName = g.FirstName?.Trim() ?? "",
        LastName = g.LastName?.Trim() ?? "",
        Phone = Blank(g.Phone),
        Email = Blank(g.Email),
        NationalId = Blank(g.NationalId),
        CreatedAt = g.CreatedAt
    };

    /// <summary>Returns all validation errors for an already normalized guest.</summary>
    public static IReadOnlyList<string> Validate(Guest g)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(g.FirstName)) errors.Add("First name is required.");
        else if (g.FirstName.Length > 50) errors.Add("First name cannot exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(g.LastName)) errors.Add("Last name is required.");
        else if (g.LastName.Length > 50) errors.Add("Last name cannot exceed 50 characters.");

        if (g.Phone is not null && (g.Phone.Length > 20 || !PhoneRegex().IsMatch(g.Phone)))
            errors.Add("Phone may contain only digits, spaces, +, -, ( ) and be at most 20 characters.");

        if (g.Email is not null && (g.Email.Length > 100 || !EmailRegex().IsMatch(g.Email)))
            errors.Add("Email address is not valid.");

        if (g.NationalId is not null && (g.NationalId.Length > 30 || !IdRegex().IsMatch(g.NationalId)))
            errors.Add("National ID / passport may contain only letters and digits (max 30).");

        return errors;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex(@"^\+?[0-9 ()\-]{5,20}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{3,30}$")]
    private static partial Regex IdRegex();
}
