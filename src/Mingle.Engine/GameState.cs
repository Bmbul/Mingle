using System.Collections.Immutable;

namespace Mingle.Engine;

public sealed record Player(string Id, string Name, Lang Lang);

public enum AnswerMode
{
    /// <summary>Players type or tap their answers privately; answers are revealed together.</summary>
    Written,
    /// <summary>Free answers are spoken out loud; choices, votes and scales are still one tap.</summary>
    Talk,
    /// <summary>Everything is answered out loud: nothing to type or tap. Made for one shared phone.</summary>
    OutLoud,
}

public enum TurnStyle
{
    /// <summary>Everyone answers free-response and scale cards.</summary>
    Everyone,
    /// <summary>Only the turn player answers free-response and scale cards.</summary>
    TakeTurns,
}

/// <summary>The order cards are dealt in.</summary>
public enum CardOrder
{
    /// <summary>Starts with light cards and goes deeper as the game goes on.</summary>
    LightToDeep,
    /// <summary>Any card can come at any time.</summary>
    Shuffled,
}

public sealed record GameSettings
{
    public ImmutableHashSet<string> KindIds { get; init; } = CardKinds.Regular.Select(k => k.Id).ToImmutableHashSet();
    public int MinDepth { get; init; } = 1;
    public int MaxDepth { get; init; } = 3;
    public AnswerMode AnswerMode { get; init; } = AnswerMode.Written;
    public TurnStyle TurnStyle { get; init; } = TurnStyle.Everyone;
    public int CardCount { get; init; } = 20;
    public CardOrder Order { get; init; } = CardOrder.LightToDeep;
}

public enum GamePhase
{
    /// <summary>Waiting for the required players to answer.</summary>
    Answering,
    /// <summary>Answers (if any) are revealed and the group talks; anyone can deal the next card.</summary>
    Discussing,
    Ended,
}

/// <summary>One player's answer. Exactly one field is set, matching the card's input mode.</summary>
public sealed record Answer
{
    public string? Text { get; init; }
    public int? Choice { get; init; }
    public string? VotedPlayerId { get; init; }
    public int? Scale { get; init; }

    public static Answer FromText(string text) => new() { Text = text };
    public static Answer FromChoice(int index) => new() { Choice = index };
    public static Answer ForPlayer(string playerId) => new() { VotedPlayerId = playerId };
    public static Answer FromScale(int value) => new() { Scale = value };
}

public sealed record Round(
    int Number,
    Card Card,
    string TurnPlayerId,
    InputMode Input,
    ImmutableArray<string> Answerers,
    ImmutableDictionary<string, Answer> Answers,
    ImmutableDictionary<string, bool> Reactions)
{
    public bool AllAnswered => Answerers.All(Answers.ContainsKey);
}

/// <summary>What is kept after a card is finished. Answers are deliberately not kept.</summary>
public sealed record RoundResult(Card Card, string TurnPlayerId, bool Passed, ImmutableDictionary<string, bool> Reactions)
{
    public int Likes => Reactions.Values.Count(liked => liked);
    public int Dislikes => Reactions.Values.Count(liked => !liked);
}

public sealed record GameState(
    GamePhase Phase,
    ImmutableArray<Player> Players,
    GameSettings Settings,
    ImmutableArray<Card> Pool,
    int Seed,
    int TurnIndex,
    Round? Current,
    ImmutableList<RoundResult> History)
{
    public Player Player(string id) =>
        Players.FirstOrDefault(p => p.Id == id) ?? throw new GameRuleException($"Unknown player '{id}'.");

    public Player? TurnPlayer => Current is null ? null : Player(Current.TurnPlayerId);
}

/// <summary>Thrown when an action breaks the rules of the game. The state is left unchanged.</summary>
public sealed class GameRuleException(string message) : Exception(message);
