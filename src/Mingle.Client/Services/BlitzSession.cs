using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// A blitz game on one phone: the players in turn order, the chosen decks, and each player's score.
/// Cards never repeat within a game until every card has been seen.
/// </summary>
public sealed class BlitzSession
{
    private readonly Random random = new();
    private readonly HashSet<string> seen = [];
    private Dictionary<Lang, ImmutableArray<Card>> pools = [];

    public ImmutableArray<Player> Players { get; private set; } = [];
    public ImmutableArray<Deck> Decks { get; private set; } = [];
    public Dictionary<string, (int Answered, int Missed)> Scores { get; } = [];
    public int TurnIndex { get; private set; }

    public bool Ready => !Players.IsEmpty;
    public bool Finished => Ready && TurnIndex >= Players.Length;
    public Player? Current => Ready && !Finished ? Players[TurnIndex] : null;

    public void Start(ImmutableArray<Player> players, ImmutableArray<Deck> decks)
    {
        Players = players;
        Decks = decks;
        pools = players.Select(p => p.Lang).Distinct().ToDictionary(l => l, l => Blitz.Pool(decks, l));
        Restart();
    }

    /// <summary>Same players and decks, scores back to zero.</summary>
    public void Restart()
    {
        Scores.Clear();
        seen.Clear();
        TurnIndex = 0;
    }

    /// <summary>The next card for the player in the hot seat, in their language.</summary>
    public Card? NextCard() => Current is { } player ? Blitz.Next(pools[player.Lang], seen, random) : null;

    /// <summary>A random letter for a "category with a letter" card, in the player's alphabet.</summary>
    public string LetterFor(Lang lang)
    {
        var letters = Blitz.Letters(lang);
        return letters[random.Next(letters.Length)];
    }

    public void EndTurn(int answered, int missed)
    {
        if (Current is { } player) Scores[player.Id] = (answered, missed);
        TurnIndex++;
    }

    public ImmutableArray<(Player Player, int Answered)> Ranking() =>
        Blitz.Ranking(Players.Select(p => (p, Scores.TryGetValue(p.Id, out var s) ? s.Answered : 0)));
}
