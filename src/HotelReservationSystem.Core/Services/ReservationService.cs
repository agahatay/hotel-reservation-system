using System.Data;
using HotelReservationSystem.Core.Data;

namespace HotelReservationSystem.Core.Services;

public sealed class ReservationService(SqlConnectionFactory db, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private DateTime Today => _clock.GetLocalNow().Date;

    /// <summary>Moves reservations along Booked → CheckedIn → Completed according to today's date.</summary>
    public void RefreshStatuses()
    {
        using var con = db.Open();
        new ReservationRepository(con).RefreshStatuses(Today);
    }

    public List<ReservationListItem> List(ReservationStatus? status = null, string? search = null)
    {
        using var con = db.Open();
        return new ReservationRepository(con).List(status, search);
    }

    public List<Room> ListAvailableRooms(DateTime checkIn, DateTime checkOut)
    {
        if (ReservationRules.ValidateNewStay(checkIn, checkOut, Today) is { } error)
            throw new BusinessRuleException(error);
        using var con = db.Open();
        return new RoomRepository(con).ListAvailableRooms(checkIn, checkOut);
    }

    /// <summary>
    /// Creates a reservation atomically. The room row is locked (UPDLOCK, HOLDLOCK) inside a
    /// SERIALIZABLE transaction, so two concurrent bookings of the same room are processed one
    /// after the other and the second one sees the first one's reservation.
    /// </summary>
    public int Create(AuthenticatedUser actor, NewReservation request)
    {
        RolePermissions.Require(actor, Permission.ManageReservations);

        var r = request with { CheckInDate = request.CheckInDate.Date, CheckOutDate = request.CheckOutDate.Date };
        if (ReservationRules.ValidateNewStay(r.CheckInDate, r.CheckOutDate, Today) is { } error)
            throw new BusinessRuleException(error);

        return Transactions.Run(db, IsolationLevel.Serializable, (con, tx) =>
        {
            if (new GuestRepository(con, tx).GetById(r.GuestId) is null)
                throw new BusinessRuleException("The selected guest no longer exists.");

            var rooms = new RoomRepository(con, tx);
            var room = rooms.GetRoomForBooking(r.RoomId)
                       ?? throw new BusinessRuleException("The selected room no longer exists.");
            if (!room.IsActive)
                throw new BusinessRuleException($"Room {room.RoomNumber} is not active and cannot be booked.");

            var reservations = new ReservationRepository(con, tx);
            if (reservations.CountOverlapping(r.RoomId, r.CheckInDate, r.CheckOutDate) > 0)
                throw new BusinessRuleException($"Room {room.RoomNumber} is already booked for some of the selected nights.");

            var total = ReservationRules.CalculateTotal(r.CheckInDate, r.CheckOutDate, room.BasePrice);
            return reservations.Insert(r, total, Currencies.Default);
        });
    }

    public void Cancel(AuthenticatedUser actor, int reservationId)
    {
        RolePermissions.Require(actor, Permission.ManageReservations);
        using var con = db.Open();
        var repo = new ReservationRepository(con);
        var res = repo.GetById(reservationId) ?? throw new BusinessRuleException("Reservation not found.");

        if (!ReservationRules.CanCancel(res.Status))
            throw new BusinessRuleException(res.Status switch
            {
                ReservationStatus.Cancelled => "This reservation is already cancelled.",
                ReservationStatus.CheckedIn => "The guest has already checked in; this reservation can no longer be cancelled.",
                ReservationStatus.Completed => "Completed reservations cannot be cancelled.",
                _ => "This reservation cannot be cancelled."
            });

        if (!repo.Cancel(reservationId))
            throw new BusinessRuleException("The reservation was changed by someone else. Refresh and try again.");
    }
}
