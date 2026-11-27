using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// The fact games on one phone: the phone goes round so everyone writes in secret, then the facts come up one by one
/// and the group talks it over before the reveal ("One truth": which fact is true; "Whose fact?": who wrote it).
/// Nobody votes and nobody scores.
/// </summary>
public sealed class FactSession
{
    private ImmutableArray<Card> prompts = [];

    public ImmutableArray<Player> Players { get; private set; } = [];
    public FactSettings Settings { get; private set; } = new();
    public bool FindTheLie => Settings.FindTheLie;
    public bool OneTruth => Settings.Kind == FactGameKind.OneTruth;
    public FactGame? Game { get; private set; }

    public bool Ready => Game is not null;

    public void Start(ImmutableArray<Player> players, IEnumerable<Deck> decks, FactSettings settings)
    {
        Players = players;
        Settings = settings;
        prompts = [.. decks.Where(d => d.Style == DeckStyle.Facts).SelectMany(d => d.Cards).Where(c => c.Kind.Style == DeckStyle.Facts)];
        Restart();
    }

    /// <summary>Same players and prompts; everyone writes new facts.</summary>
    public void Restart() =>
        Game = new FactGame(Players, Settings, prompts, Random.Shared.Next(), votes: false);

    /// <summary>Who writes next: the first player whose facts are not in yet.</summary>
    public Player? NextWriter => Game is { Stage: FactStage.Writing } game ? Players.FirstOrDefault(p => !game.HasWritten(p.Id)) : null;

    public Player PlayerById(string id) => Players.First(p => p.Id == id);
}
