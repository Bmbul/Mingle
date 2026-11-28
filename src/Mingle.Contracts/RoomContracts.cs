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

/// <summary>Writing: the "Our topics" cards are being written; it comes between the lobby and play.</summary>
public enum RoomStage { Lobby, Playing, Ended, Writing }

/// <param name="DeckId">The deck this player brings to an "Everyone's decks" game, if chosen yet.</param>
/// <param name="PickedTopics">Whether this player has picked topics; never which ones.</param>
public sealed record RoomPlayer(string Id, string Name, Lang Lang, bool IsHost, bool Connected, string? DeckId = null, bool PickedTopics = false);

/// <summary>Where a room's cards come from.</summary>
public enum DeckSource
{
    /// <summary>The host picks the decks when creating the room.</summary>
    Chosen,
    /// <summary>Every player brings one deck; a few random cards from each are shuffled together.</summary>
    Everyone,
    /// <summary>Every player picks topics; AI writes the cards (library cards if it cannot).</summary>
    Topics,
}

public sealed record TopicOption(string Code, string Name);

/// <summary>Your own topic offer and picks in an "Our topics" lobby. Other players' picks are never sent.</summary>
public sealed record TopicChoice(ImmutableArray<TopicOption> Offer, ImmutableArray<string> Picked, int MaxPicks);

/// <summary>The game settings chosen in the lobby, plus which decks to play.</summary>
/// <param name="Depths">"Our topics" only: the depths to write (1 light, 2 personal, 3 deep); none or all three mixes them.</param>
/// <param name="Facts">Set when the room plays a fact game ("One truth", "Whose fact?") instead of questions; its prompts
/// come from the chosen decks.</param>
/// <param name="Guess">Set when the room plays "Guess the answer", with cards from the chosen decks.</param>
public sealed record RoomSettings(GameSettings Game, ImmutableArray<string> DeckIds, DeckSource Source = DeckSource.Chosen,
    ImmutableArray<int> Depths = default, FactSettings? Facts = null, GuessSettings? Guess = null, OddSettings? Odd = null,
    MatchSettings? Match = null, NeverSettings? Never = null, MirrorSettings? Mirror = null, AuctionSettings? Auction = null)
{
    // Missing from older clients: an empty list, since a default ImmutableArray cannot be serialized.
    public ImmutableArray<int> Depths { get; init; } = Depths.IsDefault ? [] : Depths;
}

public sealed record LikedCard(string KindId, int Depth, string Text);

/// <param name="Cards">In an "Our topics" game, every written card as a deck file (named after the topics,
/// in the viewer's language), so each player can save them as their own deck.</param>
/// <param name="LikedIds">Ids of the cards anyone liked, to save only those.</param>
public sealed record GameSummary(int Played, int Passed, ImmutableArray<LikedCard> Liked,
    string? Cards = null, ImmutableArray<string> LikedIds = default)
{
    public ImmutableArray<string> LikedIds { get; init; } = LikedIds.IsDefault ? [] : LikedIds;
}

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
    GameSummary? Summary,
    TopicChoice? Topics = null,
    bool LibraryCards = false,
    FactView? Facts = null,
    GuessView? Guess = null,
    OddView? Odd = null,
    MatchView? Match = null,
    NeverView? Never = null,
    MirrorView? Mirror = null,
    AuctionView? Auction = null)
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
    /// <summary>Sets your topic picks (up to three, from your offer) in an "Our topics" lobby.</summary>
    Task PickTopics(ImmutableArray<string> codes);
    Task StartGame();
    Task SubmitAnswer(Answer answer);
    Task Pass();
    Task Next();
    Task React(bool? liked);
    Task EndGame();
    Task Leave();

    // Fact games.
    /// <summary>Your facts; in "One truth" three, with <paramref name="marked"/> the true one (or the lie).</summary>
    Task WriteFacts(ImmutableArray<string> facts, int marked);
    /// <summary>"One truth": another prompt, before you have written.</summary>
    Task SwapPrompt();
    /// <summary>Host: start with whoever has written, without waiting for the rest.</summary>
    Task BeginFacts();
    /// <summary>A position (One truth) or a player id (Whose fact).</summary>
    Task VoteFact(string choice);
    /// <summary>Host: show the answer before everyone has voted.</summary>
    Task RevealFact();
    Task NextFact();

    // "Guess the answer".
    /// <summary>Your answer (your turn) or your guess.</summary>
    Task WriteGuess(string text);
    /// <summary>Host: show the answers before everyone has written.</summary>
    Task RevealGuesses();
    /// <summary>The turn player (or the host): marks a guess right or not.</summary>
    Task MarkGuess(string playerId, bool right);
    /// <summary>The turn player (or the host): on to the next card.</summary>
    Task NextGuess();

    // "Who got a different question?".
    Task AnswerOdd(string text);
    /// <summary>Host: on to the vote without waiting for a missing answer.</summary>
    Task StartOddVote();
    Task VoteOdd(string playerId);
    /// <summary>Host: show the answer before everyone has voted.</summary>
    Task RevealOdd();
    Task NextOdd();

    // "Same wavelength".
    Task AnswerMatch(string text);
    /// <summary>Host: show the answers before everyone has written.</summary>
    Task RevealMatch();
    /// <summary>Host: count one answer as the same as another.</summary>
    Task JoinMatch(string key, string intoKey);
    Task NextMatch();

    // «Ես երբեք…» / Never have I ever.
    Task AnswerNever(bool has);
    /// <summary>The other players you think have done it.</summary>
    Task GuessNever(ImmutableArray<string> playerIds);
    /// <summary>Host: on to the guessing without a missing answer, or show the names without a missing guess.</summary>
    Task MoveNeverOn();
    Task NextNever();

    // "How you see me".
    /// <summary>Where you put the player on the scale (or yourself, on your turn), 1 to 10.</summary>
    Task PlaceMirror(int value);
    /// <summary>Host: show the placements before everyone has placed.</summary>
    Task RevealMirror();
    Task NextMirror();

    // "Life auction".
    /// <summary>Your secret bid on this lot; 0 passes.</summary>
    Task BidAuction(int amount);
    /// <summary>Host: the hammer falls before everyone has bid (the rest pass).</summary>
    Task RevealAuction();
    Task NextAuction();
}

/// <summary>Messages the server sends to a player.</summary>
public interface IGameClient
{
    Task RoomUpdated(RoomView view);
}
