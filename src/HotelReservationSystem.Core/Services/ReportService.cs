using HotelReservationSystem.Core.Data;

namespace HotelReservationSystem.Core.Services;

public sealed class ReportService(SqlConnectionFactory db)
{
    /// <param name="from">First day of the period.</param>
    /// <param name="toInclusive">Last day of the period (inclusive).</param>
    public DashboardSummary GetDashboard(DateTime from, DateTime toInclusive)
    {
        if (toInclusive.Date < from.Date) throw new BusinessRuleException("The end date must not be before the start date.");
        var start = from.Date;
        var endExclusive = toInclusive.Date.AddDays(1);

        using var con = db.Open();
        var reports = new ReportRepository(con);
        var counts = reports.CountByStatus(start, endExclusive);
        int Count(ReservationStatus s) => counts.GetValueOrDefault(s);

        var activeRoomIds = new RoomRepository(con).ListRooms(activeOnly: true).Select(r => r.RoomId).ToHashSet();
        var occupancy = OccupancyCalculator.Calculate(reports.Stays(start, endExclusive), activeRoomIds, start, endExclusive);
        var topGuest = reports.TopGuest(start, endExclusive);

        return new DashboardSummary
        {
            TotalReservations = counts.Values.Sum(),
            ActiveReservations = Count(ReservationStatus.Booked) + Count(ReservationStatus.CheckedIn),
            CompletedReservations = Count(ReservationStatus.Completed),
            CancelledReservations = Count(ReservationStatus.Cancelled),
            IncomeByCurrency = reports.IncomeByCurrency(start, endExclusive),
            Occupancy = occupancy,
            TopGuest = topGuest?.Name,
            TopGuestReservations = topGuest?.Count ?? 0,
            TopRooms = reports.TopRooms(start, endExclusive, 10),
            Reservations = new ReservationRepository(con).ListByCheckIn(start, endExclusive, 200)
        };
    }

    /// <summary>Room × day occupancy grid for one calendar month.</summary>
    public MonthlyOccupancy GetMonthlyOccupancy(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        using var con = db.Open();
        var stays = new ReportRepository(con).Stays(monthStart, monthEnd);
        var roomIdsWithStays = stays.Select(s => s.RoomId).ToHashSet();

        // Active rooms, plus inactive rooms that still have stays this month.
        var rooms = new RoomRepository(con).ListRooms()
            .Where(r => r.IsActive || roomIdsWithStays.Contains(r.RoomId))
            .ToList();

        var occupied = rooms.ToDictionary(r => r.RoomId, _ => new HashSet<int>());
        foreach (var s in stays)
        {
            if (!occupied.TryGetValue(s.RoomId, out var days)) continue;
            foreach (var night in OccupancyCalculator.OccupiedNights(s.CheckInDate, s.CheckOutDate, monthStart, monthEnd))
                days.Add(night.Day);
        }

        return new MonthlyOccupancy
        {
            MonthStart = monthStart,
            DaysInMonth = DateTime.DaysInMonth(year, month),
            Rooms = rooms,
            OccupiedDays = occupied.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<int>)kv.Value)
        };
    }
}
