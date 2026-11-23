using System.Collections.Immutable;
using System.Text.Json;
using Mingle.API.Ai;
using Mingle.Engine;

namespace Mingle.API.Tests;

public class CardWriterTests
{
    private static readonly CardRequest Request = new(
        [new TopicInfo("travel", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Travel")),
         new TopicInfo("food", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Food"))],
        [Lang.En, Lang.Hy],
        PlayerCount: 2,
        Count: 5);

    private static string Json(params object[] cards) => JsonSerializer.Serialize(new { cards });

    private static object Open(string en, string hy, string topic = "travel", int depth = 1) =>
        new { kind = "open", depth, topic, text = new { en, hy }, options = new { en = Array.Empty<string>(), hy = Array.Empty<string>() } };

    [Fact]
    public void Reads_cards_in_every_requested_language()
    {
        var wyr = new { kind = "wyr", depth = 1, topic = "food", text = new { en = "", hy = "" },
                        options = new { en = new[] { "Tea", "Coffee" }, hy = new[] { "Թեյ", "Սուրճ" } } };

        var cards = CardParser.Parse(Json(Open("Where to next?", "Ո՞ւր հաջորդը։"), wyr), Request);

        Assert.Equal(2, cards.Length);
        Assert.Equal("Ո՞ւր հաջորդը։", cards[0].Texts[Lang.Hy].Text);
        Assert.Equal(["travel"], cards[0].Topics.ToArray());
        Assert.Equal("Would you rather…", cards[1].Texts[Lang.En].Text);
        Assert.Equal(["Թեյ", "Սուրճ"], cards[1].Texts[Lang.Hy].Options.ToArray());
        Assert.All(cards, c => Assert.StartsWith("ai-", c.Id));
    }

    [Fact]
    public void Drops_cards_that_break_the_rules()
    {
        var oneOption = new { kind = "wyr", depth = 1, topic = "food", text = new { en = "", hy = "" },
                              options = new { en = new[] { "Tea" }, hy = new[] { "Թեյ" } } };
        var missingArmenian = new { kind = "open", depth = 1, topic = "food", text = new { en = "Hi?" }, options = new { } };

        var cards = CardParser.Parse(Json(
            Open("Ask {player} something", "Հարց"),       // placeholder
            Open("Fine?", "Լավ?", topic: "politics"),      // topic nobody picked
            Open("Too deep?", "Շատ խորը՞", depth: 4),      // depth out of range
            new { kind = "riddle", depth = 1, topic = "food", text = new { en = "?", hy = "?" }, options = new { } },
            oneOption,
            missingArmenian,
            Open("Same?", "Նույնը՞"),
            Open("Same?", "Նույնը՞"),                       // duplicate
            Open("Kept?", "Մնա՞ց")), Request);

        Assert.Equal(["Same?", "Kept?"], cards.Select(c => c.Texts[Lang.En].Text).ToArray());
    }

    [Fact]
    public void Garbage_gives_no_cards_and_extra_cards_are_cut()
    {
        Assert.Empty(CardParser.Parse("not json", Request));
        Assert.Empty(CardParser.Parse("{\"cards\": 3}", Request));

        var many = Enumerable.Range(1, 9).Select(i => Open($"Q{i}?", $"Հ{i}՞")).ToArray();
        Assert.Equal(5, CardParser.Parse(Json(many), Request).Length);
    }

    [Fact]
    public void The_schema_asks_only_for_the_tables_languages_and_picked_topics()
    {
        var schema = JsonSerializer.Serialize(CardPrompt.Schema(Request));

        Assert.Contains("\"hy\"", schema);
        Assert.DoesNotContain("\"ru\"", schema);
        Assert.Contains("\"travel\",\"food\"", schema);
        Assert.Contains("- food: Food", CardPrompt.UserMessage(Request));
    }

    [Fact]
    public void The_daily_limit_resets_each_day()
    {
        var clock = new ManualClock();
        var limit = new DailyLimit(clock);

        Assert.True(limit.TryTake(2));
        Assert.True(limit.TryTake(2));
        Assert.False(limit.TryTake(2));

        clock.Now = clock.Now.AddDays(1);
        Assert.True(limit.TryTake(2));
    }

    [Fact]
    public void Quotes_around_the_opening_words_are_removed()
    {
        var likely = new { kind = "likely", depth = 1, topic = "food",
                           text = new { en = "Who is most likely to cook?", hy = "«Ո՞վ ավելի շուտ» ճաշ կպատրաստի։" }, options = new { } };

        var card = Assert.Single(CardParser.Parse(Json(likely), Request));

        Assert.Equal("Ո՞վ ավելի շուտ ճաշ կպատրաստի։", card.Texts[Lang.Hy].Text);
    }

    [Theory]
    [InlineData("Որ քաղաքում կուզեիր մեկ կեսօր թափառել։", "Ո՞ր քաղաքում կուզեիր մեկ կեսօր թափառել։")]
    [InlineData("Ինչու ես սիրում ծովը?", "Ինչո՞ւ ես սիրում ծովը։")]
    [InlineData("1-ից 10, որքան ես սիրում ճամփորդել։", "1-ից 10, որքա՞ն ես սիրում ճամփորդել։")]
    [InlineData("Ինչ է այն, որ քեզ ուրախացնում է։", "Ի՞նչ է այն, որ քեզ ուրախացնում է։")]
    [InlineData("Ո՞վ ավելի շուտ կուշանա։", "Ո՞վ ավելի շուտ կուշանա։")]   // already marked: unchanged
    [InlineData("Քո սիրելի երգը։", "Քո սիրելի երգը։")]               // no question word: unchanged
    public void Armenian_questions_get_their_question_mark(string written, string expected)
    {
        Assert.Equal(expected, ArmenianQuestions.Fix(written));
    }

    [Fact]
    public void Finish_the_sentence_cards_are_not_turned_into_questions()
    {
        var finish = new { kind = "finish", depth = 1, topic = "food", text = new { en = "What I cook best is…", hy = "Ինչ ամենալավն եմ պատրաստում…" }, options = new { } };
        var open = Open("Which city?", "Որ քաղաքը։");

        var cards = CardParser.Parse(Json(finish, open), Request);

        Assert.Equal("Ինչ ամենալավն եմ պատրաստում…", cards[0].Texts[Lang.Hy].Text);
        Assert.Equal("Ո՞ր քաղաքը։", cards[1].Texts[Lang.Hy].Text);
    }

    [Fact]
    public void Topic_descriptions_reach_the_prompt()
    {
        var dreams = new TopicInfo("dreams", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Dreams"), "hopes and ambitions (not dreams while sleeping)");

        var message = CardPrompt.UserMessage(Request with { Topics = [dreams] });

        Assert.Contains("- dreams: Dreams (hopes and ambitions (not dreams while sleeping))", message);
    }

    [Fact]
    public void Guess_cards_written_as_instructions_are_dropped()
    {
        var instruction = new { kind = "guess", depth = 1, topic = "travel", text = new { en = "Guess which country this player would pick.", hy = "Գուշակեք՝ ո՞ր երկիրը։" }, options = new { } };
        var question = new { kind = "guess", depth = 1, topic = "travel", text = new { en = "Which country would you pick?", hy = "Ո՞ր երկիրը կընտրեիր։" }, options = new { } };

        var cards = CardParser.Parse(Json(instruction, question), Request);

        Assert.Equal(["Which country would you pick?"], cards.Select(c => c.Texts[Lang.En].Text).ToArray());
    }

    [Fact]
    public void Association_requests_accept_only_short_words()
    {
        var words = Request with { Associations = true };
        object Word(string en, string hy) => new { kind = "word", depth = 1, topic = "travel", text = new { en, hy }, options = new { } };

        var cards = CardParser.Parse(Json(
            Word("Sea.", "Ծով։"),                                   // trailing stops are trimmed
            Word("What is the sea?", "Ի՞նչ է ծովը"),               // a question
            Word("A very long phrase about the sea", "Շատ երկար"), // too long
            Open("Where to?", "Ո՞ւր")), words);                     // not an association

        Assert.Equal(["Sea"], cards.Select(c => c.Texts[Lang.En].Text).ToArray());
        Assert.Equal("Ծով", cards[0].Texts[Lang.Hy].Text);
        Assert.Contains("\"word\"", JsonSerializer.Serialize(CardPrompt.Schema(words)));
        Assert.DoesNotContain("\"word\"", JsonSerializer.Serialize(CardPrompt.Schema(Request)));
    }
}
