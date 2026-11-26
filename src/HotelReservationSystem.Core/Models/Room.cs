namespace HotelReservationSystem.Core.Models;

public sealed class RoomType
{
    public int RoomTypeId { get; set; }
    public string TypeName { get; set; } = "";
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }

    public override string ToString() => TypeName;
}

public sealed class Room
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = "";
    public int RoomTypeId { get; set; }
    public string TypeName { get; set; } = "";
    public int Capacity { get; set; }
    public int? Floor { get; set; }
    public bool IsActive { get; set; }
    public decimal BasePrice { get; set; }

    public string DisplayText => $"{RoomNumber} | {TypeName} | {BasePrice:N2} / night";
}
