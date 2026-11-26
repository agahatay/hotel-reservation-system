using HotelReservationSystem.Core.Data;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Services;

public sealed class GuestService(SqlConnectionFactory db)
{
    public List<Guest> Search(string? text)
    {
        using var con = db.Open();
        return new GuestRepository(con).Search(text);
    }

    /// <summary>Inserts a new guest (GuestId == 0) or updates an existing one. Returns the guest id.</summary>
    public int Save(AuthenticatedUser actor, Guest input)
    {
        RolePermissions.Require(actor, Permission.ManageGuests);

        var guest = GuestValidator.Normalize(input);
        var errors = GuestValidator.Validate(guest);
        if (errors.Count > 0) throw new BusinessRuleException(string.Join(Environment.NewLine, errors));

        try
        {
            using var con = db.Open();
            var repo = new GuestRepository(con);
            if (guest.GuestId == 0) return repo.Insert(guest);
            if (!repo.Update(guest)) throw new BusinessRuleException("The guest no longer exists.");
            return guest.GuestId;
        }
        catch (SqlException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException("Another guest is already registered with this national ID / passport number.");
        }
    }

    public void Delete(AuthenticatedUser actor, int guestId)
    {
        RolePermissions.Require(actor, Permission.ManageGuests);
        using var con = db.Open();
        var repo = new GuestRepository(con);
        if (repo.HasReservations(guestId))
            throw new BusinessRuleException("This guest has reservations and cannot be deleted. Reservation history must be kept.");
        try
        {
            repo.Delete(guestId);
        }
        catch (SqlException ex) when (ex.Number == SqlErrors.ForeignKeyViolation)
        {
            throw new BusinessRuleException("This guest has reservations and cannot be deleted.");
        }
    }
}
