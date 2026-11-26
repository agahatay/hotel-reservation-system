namespace HotelReservationSystem.Core.Models;

public sealed record StayInterval(int RoomId, DateTime CheckInDate, DateTime CheckOutDate);

public sealed record CurrencyTotal(string Currency, decimal Amount);

public sealed record RoomUsage(string RoomNumber, int Reservations, int Nights);

public sealed class DashboardSummary
{
    public int TotalReservations { get; init; }
    public int ActiveReservations { get; init; }
    public int CompletedReservations { get; init; }
    public int CancelledReservations { get; init; }
    public IReadOnlyList<CurrencyTotal> IncomeByCurrency { get; init; } = [];
    public OccupancyResult Occupancy { get; init; } = new(0, 0, 0);
    public string? TopGuest { get; init; }
    public int TopGuestReservations { get; init; }
    public IReadOnlyList<RoomUsage> TopRooms { get; init; } = [];
    public IReadOnlyList<ReservationListItem> Reservations { get; init; } = [];
}

/// <summary>Occupancy expressed in room-nights.</summary>
public sealed record OccupancyResult(int OccupiedRoomNights, int AvailableRoomNights, int RoomCount)
{
    public decimal Rate => AvailableRoomNights == 0 ? 0m : (decimal)OccupiedRoomNights / AvailableRoomNights;
}

public sealed class MonthlyOccupancy
{
    public DateTime MonthStart { get; init; }
    public int DaysInMonth { get; init; }
    public IReadOnlyList<Room> Rooms { get; init; } = [];

    /// <summary>Key: RoomId, value: set of occupied day numbers (1-based).</summary>
    public IReadOnlyDictionary<int, IReadOnlySet<int>> OccupiedDays { get; init; } = new Dictionary<int, IReadOnlySet<int>>();
}
