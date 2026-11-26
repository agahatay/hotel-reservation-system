using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ReservationIntegrationTests(DatabaseFixture db)
{
    private static DateTime Day(int offset) => DateTime.Today.AddDays(offset);

    private int Book(int roomId, int fromOffset, int toOffset, int? guestId = null) =>
        db.Reservations.Create(db.Staff, new NewReservation(guestId ?? db.NewGuest(), roomId, Day(fromOffset), Day(toOffset)));

    [Fact]
    public void Booking_stores_total_price_fixed_at_booking_time()
    {
        var room = db.NewRoom(price: 3800m);
        var id = Book(room.RoomId, 10, 13);

        var res = db.Reservations.List().Single(r => r.ReservationId == id);
        Assert.Equal(ReservationStatus.Booked, res.Status);
        Assert.Equal(11_400m, res.TotalPrice);
        Assert.Equal("TRY", res.Currency);

        // A later price change must not rewrite existing reservations.
        var type = db.Rooms.ListRoomTypes().Single(t => t.RoomTypeId == room.RoomTypeId);
        db.Rooms.SaveRoomType(db.Admin, new RoomType { RoomTypeId = type.RoomTypeId, TypeName = type.TypeName, Capacity = type.Capacity, BasePrice = 5000m });
        Assert.Equal(11_400m, db.Reservations.List().Single(r => r.ReservationId == id).TotalPrice);
    }

    [Fact]
    public void Same_room_overlapping_dates_is_rejected()
    {
        var room = db.NewRoom();
        Book(room.RoomId, 10, 15);

        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 12, 14)); // inside
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 8, 11));  // overlaps start
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 14, 20)); // overlaps end
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 5, 25));  // surrounds
    }

    [Fact]
    public void Same_room_back_to_back_and_non_overlapping_dates_are_allowed()
    {
        var room = db.NewRoom();
        Book(room.RoomId, 10, 15);
        Book(room.RoomId, 15, 17); // checks in the day the previous guest checks out
        Book(room.RoomId, 7, 10);  // checks out the day the first guest checks in
        Book(room.RoomId, 30, 31);
        Assert.Equal(4, db.Scalar<int>("SELECT COUNT(*) FROM dbo.Reservations WHERE RoomId = @r", ("@r", room.RoomId)));
    }

    [Fact]
    public void Different_rooms_same_dates_are_allowed()
    {
        var a = db.NewRoom();
        var b = db.NewRoom();
        var guest = db.NewGuest();
        Book(a.RoomId, 10, 12, guest);
        Book(b.RoomId, 10, 12, guest);
    }

    [Fact]
    public void Cancelled_reservation_releases_the_room()
    {
        var room = db.NewRoom();
        var first = Book(room.RoomId, 10, 15);
        Assert.DoesNotContain(db.Reservations.ListAvailableRooms(Day(10), Day(15)), r => r.RoomId == room.RoomId);

        db.Reservations.Cancel(db.Staff, first);

        Assert.Contains(db.Reservations.ListAvailableRooms(Day(10), Day(15)), r => r.RoomId == room.RoomId);
        Book(room.RoomId, 10, 15);
    }

    [Fact]
    public void Cancelling_twice_or_after_check_in_is_rejected()
    {
        var room = db.NewRoom();
        var id = Book(room.RoomId, 3, 5);
        db.Reservations.Cancel(db.Staff, id);
        Assert.Throws<BusinessRuleException>(() => db.Reservations.Cancel(db.Staff, id));

        var current = Book(db.NewRoom().RoomId, 0, 2);
        db.Reservations.RefreshStatuses();
        Assert.Equal(ReservationStatus.CheckedIn, db.Reservations.List().Single(r => r.ReservationId == current).Status);
        Assert.Throws<BusinessRuleException>(() => db.Reservations.Cancel(db.Staff, current));
    }

    [Fact]
    public void Inactive_room_cannot_be_booked_or_listed()
    {
        var room = db.NewRoom(active: false);
        Assert.DoesNotContain(db.Reservations.ListAvailableRooms(Day(1), Day(2)), r => r.RoomId == room.RoomId);
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 1, 2));
    }

    [Fact]
    public void Past_check_in_and_invalid_ranges_are_rejected()
    {
        var room = db.NewRoom();
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, -1, 2));
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 5, 5));
        Assert.Throws<BusinessRuleException>(() => Book(room.RoomId, 5, 4));
    }

    [Fact]
    public void Database_check_constraint_rejects_invalid_dates_even_without_the_application()
    {
        var room = db.NewRoom();
        var guest = db.NewGuest();
        var ex = Assert.ThrowsAny<Microsoft.Data.SqlClient.SqlException>(() => db.Scalar<int>(
            "INSERT INTO dbo.Reservations (GuestId, RoomId, CheckInDate, CheckOutDate, TotalPrice) VALUES (@g, @r, '2030-01-05', '2030-01-05', 0); SELECT 1;",
            ("@g", guest), ("@r", room.RoomId)));
        Assert.Equal(547, ex.Number); // CHECK constraint violation
    }

    [Fact]
    public async Task Simultaneous_bookings_of_the_same_room_and_dates_produce_exactly_one_reservation()
    {
        var room = db.NewRoom();
        var guests = Enumerable.Range(0, 12).Select(_ => db.NewGuest()).ToList();
        using var start = new ManualResetEventSlim(false);

        var attempts = guests.Select(g => Task.Run(() =>
        {
            start.Wait();
            try
            {
                db.Reservations.Create(db.Staff, new NewReservation(g, room.RoomId, Day(40), Day(43)));
                return true;
            }
            catch (BusinessRuleException)
            {
                return false;
            }
        })).ToList();

        start.Set();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal(1, db.Scalar<int>("SELECT COUNT(*) FROM dbo.Reservations WHERE RoomId = @r AND Status <> N'Cancelled'", ("@r", room.RoomId)));
    }

    [Fact]
    public async Task Simultaneous_partially_overlapping_bookings_never_create_an_overlap()
    {
        var room = db.NewRoom();
        // Ranges that all overlap their neighbours.
        var ranges = new[] { (50, 53), (52, 55), (54, 57), (51, 52), (56, 58), (50, 58), (53, 54), (57, 60) };
        var guest = db.NewGuest();
        using var start = new ManualResetEventSlim(false);

        var tasks = ranges.Select(r => Task.Run(() =>
        {
            start.Wait();
            try { db.Reservations.Create(db.Staff, new NewReservation(guest, room.RoomId, Day(r.Item1), Day(r.Item2))); }
            catch (BusinessRuleException) { }
        })).ToList();
        start.Set();
        await Task.WhenAll(tasks);

        var overlaps = db.Scalar<int>("""
            SELECT COUNT(*) FROM dbo.Reservations a
            JOIN dbo.Reservations b ON a.RoomId = b.RoomId AND a.ReservationId < b.ReservationId
             AND a.CheckInDate < b.CheckOutDate AND a.CheckOutDate > b.CheckInDate
            WHERE a.RoomId = @r AND a.Status <> N'Cancelled' AND b.Status <> N'Cancelled'
            """, ("@r", room.RoomId));
        Assert.Equal(0, overlaps);
        Assert.True(db.Scalar<int>("SELECT COUNT(*) FROM dbo.Reservations WHERE RoomId = @r", ("@r", room.RoomId)) >= 1);
    }

    [Fact]
    public void Status_refresh_moves_reservations_along_by_date()
    {
        var room = db.NewRoom();
        var guest = db.NewGuest();
        // Insert historic rows directly (the application refuses past check-ins).
        var past = db.Scalar<int>("""
            INSERT INTO dbo.Reservations (GuestId, RoomId, CheckInDate, CheckOutDate, TotalPrice)
            OUTPUT INSERTED.ReservationId VALUES (@g, @r, DATEADD(day, -60, CAST(GETDATE() AS date)), DATEADD(day, -58, CAST(GETDATE() AS date)), 100);
            """, ("@g", guest), ("@r", room.RoomId));
        var current = db.Scalar<int>("""
            INSERT INTO dbo.Reservations (GuestId, RoomId, CheckInDate, CheckOutDate, TotalPrice)
            OUTPUT INSERTED.ReservationId VALUES (@g, @r, DATEADD(day, -1, CAST(GETDATE() AS date)), DATEADD(day, 1, CAST(GETDATE() AS date)), 100);
            """, ("@g", guest), ("@r", room.RoomId));
        var future = Book(room.RoomId, 5, 6, guest);

        db.Reservations.RefreshStatuses();

        var list = db.Reservations.List().ToDictionary(r => r.ReservationId, r => r.Status);
        Assert.Equal(ReservationStatus.Completed, list[past]);
        Assert.Equal(ReservationStatus.CheckedIn, list[current]);
        Assert.Equal(ReservationStatus.Booked, list[future]);
    }

    [Fact]
    public void Staff_cannot_manage_rooms()
    {
        Assert.Throws<AuthorizationException>(() =>
            db.Rooms.SaveRoom(db.Staff, new Room { RoomNumber = "X1", RoomTypeId = 1, IsActive = true }));
    }
}
