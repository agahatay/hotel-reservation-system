using HotelReservationSystem.Core.Data;

namespace HotelReservationSystem.Common;

/// <summary>Composition root: one instance of each service for the application's lifetime.</summary>
internal sealed class AppServices
{
    public AppServices(string connectionString)
    {
        Database = new SqlConnectionFactory(connectionString);
        Auth = new AuthService(Database);
        Guests = new GuestService(Database);
        Rooms = new RoomService(Database);
        Reservations = new ReservationService(Database);
        Payments = new PaymentService(Database);
        Reports = new ReportService(Database);
    }

    public SqlConnectionFactory Database { get; }
    public AuthService Auth { get; }
    public GuestService Guests { get; }
    public RoomService Rooms { get; }
    public ReservationService Reservations { get; }
    public PaymentService Payments { get; }
    public ReportService Reports { get; }
}
