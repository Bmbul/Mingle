using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// "One truth" on one phone: the phone goes round so everyone writes their three facts in secret, then each player's
/// facts are read out and the group talks it over before the reveal. Nobody votes and nobody scores.
/// </summary>
public sealed class FactSession
{
    private ImmutableArray<Card> prompts = [];

    public ImmutableArray<Player> Players { get; private set; } = [];
    public bool FindTheLie { get; private set; }
    public FactGame? Game { get; private set; }

    public bool Ready => Game is not null;

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, bool findTheLie)
    {
        Players = players;
        FindTheLie = findTheLie;
        prompts = [.. decks.Where(d => d.Style == DeckStyle.Facts).SelectMany(d => d.Cards).Where(c => c.Kind.Style == DeckStyle.Facts)];
        Restart();
    }

    /// <summary>Same players and prompts; everyone writes new facts.</summary>
    public void Restart() =>
        Game = new FactGame(Players, new FactSettings(FactGameKind.OneTruth, FindTheLie), prompts, Random.Shared.Next(), votes: false);

    /// <summary>Who writes next: the first player whose facts are not in yet.</summary>
    public Player? NextWriter => Game is { Stage: FactStage.Writing } game ? Players.FirstOrDefault(p => !game.HasWritten(p.Id)) : null;

    public Player PlayerById(string id) => Players.First(p => p.Id == id);
}
