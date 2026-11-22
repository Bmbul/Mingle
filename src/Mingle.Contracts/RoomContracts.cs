using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Mingle.Engine;

namespace Mingle.Contracts;

public static class HubRoutes
{
    public const string Game = "/hubs/game";
}

/// <summary>What a player gets back after creating or joining a room. Keep the token to rejoin later.</summary>
public sealed record JoinResult(string RoomCode, string PlayerId, string Token);

public enum RoomStage { Lobby, Playing, Ended }

/// <param name="DeckId">The deck this player brings to an "Everyone's decks" game, if chosen yet.</param>
public sealed record RoomPlayer(string Id, string Name, Lang Lang, bool IsHost, bool Connected, string? DeckId = null);

/// <summary>Where a room's cards come from.</summary>
public enum DeckSource
{
    /// <summary>The host picks the decks when creating the room.</summary>
    Chosen,
    /// <summary>Every player brings one deck; a few random cards from each are shuffled together.</summary>
    Everyone,
}

/// <summary>The game settings chosen in the lobby, plus which decks to play.</summary>
public sealed record RoomSettings(GameSettings Game, ImmutableArray<string> DeckIds, DeckSource Source = DeckSource.Chosen);

public sealed record LikedCard(string KindId, int Depth, string Text);

public sealed record GameSummary(int Played, int Passed, ImmutableArray<LikedCard> Liked);

/// <summary>
/// Everything one player's screen needs. Built per player on the server, so it never contains
/// other players' answers before the reveal.
/// </summary>
public sealed record RoomView(
    string Code,
    RoomStage Stage,
    string YouId,
    ImmutableArray<RoomPlayer> Players,
    RoomSettings Settings,
    GameView? Game,
    GameSummary? Summary)
{
    [JsonIgnore] public RoomPlayer? You => Players.FirstOrDefault(p => p.Id == YouId);
    [JsonIgnore] public bool YouAreHost => You?.IsHost == true;
}

/// <summary>Calls a player can make. The server knows who is calling, so no call takes a player id.</summary>
public interface IGameHub
{
    Task<JoinResult> CreateRoom(string name, Lang lang, RoomSettings settings);
    Task<JoinResult> JoinRoom(string code, string name, Lang lang);
    Task<bool> Rejoin(string code, string playerId, string token);
    Task UpdateSettings(RoomSettings settings);
    /// <summary>Picks the deck you bring to an "Everyone's decks" game; null takes it back.</summary>
    Task BringDeck(string? deckId);
    Task StartGame();
    Task SubmitAnswer(Answer answer);
    Task Pass();
    Task Next();
    Task React(bool? liked);
    Task EndGame();
    Task Leave();
}

/// <summary>Messages the server sends to a player.</summary>
public interface IGameClient
{
    Task RoomUpdated(RoomView view);
}
