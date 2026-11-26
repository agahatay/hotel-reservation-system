using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.Tests;

public class OccupancyCalculatorTests
{
    private static DateTime D(int day) => new(2026, 4, day);

    [Theory]
    [InlineData(5, 8, 1, 11, 3)]    // fully inside the period
    [InlineData(1, 4, 3, 11, 1)]    // starts before the period
    [InlineData(9, 14, 1, 11, 2)]   // ends after the period
    [InlineData(1, 30, 10, 12, 2)]  // covers the period
    [InlineData(1, 3, 3, 11, 0)]    // check-out on the first day: no night inside
    [InlineData(11, 13, 1, 11, 0)]  // check-in on the exclusive end
    public void NightsWithin_clips_stays_to_the_period(int checkIn, int checkOut, int from, int toExclusive, int expected) =>
        Assert.Equal(expected, OccupancyCalculator.NightsWithin(D(checkIn), D(checkOut), D(from), D(toExclusive)));

    [Fact]
    public void Calculate_returns_room_nights_over_available_room_nights()
    {
        var rooms = new[] { 1, 2 };
        var stays = new[]
        {
            new StayInterval(1, D(1), D(4)),  // 3 nights
            new StayInterval(2, D(3), D(5))   // 2 nights
        };

        // 10 nights × 2 rooms = 20 available
        var result = OccupancyCalculator.Calculate(stays, rooms, D(1), D(11));

        Assert.Equal(5, result.OccupiedRoomNights);
        Assert.Equal(20, result.AvailableRoomNights);
        Assert.Equal(0.25m, result.Rate);
    }

    [Fact]
    public void Calculate_ignores_rooms_outside_the_room_set()
    {
        var result = OccupancyCalculator.Calculate([new StayInterval(99, D(1), D(5))], [1], D(1), D(11));
        Assert.Equal(0, result.OccupiedRoomNights);
    }

    [Fact]
    public void Calculate_never_double_counts_a_room_night()
    {
        var stays = new[] { new StayInterval(1, D(1), D(5)), new StayInterval(1, D(3), D(6)) };
        var result = OccupancyCalculator.Calculate(stays, [1], D(1), D(11));
        Assert.Equal(5, result.OccupiedRoomNights); // nights 1,2,3,4,5
    }

    [Fact]
    public void Calculate_with_no_rooms_has_zero_rate()
    {
        var result = OccupancyCalculator.Calculate([], [], D(1), D(11));
        Assert.Equal(0m, result.Rate);
    }

    [Fact]
    public void Full_occupancy_is_100_percent()
    {
        var result = OccupancyCalculator.Calculate([new StayInterval(1, D(1), D(30))], [1], D(1), D(11));
        Assert.Equal(1m, result.Rate);
    }

    [Fact]
    public void OccupiedNights_excludes_check_out_day()
    {
        var nights = OccupancyCalculator.OccupiedNights(D(28), new DateTime(2026, 5, 2), D(1), new DateTime(2026, 5, 1)).ToList();
        Assert.Equal([D(28), D(29), D(30)], nights);
    }
}
