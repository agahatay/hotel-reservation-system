using System.Data;
using HotelReservationSystem.Core.Data;

namespace HotelReservationSystem.Core.Services;

public sealed class PaymentService(SqlConnectionFactory db)
{
    /// <param name="from">First day of the period.</param>
    /// <param name="toInclusive">Last day of the period (inclusive).</param>
    public List<ReservationBalance> ListBalances(DateTime from, DateTime toInclusive, bool outstandingOnly = false, string? search = null)
    {
        if (toInclusive.Date < from.Date) throw new BusinessRuleException("The end date must not be before the start date.");
        using var con = db.Open();
        return new PaymentRepository(con).ListBalances(from.Date, toInclusive.Date.AddDays(1), outstandingOnly, search);
    }

    public List<Payment> ListPayments(int reservationId)
    {
        using var con = db.Open();
        return new PaymentRepository(con).ListForReservation(reservationId);
    }

    /// <summary>
    /// Records a payment in the reservation's currency. The reservation row is locked for the
    /// duration of the check so two simultaneous payments cannot together exceed the total.
    /// </summary>
    public int AddPayment(AuthenticatedUser actor, int reservationId, decimal amount, PaymentMethod method)
    {
        RolePermissions.Require(actor, Permission.TakePayments);

        return Transactions.Run(db, IsolationLevel.Serializable, (con, tx) =>
        {
            var res = new ReservationRepository(con, tx).GetById(reservationId, forUpdate: true)
                      ?? throw new BusinessRuleException("Reservation not found.");
            var payments = new PaymentRepository(con, tx);
            var paid = payments.SumPaid(reservationId, res.Currency);

            if (PaymentRules.Validate(amount, res.Currency, res.Status, res.Currency, res.TotalPrice, paid) is { } error)
                throw new BusinessRuleException(error);

            return payments.Insert(reservationId, amount, res.Currency, method);
        });
    }

    /// <summary>Removes a payment recorded by mistake (administrators only).</summary>
    public void VoidPayment(AuthenticatedUser actor, int paymentId)
    {
        RolePermissions.Require(actor, Permission.VoidPayments);
        using var con = db.Open();
        if (!new PaymentRepository(con).Delete(paymentId))
            throw new BusinessRuleException("The payment no longer exists.");
    }
}
