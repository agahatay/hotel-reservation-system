namespace HotelReservationSystem.Core.Domain;

public static class PaymentRules
{
    /// <summary>Outstanding amount. Never negative (overpayment is rejected before it is stored).</summary>
    public static decimal Balance(decimal totalPrice, decimal paidAmount) =>
        Math.Max(0m, totalPrice - paidAmount);

    /// <summary>
    /// Validates a new payment against its reservation. Returns an error message or null.
    /// A reservation has exactly one currency; payments must use it, so balances are
    /// never computed across currencies.
    /// </summary>
    public static string? Validate(decimal amount, string currency,
                                   ReservationStatus status, string reservationCurrency,
                                   decimal totalPrice, decimal alreadyPaid)
    {
        if (status == ReservationStatus.Cancelled)
            return "Payments cannot be added to a cancelled reservation.";
        if (amount <= 0)
            return "Payment amount must be greater than zero.";
        if (decimal.Round(amount, 2) != amount)
            return "Payment amount cannot have more than 2 decimal places.";
        if (!Currencies.IsSupported(currency))
            return $"Unsupported currency '{currency}'.";
        if (!string.Equals(currency, reservationCurrency, StringComparison.Ordinal))
            return $"This reservation is priced in {reservationCurrency}. Payments must be made in {reservationCurrency}.";

        var balance = Balance(totalPrice, alreadyPaid);
        if (balance == 0)
            return "This reservation is already fully paid.";
        if (amount > balance)
            return $"Payment exceeds the outstanding balance ({balance:N2} {reservationCurrency}).";
        return null;
    }
}
