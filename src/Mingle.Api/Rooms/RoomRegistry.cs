using System.Collections.Concurrent;

namespace Mingle.Api.Rooms;

/// <summary>
/// All open rooms, kept in memory. Fine for one server instance; with several instances the rooms
/// would move to a shared store (e.g. Redis), as the design doc notes.
/// </summary>
public sealed class RoomRegistry(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Room> rooms = new(StringComparer.Ordinal);

    public int Count => rooms.Count;

    public Room Create()
    {
        while (true)
        {
            var room = new Room(RoomCodes.New(), clock);
            if (rooms.TryAdd(room.Code, room)) return room;
        }
    }

    public Room? Find(string? code) =>
        rooms.TryGetValue(RoomCodes.Normalize(code), out var room) ? room : null;

    /// <summary>Removes rooms nobody is connected to and nobody has touched for <paramref name="idleFor"/>.</summary>
    public int RemoveIdle(TimeSpan idleFor)
    {
        var cutoff = clock.GetUtcNow() - idleFor;
        var removed = 0;
        foreach (var (code, room) in rooms)
        {
            if (!room.AnyoneConnected && room.LastActivity < cutoff && rooms.TryRemove(code, out _)) removed++;
        }
        return removed;
    }
}
