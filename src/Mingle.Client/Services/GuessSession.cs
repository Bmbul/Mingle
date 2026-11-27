using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// "Guess the answer" on one phone: everyone says their guess out loud, the player answers, then taps who got it.
/// </summary>
public sealed class GuessSession
{
    private ImmutableArray<Card> pool = [];
    private GuessSettings settings = new();

    public ImmutableArray<Player> Players { get; private set; } = [];
    public GuessGame? Game { get; private set; }

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, int rounds)
    {
        Players = players;
        pool = [.. decks.Where(d => d.Style == DeckStyle.Guess).SelectMany(d => d.Cards)];
        settings = new GuessSettings(rounds);
        Restart();
    }

    /// <summary>Same players and decks, new cards and scores from zero.</summary>
    public void Restart() => Game = new GuessGame(Players, pool, settings, Random.Shared.Next(), written: false);

    public Player PlayerById(string id) => Players.First(p => p.Id == id);
}
