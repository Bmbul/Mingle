using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// "Who got a different question?" on one phone: the phone goes round so each player reads their question in secret,
/// everyone answers out loud, then the group accuses someone and the questions are revealed.
/// </summary>
public sealed class OddSession
{
    private ImmutableArray<Card> pool = [];
    private OddSettings settings = new();

    public ImmutableArray<Player> Players { get; private set; } = [];
    public OddGame? Game { get; private set; }
    /// <summary>How many players have read their question this round.</summary>
    public int Read { get; set; }

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, OddSettings choice)
    {
        Players = players;
        pool = [.. decks.Where(d => d.Style == DeckStyle.Odd).SelectMany(d => d.Cards)];
        settings = choice;
        Restart();
    }

    public void Restart()
    {
        Game = new OddGame(Players, pool, settings, Random.Shared.Next(), votes: false);
        Read = 0;
    }

    public Player PlayerById(string id) => Players.First(p => p.Id == id);
}
