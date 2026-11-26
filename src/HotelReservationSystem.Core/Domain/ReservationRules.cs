namespace HotelReservationSystem.Core.Domain;

/// <summary>
/// Reservation business rules. Dates are hotel dates: check-in day is the first
/// occupied night, check-out day is NOT occupied (the room is free that night).
/// </summary>
public static class ReservationRules
{
    public const int MaxNights = 365;

    /// <summary>
    /// Two stays on the same room conflict when each starts before the other ends.
    /// Back-to-back stays (one checks out the day the other checks in) do not conflict.
    /// </summary>
    public static bool Overlaps(DateTime existingCheckIn, DateTime existingCheckOut,
                                DateTime newCheckIn, DateTime newCheckOut) =>
        existingCheckIn.Date < newCheckOut.Date && existingCheckOut.Date > newCheckIn.Date;

    /// <summary>Cancelled reservations release the room; every other status holds it.</summary>
    public static bool BlocksRoom(ReservationStatus status) => status != ReservationStatus.Cancelled;

    public static int Nights(DateTime checkIn, DateTime checkOut) =>
        (int)(checkOut.Date - checkIn.Date).TotalDays;

    /// <summary>Returns an error message, or null when the date range is acceptable for a new booking.</summary>
    public static string? ValidateNewStay(DateTime checkIn, DateTime checkOut, DateTime today)
    {
        if (checkIn.Date < today.Date)
            return "Check-in date cannot be in the past.";
        if (checkOut.Date <= checkIn.Date)
            return "Check-out date must be after the check-in date.";
        if (Nights(checkIn, checkOut) > MaxNights)
            return $"A reservation cannot be longer than {MaxNights} nights.";
        return null;
    }

    public static decimal CalculateTotal(DateTime checkIn, DateTime checkOut, decimal nightlyRate)
    {
        var nights = Nights(checkIn, checkOut);
        if (nights <= 0) throw new ArgumentException("Check-out must be after check-in.");
        if (nightlyRate <= 0) throw new ArgumentOutOfRangeException(nameof(nightlyRate), "Nightly rate must be positive.");
        return decimal.Round(nights * nightlyRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Only reservations that have not started yet can be cancelled.</summary>
    public static bool CanCancel(ReservationStatus status) => status == ReservationStatus.Booked;

    /// <summary>
    /// Date-driven status progression (same rule the original application applied):
    /// Booked becomes CheckedIn on the check-in day, and Booked/CheckedIn become
    /// Completed on the check-out day. Cancelled and Completed never change.
    /// </summary>
    public static ReservationStatus StatusForDate(ReservationStatus current, DateTime checkIn, DateTime checkOut, DateTime today)
    {
        if (current is ReservationStatus.Cancelled or ReservationStatus.Completed) return current;
        var d = today.Date;
        if (checkOut.Date <= d) return ReservationStatus.Completed;
        if (current == ReservationStatus.Booked && checkIn.Date <= d) return ReservationStatus.CheckedIn;
        return current;
    }
}
