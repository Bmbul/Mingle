using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class GameTests
{
    [Fact]
    public void Start_needs_two_players()
    {
        var ex = Assert.Throws<GameRuleException>(() => Game.Start([Ani], new GameSettings(), [FullDeck()], 1));
        Assert.Contains("2 players", ex.Message);
    }

    [Fact]
    public void Start_fails_when_no_card_matches()
    {
        var settings = new GameSettings { KindIds = ["rate"], MinDepth = 3, MaxDepth = 3 };
        var deck = Deck(Card("only-light", CardKinds.RateIt, depth: 1));
        Assert.Throws<GameRuleException>(() => Game.Start(Couple, settings, [deck], 1));
    }

    [Fact]
    public void Pool_keeps_only_selected_kinds_depths_and_languages_every_player_reads()
    {
        var deck = Deck(
            Card("open-ok", CardKinds.OpenQuestion, 2),
            Card("wrong-kind", CardKinds.RateIt, 2),
            Card("too-deep", CardKinds.OpenQuestion, 3),
            Card("no-armenian", CardKinds.OpenQuestion, 2, Lang.En, Lang.Ru));
        var settings = new GameSettings { KindIds = ["open"], MinDepth = 1, MaxDepth = 2 };

        var pool = Game.BuildPool([deck], settings, Couple);

        Assert.Equal(["open-ok"], pool.Select(c => c.Id));
    }

    [Fact]
    public void Written_mode_waits_for_everyone_then_reveals()
    {
        var state = Start(Only(CardKinds.FinishSentence));
        Assert.Equal(GamePhase.Answering, state.Phase);
        Assert.Equal(InputMode.Text, state.Current!.Input);
        Assert.Equal(["ani", "arman"], state.Current.Answerers.ToArray());

        state = Game.Apply(state, new SubmitAnswer("ani", Answer.FromText("  when you call  ")));
        Assert.Equal(GamePhase.Answering, state.Phase);
        Assert.Equal("when you call", state.Current!.Answers["ani"].Text);

        state = Game.Apply(state, new SubmitAnswer("arman", Answer.FromText("voice notes")));
        Assert.Equal(GamePhase.Discussing, state.Phase);
    }

    [Fact]
    public void A_player_can_change_their_answer_before_the_reveal()
    {
        var state = Start(Only(CardKinds.OpenQuestion), Trio);
        state = Game.Apply(state, new SubmitAnswer("ani", Answer.FromText("first")));
        state = Game.Apply(state, new SubmitAnswer("ani", Answer.FromText("second")));

        Assert.Equal("second", state.Current!.Answers["ani"].Text);
        Assert.Equal(GamePhase.Answering, state.Phase);
    }

    [Fact]
    public void Talk_mode_free_cards_go_straight_to_discussion()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk));

        Assert.Equal(GamePhase.Discussing, state.Phase);
        Assert.Equal(InputMode.Spoken, state.Current!.Input);
        Assert.Empty(state.Current.Answerers);
    }

    [Theory]
    [InlineData("wyr", InputMode.Choice)]
    [InlineData("likely", InputMode.PlayerVote)]
    [InlineData("rate", InputMode.Scale)]
    public void Talk_mode_keeps_one_tap_answers(string kindId, InputMode expected)
    {
        var state = Start(Only(CardKinds.Get(kindId), AnswerMode.Talk));

        Assert.Equal(GamePhase.Answering, state.Phase);
        Assert.Equal(expected, state.Current!.Input);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("wyr")]
    [InlineData("likely")]
    [InlineData("guess")]
    [InlineData("rate")]
    public void Out_loud_mode_never_asks_for_input(string kindId)
    {
        var state = Start(Only(CardKinds.Get(kindId), AnswerMode.OutLoud));

        Assert.Equal(GamePhase.Discussing, state.Phase);
        Assert.Equal(InputMode.Spoken, state.Current!.Input);
        Assert.Empty(state.Current.Answerers);
    }

    [Fact]
    public void Take_turns_asks_only_the_turn_player_and_rotates()
    {
        var state = Start(Only(CardKinds.OpenQuestion, turn: TurnStyle.TakeTurns), Trio);
        var turns = new List<string>();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal([state.Current!.TurnPlayerId], state.Current.Answerers.ToArray());
            turns.Add(state.Current.TurnPlayerId);
            state = AnswerAll(state);
            state = Game.Apply(state, new NextCard("lilit"));
        }

        Assert.Equal(["ani", "arman", "lilit", "ani"], turns);
    }

    [Fact]
    public void Votes_always_go_to_everyone_even_when_taking_turns()
    {
        var state = Start(Only(CardKinds.MostLikelyTo, turn: TurnStyle.TakeTurns), Trio);
        Assert.Equal(["ani", "arman", "lilit"], state.Current!.Answerers.ToArray());
    }

    [Fact]
    public void Guess_my_answer_asks_the_subject_and_everyone_else()
    {
        var state = Start(Only(CardKinds.GuessMyAnswer, turn: TurnStyle.TakeTurns), Trio);
        Assert.Equal(3, state.Current!.Answerers.Length);
    }

    [Fact]
    public void Only_answerers_can_submit()
    {
        var state = Start(Only(CardKinds.OpenQuestion, turn: TurnStyle.TakeTurns));
        var other = state.Players.First(p => p.Id != state.Current!.TurnPlayerId).Id;

        Assert.Throws<GameRuleException>(() => Game.Apply(state, new SubmitAnswer(other, Answer.FromText("hi"))));
    }

    [Fact]
    public void Answers_must_match_the_input()
    {
        var text = Start(Only(CardKinds.OpenQuestion));
        Assert.Throws<GameRuleException>(() => Game.Apply(text, new SubmitAnswer("ani", Answer.FromText("   "))));
        Assert.Throws<GameRuleException>(() => Game.Apply(text, new SubmitAnswer("ani", Answer.FromText(new string('x', 501)))));
        Assert.Throws<GameRuleException>(() => Game.Apply(text, new SubmitAnswer("ani", Answer.FromScale(5))));

        var choice = Start(Only(CardKinds.WouldYouRather));
        Assert.Throws<GameRuleException>(() => Game.Apply(choice, new SubmitAnswer("ani", Answer.FromChoice(2))));

        var vote = Start(Only(CardKinds.MostLikelyTo));
        Assert.Throws<GameRuleException>(() => Game.Apply(vote, new SubmitAnswer("ani", Answer.ForPlayer("nobody"))));

        var scale = Start(Only(CardKinds.RateIt));
        Assert.Throws<GameRuleException>(() => Game.Apply(scale, new SubmitAnswer("ani", Answer.FromScale(11))));
    }

    [Fact]
    public void Next_is_only_allowed_once_answers_are_revealed()
    {
        var state = Start(Only(CardKinds.OpenQuestion));
        Assert.Throws<GameRuleException>(() => Game.Apply(state, new NextCard("ani")));

        state = AnswerAll(state);
        state = Game.Apply(state, new NextCard("arman"));
        Assert.Equal(2, state.Current!.Number);
    }

    [Fact]
    public void Anyone_can_pass_and_the_pass_is_recorded_without_answers()
    {
        var state = Start(Only(CardKinds.OpenQuestion));
        var first = state.Current!.Card.Id;
        state = Game.Apply(state, new SubmitAnswer("ani", Answer.FromText("private")));

        state = Game.Apply(state, new PassCard("arman"));

        var result = Assert.Single(state.History);
        Assert.Equal(first, result.Card.Id);
        Assert.True(result.Passed);
        Assert.NotEqual(first, state.Current!.Card.Id);
    }

    [Fact]
    public void Reactions_can_be_set_changed_and_cleared()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk));
        state = Game.Apply(state, new ReactToCard("ani", true));
        state = Game.Apply(state, new ReactToCard("arman", false));
        state = Game.Apply(state, new ReactToCard("arman", null));
        state = Game.Apply(state, new NextCard("ani"));

        var result = Assert.Single(state.History);
        Assert.Equal(1, result.Likes);
        Assert.Equal(0, result.Dislikes);
    }

    [Fact]
    public void Game_ends_after_the_card_count()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk) with { CardCount = 3 });
        for (var i = 0; i < 3; i++) state = Game.Apply(state, new NextCard("ani"));

        Assert.Equal(GamePhase.Ended, state.Phase);
        Assert.Null(state.Current);
        Assert.Equal(3, state.History.Count);
        Assert.Throws<GameRuleException>(() => Game.Apply(state, new NextCard("ani")));
    }

    [Fact]
    public void Game_ends_early_when_the_pool_runs_out()
    {
        var deck = Deck(Card("a", CardKinds.OpenQuestion), Card("b", CardKinds.OpenQuestion));
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk), deck: deck);
        state = Game.Apply(state, new NextCard("ani"));
        state = Game.Apply(state, new NextCard("ani"));

        Assert.Equal(GamePhase.Ended, state.Phase);
        Assert.Equal(2, state.History.Count);
    }

    [Fact]
    public void No_card_is_dealt_twice()
    {
        var state = Start(new GameSettings { AnswerMode = AnswerMode.Written, CardCount = 40 }, Trio);
        while (state.Phase != GamePhase.Ended)
        {
            state = Game.Apply(state, new PassCard("ani"));
        }

        Assert.Equal(40, state.History.Count);
        Assert.Equal(40, state.History.Select(h => h.Card.Id).Distinct().Count());
    }

    [Fact]
    public void Ending_mid_answer_does_not_record_the_unfinished_card()
    {
        var state = Start(Only(CardKinds.OpenQuestion));
        state = Game.Apply(state, new EndGame("ani"));

        Assert.Equal(GamePhase.Ended, state.Phase);
        Assert.Empty(state.History);
    }

    [Fact]
    public void Ending_while_discussing_records_the_card()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk));
        state = Game.Apply(state, new EndGame("ani"));

        Assert.Single(state.History);
    }

    [Fact]
    public void Unknown_players_are_rejected()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk));
        Assert.Throws<GameRuleException>(() => Game.Apply(state, new NextCard("stranger")));
    }

    [Fact]
    public void The_same_seed_deals_the_same_cards()
    {
        static List<string> Deal(int seed)
        {
            var state = Start(new GameSettings { AnswerMode = AnswerMode.Talk, CardCount = 10 }, seed: seed);
            var ids = new List<string>();
            while (state.Current is { } round)
            {
                ids.Add(round.Card.Id);
                state = Game.Apply(state, new PassCard("ani"));
            }
            return ids;
        }

        Assert.Equal(Deal(7), Deal(7));
        Assert.NotEqual(Deal(7), Deal(8));
    }
}
