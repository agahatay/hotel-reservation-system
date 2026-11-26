namespace HotelReservationSystem.Core.Domain;

/// <summary>Orders room numbers naturally: "2" before "10", numeric before alphanumeric.</summary>
public sealed class RoomNumberComparer : IComparer<string>
{
    public static readonly RoomNumberComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var xn = int.TryParse(x, out var xi);
        var yn = int.TryParse(y, out var yi);
        if (xn && yn) return xi.CompareTo(yi);
        if (xn) return -1;
        if (yn) return 1;
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
