using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.Tests;

public class PaymentRulesTests
{
    private const string Try = "TRY";

    private static string? Validate(decimal amount, string currency = Try, ReservationStatus status = ReservationStatus.Booked,
                                    decimal total = 1000m, decimal paid = 0m) =>
        PaymentRules.Validate(amount, currency, status, Try, total, paid);

    [Theory]
    [InlineData(1000, 0, 1000)]
    [InlineData(1000, 400, 600)]
    [InlineData(1000, 1000, 0)]
    [InlineData(1000, 1200, 0)] // never negative
    public void Balance_is_total_minus_paid(decimal total, decimal paid, decimal expected) =>
        Assert.Equal(expected, PaymentRules.Balance(total, paid));

    [Fact]
    public void Full_payment_is_accepted() => Assert.Null(Validate(1000m));

    [Fact]
    public void Partial_payment_is_accepted() => Assert.Null(Validate(250.50m, paid: 400m));

    [Fact]
    public void Exact_remaining_balance_is_accepted() => Assert.Null(Validate(600m, paid: 400m));

    [Fact]
    public void Overpayment_is_rejected() => Assert.Contains("exceeds", Validate(600.01m, paid: 400m));

    [Fact]
    public void Payment_on_fully_paid_reservation_is_rejected() => Assert.Contains("fully paid", Validate(1m, paid: 1000m));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_amounts_are_rejected(decimal amount) => Assert.Contains("greater than zero", Validate(amount));

    [Fact]
    public void More_than_two_decimals_is_rejected() => Assert.Contains("decimal", Validate(10.005m));

    [Fact]
    public void Payment_in_a_different_currency_is_rejected() => Assert.Contains("TRY", Validate(100m, currency: "EUR"));

    [Fact]
    public void Unsupported_currency_is_rejected() => Assert.Contains("Unsupported", Validate(100m, currency: "XYZ"));

    [Fact]
    public void Payment_on_cancelled_reservation_is_rejected() =>
        Assert.Contains("cancelled", Validate(100m, status: ReservationStatus.Cancelled));

    [Fact]
    public void Decimal_arithmetic_has_no_rounding_drift()
    {
        // 0.1 + 0.2 must be exactly 0.3 — this fails with double.
        Assert.Equal(0m, PaymentRules.Balance(0.3m, 0.1m + 0.2m));
    }

    [Theory]
    [InlineData("TRY", true)]
    [InlineData("USD", true)]
    [InlineData("EUR", true)]
    [InlineData("GBP", true)]
    [InlineData("try", false)]
    [InlineData("JPY", false)]
    [InlineData(null, false)]
    public void Supported_currencies(string? code, bool expected) => Assert.Equal(expected, Currencies.IsSupported(code));
}

public class ReservationBalanceTests
{
    [Fact]
    public void Cancelled_reservation_has_no_outstanding_balance()
    {
        var b = new ReservationBalance { TotalPrice = 6000m, PaidAmount = 0m, Status = ReservationStatus.Cancelled };
        Assert.Equal(0m, b.Balance);
    }

    [Fact]
    public void Open_reservation_balance_is_total_minus_paid()
    {
        var b = new ReservationBalance { TotalPrice = 6000m, PaidAmount = 1200m, Status = ReservationStatus.Booked };
        Assert.Equal(4800m, b.Balance);
    }
}
