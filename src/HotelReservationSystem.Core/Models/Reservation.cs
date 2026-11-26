namespace HotelReservationSystem.Core.Models;

public sealed class ReservationListItem
{
    public int ReservationId { get; set; }
    public int GuestId { get; set; }
    public string GuestName { get; set; } = "";
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public ReservationStatus Status { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = Currencies.Default;
    public DateTime CreatedAt { get; set; }

    public int Nights => ReservationRules.Nights(CheckInDate, CheckOutDate);
}

/// <summary>A reservation together with what has been paid against it.</summary>
public sealed class ReservationBalance
{
    public int ReservationId { get; set; }
    public string GuestName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public ReservationStatus Status { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = Currencies.Default;

    /// <summary>Amount still owed. A cancelled reservation owes nothing.</summary>
    public decimal Balance => Status == ReservationStatus.Cancelled ? 0m : PaymentRules.Balance(TotalPrice, PaidAmount);
}

public sealed record NewReservation(int GuestId, int RoomId, DateTime CheckInDate, DateTime CheckOutDate);
