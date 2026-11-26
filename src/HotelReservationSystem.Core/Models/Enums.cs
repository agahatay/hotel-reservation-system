namespace HotelReservationSystem.Core.Models;

public enum UserRole
{
    Admin,
    Staff
}

/// <summary>
/// Reservation lifecycle. Stored in the database as the enum name.
/// Booked -> CheckedIn -> Completed happens automatically by date;
/// Booked -> Cancelled is a manual action.
/// </summary>
public enum ReservationStatus
{
    Booked,
    CheckedIn,
    Completed,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    Card
}
