namespace HotelReservationSystem.Core.Domain;

/// <summary>
/// Occupancy in room-nights. A night belongs to date D when a guest sleeps in the
/// room from D to D+1, so a stay occupies [CheckIn, CheckOut).
/// </summary>
public static class OccupancyCalculator
{
    /// <summary>Number of nights of the stay that fall inside [periodStart, periodEndExclusive).</summary>
    public static int NightsWithin(DateTime checkIn, DateTime checkOut, DateTime periodStart, DateTime periodEndExclusive)
    {
        var start = checkIn.Date > periodStart.Date ? checkIn.Date : periodStart.Date;
        var end = checkOut.Date < periodEndExclusive.Date ? checkOut.Date : periodEndExclusive.Date;
        return end > start ? (int)(end - start).TotalDays : 0;
    }

    /// <summary>
    /// Occupancy for a period. <paramref name="stays"/> must contain only non-cancelled
    /// reservations of the rooms counted in <paramref name="roomIds"/>; stays of other rooms are ignored.
    /// </summary>
    public static OccupancyResult Calculate(IEnumerable<StayInterval> stays, IReadOnlyCollection<int> roomIds,
                                            DateTime periodStart, DateTime periodEndExclusive)
    {
        var days = Math.Max(0, (int)(periodEndExclusive.Date - periodStart.Date).TotalDays);
        var rooms = roomIds as IReadOnlySet<int> ?? roomIds.ToHashSet();
        var available = rooms.Count * days;

        // Guard against double counting if data ever contains overlapping stays for one room.
        var occupiedNights = new HashSet<(int RoomId, DateTime Night)>();
        foreach (var s in stays)
        {
            if (!rooms.Contains(s.RoomId)) continue;
            foreach (var night in OccupiedNights(s.CheckInDate, s.CheckOutDate, periodStart, periodEndExclusive))
                occupiedNights.Add((s.RoomId, night));
        }

        return new OccupancyResult(occupiedNights.Count, available, rooms.Count);
    }

    public static IEnumerable<DateTime> OccupiedNights(DateTime checkIn, DateTime checkOut, DateTime periodStart, DateTime periodEndExclusive)
    {
        var start = checkIn.Date > periodStart.Date ? checkIn.Date : periodStart.Date;
        var end = checkOut.Date < periodEndExclusive.Date ? checkOut.Date : periodEndExclusive.Date;
        for (var d = start; d < end; d = d.AddDays(1))
            yield return d;
    }
}
