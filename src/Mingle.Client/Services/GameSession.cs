using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>The setup the player chose last, so "New game" starts from it.</summary>
/// <param name="PassThePhone">
/// True: each player answers privately and hands the phone on (slower).
/// False: everyone answers on one shared screen (faster, not private).
/// </param>
public sealed record SetupChoices(
    ImmutableArray<Player> Players,
    GameSettings Settings,
    ImmutableHashSet<string> DeckIds,
    bool PassThePhone = false);

/// <summary>Holds the current pass-and-play game on this device and applies actions to it.</summary>
public sealed class GameSession
{
    public GameState? State { get; private set; }
    public SetupChoices? LastSetup { get; private set; }

    public bool InProgress => State is { Phase: not GamePhase.Ended };

    public bool PassThePhone => LastSetup?.PassThePhone ?? false;

    public event Action? Changed;

    public void Start(SetupChoices setup, IEnumerable<Deck> decks)
    {
        var chosen = decks.Where(d => setup.DeckIds.Contains(d.Id));
        State = Game.Start(setup.Players, setup.Settings, chosen, Random.Shared.Next());
        LastSetup = setup;
        Changed?.Invoke();
    }

    public void Dispatch(GameAction action)
    {
        if (State is null) throw new InvalidOperationException("No game is running.");
        State = Game.Apply(State, action);
        Changed?.Invoke();
    }

    public void Clear()
    {
        State = null;
        Changed?.Invoke();
    }
}
