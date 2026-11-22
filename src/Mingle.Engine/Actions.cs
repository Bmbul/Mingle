namespace Mingle.Engine;

public abstract record GameAction(string PlayerId);

public sealed record SubmitAnswer(string PlayerId, Answer Answer) : GameAction(PlayerId);

/// <summary>Skip the current card. Anyone can pass, at any point before the next card.</summary>
public sealed record PassCard(string PlayerId) : GameAction(PlayerId);

/// <summary>Deal the next card. Anyone can press Next while the group is discussing.</summary>
public sealed record NextCard(string PlayerId) : GameAction(PlayerId);

/// <summary>Like (true), dislike (false) or clear (null) the current card.</summary>
public sealed record ReactToCard(string PlayerId, bool? Liked) : GameAction(PlayerId);

public sealed record EndGame(string PlayerId) : GameAction(PlayerId);
