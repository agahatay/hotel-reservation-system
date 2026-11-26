using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.Tests;

public class ReservationRulesTests
{
    private static DateTime D(int day) => new(2026, 3, day);

    [Theory]
    // existing 10→15
    [InlineData(10, 15, 12, 14, true)]   // inside
    [InlineData(10, 15, 8, 11, true)]    // overlaps start
    [InlineData(10, 15, 14, 18, true)]   // overlaps end
    [InlineData(10, 15, 8, 20, true)]    // surrounds
    [InlineData(10, 15, 10, 15, true)]   // identical
    [InlineData(10, 15, 15, 17, false)]  // new check-in on existing check-out day (back-to-back)
    [InlineData(10, 15, 7, 10, false)]   // new check-out on existing check-in day (back-to-back)
    [InlineData(10, 15, 1, 5, false)]    // before
    [InlineData(10, 15, 20, 25, false)]  // after
    public void Overlaps_detects_conflicting_stays(int exIn, int exOut, int newIn, int newOut, bool expected) =>
        Assert.Equal(expected, ReservationRules.Overlaps(D(exIn), D(exOut), D(newIn), D(newOut)));

    [Fact]
    public void Overlaps_ignores_time_of_day() =>
        Assert.False(ReservationRules.Overlaps(D(10), D(15).AddHours(11), D(15).AddHours(14), D(17)));

    [Theory]
    [InlineData(ReservationStatus.Booked, true)]
    [InlineData(ReservationStatus.CheckedIn, true)]
    [InlineData(ReservationStatus.Completed, true)]
    [InlineData(ReservationStatus.Cancelled, false)]
    public void Only_cancelled_reservations_release_the_room(ReservationStatus status, bool blocks) =>
        Assert.Equal(blocks, ReservationRules.BlocksRoom(status));

    [Fact]
    public void ValidateNewStay_accepts_valid_future_stay() =>
        Assert.Null(ReservationRules.ValidateNewStay(D(10), D(12), today: D(1)));

    [Fact]
    public void ValidateNewStay_accepts_check_in_today() =>
        Assert.Null(ReservationRules.ValidateNewStay(D(10), D(11), today: D(10)));

    [Fact]
    public void ValidateNewStay_rejects_past_check_in() =>
        Assert.Contains("past", ReservationRules.ValidateNewStay(D(9), D(12), today: D(10)));

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 9)]
    public void ValidateNewStay_rejects_check_out_not_after_check_in(int checkIn, int checkOut) =>
        Assert.Contains("after", ReservationRules.ValidateNewStay(D(checkIn), D(checkOut), today: D(1)));

    [Fact]
    public void ValidateNewStay_rejects_stays_longer_than_maximum()
    {
        var checkIn = D(1);
        Assert.Null(ReservationRules.ValidateNewStay(checkIn, checkIn.AddDays(ReservationRules.MaxNights), checkIn));
        Assert.NotNull(ReservationRules.ValidateNewStay(checkIn, checkIn.AddDays(ReservationRules.MaxNights + 1), checkIn));
    }

    [Theory]
    [InlineData(10, 11, 1)]
    [InlineData(10, 15, 5)]
    [InlineData(28, 33, 5)] // across month end (March 28 → April 2)
    public void Nights_counts_hotel_nights(int checkIn, int checkOutDay, int expected)
    {
        var checkOut = new DateTime(2026, 3, 1).AddDays(checkOutDay - 1);
        Assert.Equal(expected, ReservationRules.Nights(D(checkIn), checkOut));
    }

    [Fact]
    public void CalculateTotal_multiplies_nights_by_rate_in_decimal()
    {
        Assert.Equal(11_400.00m, ReservationRules.CalculateTotal(D(10), D(13), 3_800.00m));
        Assert.Equal(0.30m, ReservationRules.CalculateTotal(D(10), D(13), 0.10m)); // no binary floating point drift
    }

    [Fact]
    public void CalculateTotal_rejects_invalid_input()
    {
        Assert.Throws<ArgumentException>(() => ReservationRules.CalculateTotal(D(10), D(10), 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReservationRules.CalculateTotal(D(10), D(11), 0m));
    }

    [Theory]
    [InlineData(ReservationStatus.Booked, true)]
    [InlineData(ReservationStatus.CheckedIn, false)]
    [InlineData(ReservationStatus.Completed, false)]
    [InlineData(ReservationStatus.Cancelled, false)]
    public void CanCancel_only_booked(ReservationStatus status, bool expected) =>
        Assert.Equal(expected, ReservationRules.CanCancel(status));

    [Theory]
    // status, check-in, check-out, today, expected
    [InlineData(ReservationStatus.Booked, 10, 15, 9, ReservationStatus.Booked)]
    [InlineData(ReservationStatus.Booked, 10, 15, 10, ReservationStatus.CheckedIn)]
    [InlineData(ReservationStatus.Booked, 10, 15, 14, ReservationStatus.CheckedIn)]
    [InlineData(ReservationStatus.Booked, 10, 15, 15, ReservationStatus.Completed)]
    [InlineData(ReservationStatus.CheckedIn, 10, 15, 16, ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Cancelled, 10, 15, 12, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed, 10, 15, 12, ReservationStatus.Completed)]
    public void StatusForDate_progresses_by_date(ReservationStatus current, int checkIn, int checkOut, int today, ReservationStatus expected) =>
        Assert.Equal(expected, ReservationRules.StatusForDate(current, D(checkIn), D(checkOut), D(today)));
}
