namespace HotelReservationSystem.Core.Domain;

/// <summary>
/// A rule violation whose message is safe and meaningful to show to the user
/// (e.g. "Room is not available for the selected dates").
/// </summary>
public class BusinessRuleException(string message) : Exception(message);

public sealed class AuthorizationException(string message) : BusinessRuleException(message);
