using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>«Ես երբեք…» on one phone: open answers, the phone holder taps who has done it, the app picks a storyteller.</summary>
public sealed class NeverSession
{
    private ImmutableArray<Card> pool = [];
    private NeverSettings settings = new();

    public ImmutableArray<Player> Players { get; private set; } = [];
    public NeverGame? Game { get; private set; }

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, NeverSettings choice)
    {
        Players = players;
        pool = [.. decks.Where(d => d.Style == DeckStyle.Never).SelectMany(d => d.Cards)];
        settings = choice;
        Restart();
    }

    public void Restart() => Game = new NeverGame(Players, pool, settings, Random.Shared.Next(), secret: false);

    public Player PlayerById(string id) => Players.First(p => p.Id == id);
}
