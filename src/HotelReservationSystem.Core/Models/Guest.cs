namespace HotelReservationSystem.Core.Models;

public sealed class Guest
{
    public int GuestId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? NationalId { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>National ID / passport number with everything but the last 4 characters hidden.</summary>
    public string MaskedNationalId => Masking.MaskIdentifier(NationalId);
}

public static class Masking
{
    public static string MaskIdentifier(string? value, int visible = 4)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        value = value.Trim();
        if (value.Length <= visible) return new string('•', value.Length);
        return new string('•', value.Length - visible) + value[^visible..];
    }
}
