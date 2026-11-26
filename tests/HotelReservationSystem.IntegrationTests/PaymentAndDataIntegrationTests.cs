using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class PaymentIntegrationTests(DatabaseFixture db)
{
    private int NewReservation(decimal price = 1000m, int nights = 2, int offset = 70)
    {
        var room = db.NewRoom(price);
        return db.Reservations.Create(db.Staff, new NewReservation(db.NewGuest(), room.RoomId,
            DateTime.Today.AddDays(offset), DateTime.Today.AddDays(offset + nights)));
    }

    private ReservationBalance Balance(int id) =>
        db.Payments.ListBalances(DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1)).Single(b => b.ReservationId == id);

    [Fact]
    public void Partial_payments_reduce_the_balance()
    {
        var id = NewReservation(price: 1500m, nights: 2); // 3000
        db.Payments.AddPayment(db.Staff, id, 1000m, PaymentMethod.Cash);
        db.Payments.AddPayment(db.Staff, id, 500.25m, PaymentMethod.Card);

        var b = Balance(id);
        Assert.Equal(3000m, b.TotalPrice);
        Assert.Equal(1500.25m, b.PaidAmount);
        Assert.Equal(1499.75m, b.Balance);
        Assert.All(db.Payments.ListPayments(id), p => Assert.Equal("TRY", p.Currency));
    }

    [Fact]
    public void Overpayment_and_payment_on_fully_paid_reservation_are_rejected()
    {
        var id = NewReservation(price: 1000m, nights: 1);
        Assert.Throws<BusinessRuleException>(() => db.Payments.AddPayment(db.Staff, id, 1000.01m, PaymentMethod.Cash));
        db.Payments.AddPayment(db.Staff, id, 1000m, PaymentMethod.Cash);
        Assert.Throws<BusinessRuleException>(() => db.Payments.AddPayment(db.Staff, id, 1m, PaymentMethod.Cash));
        Assert.Equal(0m, Balance(id).Balance);
    }

    [Fact]
    public void Payment_on_cancelled_reservation_is_rejected()
    {
        var id = NewReservation();
        db.Reservations.Cancel(db.Staff, id);
        Assert.Throws<BusinessRuleException>(() => db.Payments.AddPayment(db.Staff, id, 10m, PaymentMethod.Cash));
    }

    [Fact]
    public async Task Simultaneous_payments_can_never_exceed_the_total()
    {
        var id = NewReservation(price: 1000m, nights: 1); // total 1000
        using var start = new ManualResetEventSlim(false);
        // 10 × 300 = 3000 requested; at most 3 fit (900), a 4th would exceed 1000.
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            start.Wait();
            try { db.Payments.AddPayment(db.Staff, id, 300m, PaymentMethod.Card); return true; }
            catch (BusinessRuleException) { return false; }
        })).ToList();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, results.Count(ok => ok));
        Assert.Equal(900m, Balance(id).PaidAmount);
    }

    [Fact]
    public void Only_admins_can_void_payments()
    {
        var id = NewReservation();
        var paymentId = db.Payments.AddPayment(db.Staff, id, 100m, PaymentMethod.Cash);
        Assert.Throws<AuthorizationException>(() => db.Payments.VoidPayment(db.Staff, paymentId));
        db.Payments.VoidPayment(db.Admin, paymentId);
        Assert.Empty(db.Payments.ListPayments(id));
    }

    [Fact]
    public void Dashboard_reports_income_per_currency_without_mixing()
    {
        var id = NewReservation(price: 2000m, nights: 1, offset: 1);
        db.Payments.AddPayment(db.Staff, id, 2000m, PaymentMethod.Card);
        // A legacy foreign-currency payment inserted directly must be reported separately.
        db.Scalar<int>("INSERT INTO dbo.Payments (ReservationId, Amount, Currency, Method) VALUES (@r, 50, 'EUR', N'Cash'); SELECT 1;", ("@r", id));

        var d = db.Reports.GetDashboard(DateTime.Today, DateTime.Today);
        Assert.Contains(d.IncomeByCurrency, c => c.Currency == "EUR" && c.Amount >= 50m);
        Assert.Contains(d.IncomeByCurrency, c => c.Currency == "TRY" && c.Amount >= 2000m);
        Assert.Equal(d.IncomeByCurrency.Count, d.IncomeByCurrency.Select(c => c.Currency).Distinct().Count());
        // and the reservation balance only counts payments in its own currency
        Assert.Equal(2000m, Balance(id).PaidAmount);
    }
}

[Collection(DatabaseCollection.Name)]
public class GuestRoomAndUserIntegrationTests(DatabaseFixture db)
{
    [Fact]
    public void Guest_round_trip_with_turkish_characters()
    {
        var id = db.Guests.Save(db.Staff, new Guest { FirstName = "Şükrü", LastName = "Öztürkoğlu", Phone = "+90 555 000 0000", Email = "sukru@example.com" });
        var g = db.Guests.Search("Öztürkoğlu").Single(x => x.GuestId == id);
        Assert.Equal("Şükrü", g.FirstName);

        // Update: change last name and clear the optional fields.
        db.Guests.Save(db.Staff, new Guest { GuestId = id, FirstName = "Şükrü", LastName = "Öztürk" });
        var updated = db.Guests.Search("Şükrü").Single(x => x.GuestId == id);
        Assert.Equal("Öztürk", updated.LastName);
        Assert.Null(updated.Email);
        Assert.Null(updated.Phone);
        Assert.DoesNotContain(db.Guests.Search("sukru@example"), x => x.GuestId == id);
    }

    [Fact]
    public void Guest_search_treats_wildcards_literally()
    {
        db.Guests.Save(db.Staff, new Guest { FirstName = "Percent", LastName = "Guest" });
        Assert.Empty(db.Guests.Search("%%%_"));
    }

    [Fact]
    public void Invalid_guest_is_rejected_by_the_service() =>
        Assert.Throws<BusinessRuleException>(() => db.Guests.Save(db.Staff, new Guest { FirstName = "", LastName = "X", Email = "bad" }));

    [Fact]
    public void Duplicate_national_id_is_rejected()
    {
        db.NewGuest("DUPL12345");
        Assert.Throws<BusinessRuleException>(() => db.NewGuest("DUPL12345"));
    }

    [Fact]
    public void Guest_with_reservations_cannot_be_deleted_but_others_can()
    {
        var withHistory = db.NewGuest();
        db.Reservations.Create(db.Staff, new NewReservation(withHistory, db.NewRoom().RoomId, DateTime.Today.AddDays(90), DateTime.Today.AddDays(91)));
        Assert.Throws<BusinessRuleException>(() => db.Guests.Delete(db.Staff, withHistory));

        var plain = db.NewGuest();
        db.Guests.Delete(db.Staff, plain);
        Assert.Null(db.Guests.Search(null).SingleOrDefault(g => g.GuestId == plain));
    }

    [Fact]
    public void Duplicate_room_number_is_rejected_and_used_rooms_are_deactivated_not_deleted()
    {
        var room = db.NewRoom();
        Assert.Throws<BusinessRuleException>(() =>
            db.Rooms.SaveRoom(db.Admin, new Room { RoomNumber = room.RoomNumber, RoomTypeId = room.RoomTypeId, IsActive = true }));

        db.Reservations.Create(db.Staff, new NewReservation(db.NewGuest(), room.RoomId, DateTime.Today.AddDays(100), DateTime.Today.AddDays(101)));
        Assert.False(db.Rooms.DeleteOrDeactivate(db.Admin, room.RoomId));
        Assert.False(db.Rooms.ListRooms().Single(r => r.RoomId == room.RoomId).IsActive);

        var unused = db.NewRoom();
        Assert.True(db.Rooms.DeleteOrDeactivate(db.Admin, unused.RoomId));
    }

    [Fact]
    public void Login_succeeds_with_correct_password_and_fails_generically_otherwise()
    {
        db.CreateUser("login_ok", UserRole.Staff);
        var ok = db.Auth.Login("login_ok", DatabaseFixture.Password);
        Assert.True(ok.Succeeded);
        Assert.Equal(UserRole.Staff, ok.User!.Role);

        var wrong = db.Auth.Login("login_ok", "wrong-password1");
        var unknown = db.Auth.Login("no_such_user", "wrong-password1");
        Assert.False(wrong.Succeeded);
        Assert.Equal(wrong.Error, unknown.Error); // does not reveal whether the user exists
    }

    [Fact]
    public void Login_rehashes_weak_hashes_on_success()
    {
        db.CreateUser("rehash_me", UserRole.Staff); // fixture hashes with 1,000 iterations
        Assert.True(db.Auth.Login("rehash_me", DatabaseFixture.Password).Succeeded);
        var stored = db.Scalar<string>("SELECT PasswordHash FROM dbo.Users WHERE Username = N'rehash_me'");
        Assert.Contains($"${Core.Security.PasswordHasher.DefaultIterations}$", stored);
    }

    [Fact]
    public void Account_locks_after_repeated_failures()
    {
        db.CreateUser("lock_me", UserRole.Staff);
        for (var i = 0; i < Core.Security.LoginPolicy.MaxFailedAttempts; i++)
            Assert.False(db.Auth.Login("lock_me", "wrong-password1").Succeeded);

        var locked = db.Auth.Login("lock_me", DatabaseFixture.Password); // correct password, but locked
        Assert.False(locked.Succeeded);
        Assert.Contains("Try again", locked.Error);
    }

    [Fact]
    public void Disabled_account_cannot_sign_in()
    {
        var u = db.CreateUser("disabled_user", UserRole.Staff);
        db.Auth.UpdateUser(db.Admin, u.UserId, UserRole.Staff, isActive: false);
        Assert.False(db.Auth.Login("disabled_user", DatabaseFixture.Password).Succeeded);
    }

    [Fact]
    public void The_last_active_admin_cannot_be_removed_and_users_cannot_demote_themselves()
    {
        Assert.Throws<BusinessRuleException>(() => db.Auth.UpdateUser(db.Admin, db.Admin.UserId, UserRole.Staff, true));
        Assert.Throws<AuthorizationException>(() => db.Auth.CreateUser(db.Staff, "nope_user", "Password123", UserRole.Admin));
    }

    [Fact]
    public void Initial_admin_setup_is_refused_once_users_exist() =>
        Assert.Throws<BusinessRuleException>(() => db.Auth.CreateInitialAdmin("second_admin", "Password123"));

    [Fact]
    public void Passwords_are_never_stored_in_plaintext()
    {
        var plaintextMatches = db.Scalar<int>("SELECT COUNT(*) FROM dbo.Users WHERE PasswordHash = @p OR PasswordHash NOT LIKE N'PBKDF2-SHA256$%'",
            ("@p", DatabaseFixture.Password));
        Assert.Equal(0, plaintextMatches);
    }
}

/// <summary>Verifies the published setup scripts (schema + fictional seed) produce a working database.</summary>
public class SetupScriptTests
{
    [Fact]
    public void Schema_and_seed_scripts_create_a_usable_demo_database()
    {
        const string name = "HotelReservationDB_SeedTests";
        DatabaseFixture.RecreateDatabase(name, includeSeed: true);
        var factory = new Core.Data.SqlConnectionFactory(DatabaseFixture.ConnectionStringFor(name));

        var auth = new Core.Services.AuthService(factory);
        var admin = auth.Login("admin", "Admin123!");
        Assert.True(admin.Succeeded);
        Assert.Equal(UserRole.Admin, admin.User!.Role);
        Assert.True(admin.MustChangePassword); // demo credentials must be replaced at first sign-in
        Assert.True(auth.Login("staff", "Staff123!").Succeeded);

        var reservations = new Core.Services.ReservationService(factory);
        reservations.RefreshStatuses();
        var all = reservations.List();
        Assert.Equal(38, all.Count);
        Assert.Equal(16, new Core.Services.RoomService(factory).ListRooms().Count);
        Assert.Contains(all, r => r.Status == ReservationStatus.CheckedIn);
        Assert.Contains(all, r => r.Status == ReservationStatus.Booked);
        Assert.Contains(all, r => r.Status == ReservationStatus.Cancelled);

        using var con = factory.Open();

        // Stored totals equal nights × the room type's nightly price.
        using var priceCheck = new Microsoft.Data.SqlClient.SqlCommand("""
            SELECT COUNT(*) FROM dbo.Reservations r
            JOIN dbo.Rooms rm ON rm.RoomId = r.RoomId JOIN dbo.RoomTypes rt ON rt.RoomTypeId = rm.RoomTypeId
            WHERE r.TotalPrice <> DATEDIFF(day, r.CheckInDate, r.CheckOutDate) * rt.BasePrice;
            """, con);
        Assert.Equal(0, (int)priceCheck.ExecuteScalar()!);

        // Seed data contains no overlapping active reservations.
        using var cmd = new Microsoft.Data.SqlClient.SqlCommand("""
            SELECT COUNT(*) FROM dbo.Reservations a JOIN dbo.Reservations b
              ON a.RoomId = b.RoomId AND a.ReservationId < b.ReservationId
             AND a.CheckInDate < b.CheckOutDate AND a.CheckOutDate > b.CheckInDate
            WHERE a.Status <> N'Cancelled' AND b.Status <> N'Cancelled';
            """, con);
        Assert.Equal(0, (int)cmd.ExecuteScalar()!);

        // No payment exceeds its reservation total.
        var balances = new Core.Services.PaymentService(factory).ListBalances(DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1));
        Assert.All(balances, b => Assert.True(b.PaidAmount <= b.TotalPrice));
    }
}

/// <summary>Forced password change for demo, new and reset accounts.</summary>
[Collection(DatabaseCollection.Name)]
public class PasswordChangeIntegrationTests(DatabaseFixture db)
{
    [Fact]
    public void Users_created_by_an_admin_must_change_their_password_and_can_then_sign_in_normally()
    {
        var id = db.Auth.CreateUser(db.Admin, "new_hire", "Temporary123", UserRole.Staff);
        var first = db.Auth.Login("new_hire", "Temporary123");
        Assert.True(first.Succeeded);
        Assert.True(first.MustChangePassword);

        Assert.Throws<BusinessRuleException>(() => db.Auth.ChangeOwnPassword(first.User!, "Temporary123", "Temporary123")); // same password
        Assert.Throws<BusinessRuleException>(() => db.Auth.ChangeOwnPassword(first.User!, "wrong-current1", "MyOwnPass456"));
        db.Auth.ChangeOwnPassword(first.User!, "Temporary123", "MyOwnPass456");

        Assert.False(db.Auth.Login("new_hire", "Temporary123").Succeeded);
        var second = db.Auth.Login("new_hire", "MyOwnPass456");
        Assert.True(second.Succeeded);
        Assert.False(second.MustChangePassword);

        db.Auth.ResetPassword(db.Admin, id, "ResetByAdmin1");
        Assert.True(db.Auth.Login("new_hire", "ResetByAdmin1").MustChangePassword);
    }
}
