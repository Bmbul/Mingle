using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>
/// The game rules as a pure reducer: <c>(GameState, GameAction) → GameState</c>.
/// No I/O, so pass-and-play, online rooms and a local host can all run the same code.
/// </summary>
public static class Game
{
    public const int MinPlayers = 2;
    public const int MaxTextLength = 500;

    public static GameState Start(
        IEnumerable<Player> players,
        GameSettings settings,
        IEnumerable<Deck> decks,
        int seed,
        ICardSelector? selector = null)
    {
        var playerList = players.ToImmutableArray();
        if (playerList.Length < MinPlayers)
            throw new GameRuleException($"At least {MinPlayers} players are needed.");
        if (playerList.Select(p => p.Id).Distinct().Count() != playerList.Length)
            throw new GameRuleException("Player ids must be unique.");
        if (playerList.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            throw new GameRuleException("Every player needs a name.");
        if (settings.CardCount < 1)
            throw new GameRuleException("A game needs at least one card.");

        var pool = BuildPool(decks, settings, playerList);
        if (pool.IsEmpty)
            throw new GameRuleException("No cards match these settings.");

        var state = new GameState(
            GamePhase.Answering, playerList, settings, pool, seed,
            TurnIndex: 0, Current: null, History: []);
        return Deal(state, selector ?? CardSelector.Instance);
    }

    /// <summary>The cards a game with these settings and players can use.</summary>
    public static ImmutableArray<Card> BuildPool(IEnumerable<Deck> decks, GameSettings settings, IReadOnlyCollection<Player> players)
    {
        var languages = players.Select(p => p.Lang).Distinct().ToList();
        return decks
            .SelectMany(d => d.Cards)
            .DistinctBy(c => c.Id)
            .Where(c => settings.KindIds.Contains(c.KindId))
            .Where(c => c.Depth >= settings.MinDepth && c.Depth <= settings.MaxDepth)
            .Where(c => languages.All(c.Supports))
            .ToImmutableArray();
    }

    public static GameState Apply(GameState state, GameAction action, ICardSelector? selector = null)
    {
        selector ??= CardSelector.Instance;
        _ = state.Player(action.PlayerId); // unknown players are rejected

        if (state.Phase == GamePhase.Ended)
            throw new GameRuleException("The game has ended.");
        var round = state.Current ?? throw new GameRuleException("No card is in play.");

        return action switch
        {
            SubmitAnswer submit => Submit(state, round, submit),
            PassCard => FinishRound(state, round, passed: true, selector),
            NextCard when state.Phase != GamePhase.Discussing =>
                throw new GameRuleException("Wait until everyone has answered, or pass the card."),
            NextCard => FinishRound(state, round, passed: false, selector),
            ReactToCard react => state with
            {
                Current = round with
                {
                    Reactions = react.Liked is { } liked
                        ? round.Reactions.SetItem(react.PlayerId, liked)
                        : round.Reactions.Remove(react.PlayerId),
                },
            },
            EndGame => End(state, round),
            _ => throw new GameRuleException($"Unknown action {action.GetType().Name}."),
        };
    }

    public static InputMode EffectiveInput(CardKind kind, AnswerMode mode) => mode == AnswerMode.OutLoud ? InputMode.Spoken : kind.Response switch
    {
        ResponseType.Free => mode == AnswerMode.Written ? InputMode.Text : InputMode.Spoken,
        ResponseType.Choice => InputMode.Choice,
        ResponseType.PlayerVote => InputMode.PlayerVote,
        ResponseType.Scale => InputMode.Scale,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ImmutableArray<string> Answerers(
        CardKind kind, InputMode input, TurnStyle turnStyle, ImmutableArray<Player> players, Player turnPlayer)
    {
        if (input == InputMode.Spoken) return [];
        var everyone = players.Select(p => p.Id).ToImmutableArray();
        return kind.Audience switch
        {
            Audience.Everyone or Audience.AboutTurnPlayer => everyone,
            Audience.FollowTurnStyle => turnStyle == TurnStyle.Everyone ? everyone : [turnPlayer.Id],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static GameState Submit(GameState state, Round round, SubmitAnswer submit)
    {
        if (state.Phase != GamePhase.Answering)
            throw new GameRuleException("Answers are already revealed.");
        if (!round.Answerers.Contains(submit.PlayerId))
            throw new GameRuleException("This player is not answering this card.");

        var answer = Validate(state, round, submit.Answer);
        var updated = round with { Answers = round.Answers.SetItem(submit.PlayerId, answer) };
        return state with
        {
            Current = updated,
            Phase = updated.AllAnswered ? GamePhase.Discussing : GamePhase.Answering,
        };
    }

    private static Answer Validate(GameState state, Round round, Answer answer)
    {
        switch (round.Input)
        {
            case InputMode.Text:
                var text = answer.Text?.Trim();
                if (string.IsNullOrEmpty(text)) throw new GameRuleException("Write an answer first.");
                if (text.Length > MaxTextLength) throw new GameRuleException($"Answers are limited to {MaxTextLength} characters.");
                return Answer.FromText(text);
            case InputMode.Choice:
                var options = round.Card.TextFor(Lang.En).Options.Length;
                if (answer.Choice is not { } choice || choice < 0 || choice >= options)
                    throw new GameRuleException("Pick one of the options.");
                return Answer.FromChoice(choice);
            case InputMode.PlayerVote:
                if (answer.VotedPlayerId is not { } voted || state.Players.All(p => p.Id != voted))
                    throw new GameRuleException("Vote for one of the players.");
                return Answer.ForPlayer(voted);
            case InputMode.Scale:
                if (answer.Scale is not { } scale || scale is < 1 or > 10)
                    throw new GameRuleException("Pick a number from 1 to 10.");
                return Answer.FromScale(scale);
            default:
                throw new GameRuleException("This card is answered out loud.");
        }
    }

    private static GameState FinishRound(GameState state, Round round, bool passed, ICardSelector selector)
    {
        var finished = state with
        {
            History = state.History.Add(new RoundResult(round.Card, round.TurnPlayerId, passed, round.Reactions)),
            TurnIndex = (state.TurnIndex + 1) % state.Players.Length,
            Current = null,
        };
        return Deal(finished, selector);
    }

    private static GameState End(GameState state, Round round)
    {
        // A card still waiting for answers was not really played, so it is not recorded.
        var history = state.Phase == GamePhase.Discussing
            ? state.History.Add(new RoundResult(round.Card, round.TurnPlayerId, false, round.Reactions))
            : state.History;
        return state with { Phase = GamePhase.Ended, Current = null, History = history };
    }

    /// <summary>
    /// The card that comes after the current one, whether it is played or passed: the selector is deterministic (seed,
    /// card number, cards played), so this is exactly the card <see cref="Deal"/> will pick. Shown under the top card.
    /// </summary>
    public static Card? Upcoming(GameState state, ICardSelector? selector = null)
    {
        if (state.Current is null || state.Phase == GamePhase.Ended) return null;
        var number = state.History.Count + 2;
        if (number > state.Settings.CardCount) return null;
        var played = state.History.Select(h => h.Card.Id).Append(state.Current.Card.Id).ToHashSet();
        return (selector ?? CardSelector.Instance).Pick(new SelectionContext(state.Pool, played, number, state.Settings, state.Seed));
    }

    private static GameState Deal(GameState state, ICardSelector selector)
    {
        var number = state.History.Count + 1;
        if (number > state.Settings.CardCount)
            return state with { Phase = GamePhase.Ended, Current = null };

        var played = state.History.Select(h => h.Card.Id).ToHashSet();
        var card = selector.Pick(new SelectionContext(state.Pool, played, number, state.Settings, state.Seed));
        if (card is null)
            return state with { Phase = GamePhase.Ended, Current = null };

        var turnPlayer = state.Players[state.TurnIndex];
        var input = EffectiveInput(card.Kind, state.Settings.AnswerMode);
        var answerers = Answerers(card.Kind, input, state.Settings.TurnStyle, state.Players, turnPlayer);
        var round = new Round(number, card, turnPlayer.Id, input, answerers,
            ImmutableDictionary<string, Answer>.Empty, ImmutableDictionary<string, bool>.Empty);

        return state with
        {
            Current = round,
            Phase = answerers.IsEmpty ? GamePhase.Discussing : GamePhase.Answering,
        };
    }
}
