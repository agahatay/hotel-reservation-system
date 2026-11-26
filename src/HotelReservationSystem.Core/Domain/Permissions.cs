namespace HotelReservationSystem.Core.Domain;

public enum Permission
{
    ViewDashboard,
    ManageGuests,
    ManageReservations,
    TakePayments,
    VoidPayments,
    ManageRooms,
    ManageUsers
}

/// <summary>
/// Role → permission mapping. Admin: everything. Staff: day-to-day front desk work.
/// </summary>
public static class RolePermissions
{
    private static readonly HashSet<Permission> StaffPermissions =
    [
        Permission.ViewDashboard,
        Permission.ManageGuests,
        Permission.ManageReservations,
        Permission.TakePayments
    ];

    public static bool Has(UserRole role, Permission permission) =>
        role == UserRole.Admin || (role == UserRole.Staff && StaffPermissions.Contains(permission));

    public static void Require(AuthenticatedUser? user, Permission permission)
    {
        if (user is null)
            throw new AuthorizationException("You must be signed in.");
        if (!Has(user.Role, permission))
            throw new AuthorizationException("You do not have permission to perform this action.");
    }
}
