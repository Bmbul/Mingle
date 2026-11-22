using Mingle.Engine;

namespace Mingle.API.Rooms;

/// <summary>A player's seat in a room. A seat survives disconnects; the token reclaims it.</summary>
public sealed class RoomMember(string id, string name, Lang lang, string token)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public Lang Lang { get; } = lang;
    public string Token { get; } = token;

    /// <summary>Open connections (a player may have the room open in more than one tab).</summary>
    public HashSet<string> Connections { get; } = [];

    public bool Connected => Connections.Count > 0;

    /// <summary>The deck this player brings when everyone brings a deck.</summary>
    public string? DeckId { get; set; }

    /// <summary>The topics this player is offered in an "Our topics" game, and the ones picked.</summary>
    public System.Collections.Immutable.ImmutableArray<string> TopicOffer { get; set; } = [];
    public System.Collections.Immutable.ImmutableArray<string> TopicPicks { get; set; } = [];

    public Player ToPlayer() => new(Id, Name, Lang);
}
