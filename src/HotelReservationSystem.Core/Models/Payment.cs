namespace HotelReservationSystem.Core.Models;

public sealed class Payment
{
    public int PaymentId { get; set; }
    public int ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Currencies.Default;
    public PaymentMethod Method { get; set; }
    public DateTime PaidAt { get; set; }
}

public static class Currencies
{
    /// <summary>All reservations are priced in the hotel's base currency.</summary>
    public const string Default = "TRY";

    public static readonly IReadOnlyList<string> Supported = ["TRY", "USD", "EUR", "GBP"];

    public static bool IsSupported(string? code) =>
        code is not null && Supported.Contains(code, StringComparer.Ordinal);
}
