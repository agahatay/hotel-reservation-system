using HotelReservationSystem.Core.Data;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Services;

public sealed class RoomService(SqlConnectionFactory db)
{
    public List<Room> ListRooms(bool activeOnly = false)
    {
        using var con = db.Open();
        return new RoomRepository(con).ListRooms(activeOnly);
    }

    public List<RoomType> ListRoomTypes()
    {
        using var con = db.Open();
        return new RoomRepository(con).ListRoomTypes();
    }

    public int SaveRoom(AuthenticatedUser actor, Room input)
    {
        RolePermissions.Require(actor, Permission.ManageRooms);

        var number = input.RoomNumber?.Trim() ?? "";
        if (number.Length == 0) throw new BusinessRuleException("Room number is required.");
        if (number.Length > 10) throw new BusinessRuleException("Room number cannot exceed 10 characters.");
        if (input.RoomTypeId <= 0) throw new BusinessRuleException("Select a room type.");
        if (input.Floor is < -5 or > 200) throw new BusinessRuleException("Floor must be between -5 and 200.");

        var room = new Room
        {
            RoomId = input.RoomId, RoomNumber = number, RoomTypeId = input.RoomTypeId,
            Floor = input.Floor, IsActive = input.IsActive
        };

        try
        {
            using var con = db.Open();
            var repo = new RoomRepository(con);
            if (repo.RoomNumberExists(number, room.RoomId == 0 ? null : room.RoomId))
                throw new BusinessRuleException($"Room number '{number}' already exists.");
            if (room.RoomId == 0) return repo.InsertRoom(room);
            if (!repo.UpdateRoom(room)) throw new BusinessRuleException("The room no longer exists.");
            return room.RoomId;
        }
        catch (SqlException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"Room number '{number}' already exists.");
        }
    }

    public void SetActive(AuthenticatedUser actor, int roomId, bool isActive)
    {
        RolePermissions.Require(actor, Permission.ManageRooms);
        using var con = db.Open();
        if (!new RoomRepository(con).SetRoomActive(roomId, isActive))
            throw new BusinessRuleException("The room no longer exists.");
    }

    /// <summary>
    /// Deletes a room that has never been booked. A room with reservation history is
    /// deactivated instead, so history stays intact. Returns true if the room was deleted.
    /// </summary>
    public bool DeleteOrDeactivate(AuthenticatedUser actor, int roomId)
    {
        RolePermissions.Require(actor, Permission.ManageRooms);
        using var con = db.Open();
        var repo = new RoomRepository(con);
        if (repo.RoomHasReservations(roomId))
        {
            repo.SetRoomActive(roomId, false);
            return false;
        }
        return repo.DeleteRoom(roomId);
    }

    public int SaveRoomType(AuthenticatedUser actor, RoomType input)
    {
        RolePermissions.Require(actor, Permission.ManageRooms);

        var name = input.TypeName?.Trim() ?? "";
        if (name.Length == 0) throw new BusinessRuleException("Room type name is required.");
        if (name.Length > 50) throw new BusinessRuleException("Room type name cannot exceed 50 characters.");
        if (input.Capacity is < 1 or > 20) throw new BusinessRuleException("Capacity must be between 1 and 20.");
        if (input.BasePrice <= 0) throw new BusinessRuleException("Base price must be greater than zero.");
        if (decimal.Round(input.BasePrice, 2) != input.BasePrice) throw new BusinessRuleException("Base price cannot have more than 2 decimal places.");

        var type = new RoomType { RoomTypeId = input.RoomTypeId, TypeName = name, Capacity = input.Capacity, BasePrice = input.BasePrice };
        try
        {
            using var con = db.Open();
            var repo = new RoomRepository(con);
            if (type.RoomTypeId == 0) return repo.InsertRoomType(type);
            if (!repo.UpdateRoomType(type)) throw new BusinessRuleException("The room type no longer exists.");
            return type.RoomTypeId;
        }
        catch (SqlException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"A room type named '{name}' already exists.");
        }
    }

    public void DeleteRoomType(AuthenticatedUser actor, int roomTypeId)
    {
        RolePermissions.Require(actor, Permission.ManageRooms);
        using var con = db.Open();
        var repo = new RoomRepository(con);
        if (repo.RoomTypeInUse(roomTypeId))
            throw new BusinessRuleException("This room type is assigned to rooms and cannot be deleted.");
        repo.DeleteRoomType(roomTypeId);
    }
}
