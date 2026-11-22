using System.Collections.Immutable;

namespace Mingle.Engine.Tests;

internal static class TestData
{
    public static readonly Player Ani = new("ani", "Ani", Lang.Hy);
    public static readonly Player Arman = new("arman", "Arman", Lang.En);
    public static readonly Player Lilit = new("lilit", "Lilit", Lang.Ru);

    public static ImmutableArray<Player> Couple => [Ani, Arman];
    public static ImmutableArray<Player> Trio => [Ani, Arman, Lilit];

    public static Card Card(string id, CardKind kind, int depth = 1, params Lang[] langs)
    {
        if (langs.Length == 0) langs = [Lang.En, Lang.Hy, Lang.Ru];
        var options = kind.Response == ResponseType.Choice ? ImmutableArray.Create("A", "B") : [];
        return new Card(id, kind.Id, depth, [],
            langs.ToImmutableDictionary(l => l, l => new CardText($"{id} in {l.Code()}", options)));
    }

    public static Deck Deck(params Card[] cards) =>
        new("test", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Test"), [.. cards]);

    /// <summary>A deck with several cards of every kind at every depth.</summary>
    public static Deck FullDeck()
    {
        var cards = new List<Card>();
        foreach (var kind in CardKinds.All)
            for (var depth = 1; depth <= 3; depth++)
                for (var i = 0; i < 4; i++)
                    cards.Add(Card($"{kind.Id}-{depth}-{i}", kind, depth));
        return Deck([.. cards]);
    }

    public static GameSettings Only(CardKind kind, AnswerMode mode = AnswerMode.Written, TurnStyle turn = TurnStyle.Everyone) =>
        new() { KindIds = [kind.Id], AnswerMode = mode, TurnStyle = turn };

    public static GameState Start(GameSettings settings, ImmutableArray<Player>? players = null, Deck? deck = null, int seed = 42) =>
        Game.Start(players ?? Couple, settings, [deck ?? FullDeck()], seed);

    /// <summary>Submits a valid answer for every answerer who has not answered yet.</summary>
    public static GameState AnswerAll(GameState state)
    {
        var round = state.Current!;
        foreach (var id in round.Answerers.Where(id => !round.Answers.ContainsKey(id)))
            state = Game.Apply(state, new SubmitAnswer(id, ValidAnswer(round.Input, state)));
        return state;
    }

    public static Answer ValidAnswer(InputMode input, GameState state) => input switch
    {
        InputMode.Text => Answer.FromText("an answer"),
        InputMode.Choice => Answer.FromChoice(1),
        InputMode.PlayerVote => Answer.ForPlayer(state.Players[0].Id),
        InputMode.Scale => Answer.FromScale(7),
        _ => throw new InvalidOperationException("Spoken cards take no answers."),
    };
}
