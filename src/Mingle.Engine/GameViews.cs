using System.Collections.Immutable;

namespace Mingle.Engine;

public sealed record AnswerView(
    string PlayerId,
    string PlayerName,
    bool IsTurnPlayer,
    string? Text,
    string? ChoiceText,
    string? VotedPlayerName,
    int? Scale);

/// <summary>
/// What one screen is allowed to see. Other players' answers never appear before the reveal:
/// this is a privacy rule, not a display choice.
/// </summary>
public sealed record GameView(
    GamePhase Phase,
    int CardNumber,
    int CardCount,
    string? KindId,
    int Depth,
    string? Prompt,
    ImmutableArray<string> Options,
    string? TurnPlayerId,
    string? TurnPlayerName,
    InputMode Input,
    ImmutableArray<string> AnswererIds,
    int AnsweredCount,
    bool MustAnswer,
    Answer? MyAnswer,
    ImmutableArray<AnswerView> RevealedAnswers,
    bool? MyReaction);

public static class GameViews
{
    /// <summary>The view for one player's private screen, in that player's language.</summary>
    public static GameView ForPlayer(GameState state, string playerId)
    {
        var player = state.Player(playerId);
        return Build(state, player.Lang, player.Id);
    }

    /// <summary>The view for a shared screen everyone can see (pass-and-play), with no private answers.</summary>
    public static GameView ForTable(GameState state, Lang lang) => Build(state, lang, viewerId: null);

    /// <summary>How many cards this game will actually deal: the setting, or fewer when fewer cards match.</summary>
    public static int CardTotal(GameState state) => Math.Min(state.Settings.CardCount, state.Pool.Length);

    private static GameView Build(GameState state, Lang lang, string? viewerId)
    {
        var round = state.Current;
        if (round is null)
        {
            return new GameView(state.Phase, state.History.Count, CardTotal(state), null, 0, null, [],
                null, null, InputMode.Spoken, [], 0, false, null, [], null);
        }

        var text = round.Card.TextFor(lang, state.Player(round.TurnPlayerId).Name);
        var revealed = state.Phase == GamePhase.Discussing
            ? round.Answerers
                .Where(round.Answers.ContainsKey)
                .Select(id => Describe(state, round, id, round.Answers[id], text))
                .OrderByDescending(a => a.IsTurnPlayer && round.Card.Kind.Audience == Audience.AboutTurnPlayer)
                .ToImmutableArray()
            : [];

        Answer? mine = null;
        bool? reaction = null;
        if (viewerId is not null)
        {
            mine = round.Answers.GetValueOrDefault(viewerId);
            reaction = round.Reactions.TryGetValue(viewerId, out var liked) ? liked : null;
        }

        return new GameView(
            state.Phase,
            round.Number,
            CardTotal(state),
            round.Card.KindId,
            round.Card.Depth,
            text.Text,
            text.Options,
            round.TurnPlayerId,
            state.Player(round.TurnPlayerId).Name,
            round.Input,
            round.Answerers,
            round.Answers.Count,
            viewerId is not null && state.Phase == GamePhase.Answering && round.Answerers.Contains(viewerId),
            mine,
            revealed,
            reaction);
    }

    private static AnswerView Describe(GameState state, Round round, string playerId, Answer answer, CardText text) =>
        new(
            playerId,
            state.Player(playerId).Name,
            playerId == round.TurnPlayerId,
            answer.Text,
            answer.Choice is { } choice && choice < text.Options.Length ? text.Options[choice] : null,
            answer.VotedPlayerId is { } voted ? state.Player(voted).Name : null,
            answer.Scale);
}
