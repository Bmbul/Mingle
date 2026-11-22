namespace Mingle.Api.Rooms;

/// <summary>A request the room refused. The message is shown to the player.</summary>
public sealed class RoomException(string message) : Exception(message);
