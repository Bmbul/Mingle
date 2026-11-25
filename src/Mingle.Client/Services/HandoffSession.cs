using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// A "Give it to…" game on one phone: who plays, the cards, the options (guess why, coin flip), who holds the phone
/// and every pass so far. Cards do not repeat until every card has been played.
/// </summary>
public sealed class HandoffSession
{
    private readonly Random random = new();
    private readonly HashSet<string> seen = [];

    public ImmutableArray<Player> Players { get; private set; } = [];
    public ImmutableArray<Card> Pool { get; private set; } = [];
    public bool Guess { get; private set; }
    public bool Coin { get; private set; }
    public int Rounds { get; private set; }
    public List<Handoff> Passes { get; } = [];
    public Player? Holder { get; private set; }

    public bool Ready => !Players.IsEmpty && !Pool.IsEmpty;
    public bool Finished => Ready && Passes.Count >= Rounds;

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, bool guess, bool coin, int rounds)
    {
        Players = players;
        Pool = Handoffs.Pool(decks, [.. players.Select(p => p.Lang).Distinct()]);
        Guess = guess;
        Coin = coin;
        Rounds = rounds;
        Restart();
    }

    /// <summary>Same players, cards and options; a fresh trail.</summary>
    public void Restart()
    {
        Passes.Clear();
        seen.Clear();
        Holder = Players.IsEmpty ? null : Players[0];
    }

    public Card? NextCard() => Blitz.Next(Pool, seen, random);

    public ImmutableArray<Card> Choices(Card answer) => Handoffs.GuessChoices(answer, Pool, random);

    /// <summary>Heads: the reason comes out; tails: it stays secret until the end.</summary>
    public bool FlipCoin() => random.Next(2) == 0;

    /// <summary>The phone went from the holder to <paramref name="to"/>, who now holds it.</summary>
    public void Record(Player to, Card card, bool? guessedRight, bool revealed)
    {
        Passes.Add(new Handoff(Holder!.Id, to.Id, card.Id, guessedRight, revealed));
        Holder = to;
    }

    public Player PlayerById(string id) => Players.First(p => p.Id == id);

    public Card CardById(string id) => Pool.First(c => c.Id == id);

    public ImmutableArray<(Player Player, ImmutableArray<Handoff> Received, int RightGuesses)> Trail() =>
        Handoffs.Trail(Players, Passes);
}
