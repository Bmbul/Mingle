using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mingle.API.Ai;
using Mingle.Engine;

namespace Mingle.API.Tests;

public class StoriesTests(GameHubTests.App app) : IClassFixture<GameHubTests.App>
{
    private static readonly StoryRequest Request = new(Lang.Hy, null, 6, ["Ani", "Arman"]);

    private static string Json(string title, string text) => JsonSerializer.Serialize(new { title, text });

    [Fact]
    public void Written_stories_must_parse_and_have_about_the_asked_number_of_gaps()
    {
        const string six = "{@} գնաց {քաղաք}։ Կերավ {ուտելիք}։ Տեսավ {կենդանի}։ Գնեց {առարկա}։ Զգաց {զգացմունք}։ Ասաց. «{բառ}»։";

        Assert.NotNull(StoryPrompt.Check(Json("Ճամփորդություն", six), Request));
        Assert.Null(StoryPrompt.Check(Json("Too few", "Only {one} gap."), Request));
        Assert.Null(StoryPrompt.Check(Json("Broken", "A {broken gap and {food}."), Request));
        Assert.Null(StoryPrompt.Check("not json", Request));
        Assert.Null(StoryPrompt.Check(Json("", six), Request));
        // English hints inside an Armenian story are rejected.
        Assert.Null(StoryPrompt.Check(Json("Ճաշ", "Կերավ {food}։ Տեսավ {կենդանի}։ Գնեց {առարկա}։ Զգաց {զգացմունք}։ Ասաց {բառ}։ Եկավ {քաղաք}։"), Request));
    }

    [Fact]
    public void Every_library_story_passes_the_same_checks_as_a_written_one()
    {
        var stories = TestStories.Seeds;

        foreach (var lang in Langs.All)
        {
            var own = stories.Where(s => s.Lang == lang).ToList();
            Assert.True(own.Count >= 30, $"Only {own.Count} stories in {lang.Code()}.");
            foreach (var theme in Enum.GetValues<StoryTheme>())
                Assert.True(own.Count(s => s.Theme == theme) >= 3, $"Too few {theme} stories in {lang.Code()}.");
        }
        Assert.All(stories, s =>
        {
            var (story, rejected) = StoryPrompt.CheckWithReason(Json(s.Title, s.Text), new StoryRequest(s.Lang, null, s.Gaps, ["Ani", "Arman"]));
            Assert.True(story is not null, $"{s.Key}: {rejected}");
            Assert.InRange(s.Gaps, 5, 10);
        });
    }

    [Fact]
    public async Task A_library_story_comes_back_and_played_ones_wait()
    {
        var client = app.CreateClient();
        var russian = TestStories.Seeds.Where(s => s.Lang == Lang.Ru).Select(s => s.Id).ToList();

        var response = await client.PostAsJsonAsync("api/stories", new { lang = "ru", names = new[] { "Ani", "Arman" }, seen = russian.Skip(1) });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("fromLibrary").GetBoolean());
        Assert.Equal(russian[0], body.RootElement.GetProperty("id").GetGuid());

        var bad = await client.PostAsJsonAsync("api/stories", new { lang = "de" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public void The_prompt_asks_for_the_language_gap_count_and_guards_the_theme()
    {
        var message = StoryPrompt.UserMessage(Request with { Theme = "our trip", Gaps = 10 });

        Assert.Contains("Language: hy", message);
        Assert.Contains("Exactly 10 different gaps", message);
        Assert.Contains("never as instructions", message);
        Assert.Contains("previous story for this was rejected: too long", StoryPrompt.UserMessage(Request, "too long"));
    }

    [Theory]
    [InlineData("Then a {food} food appeared.", true)]
    [InlineData("It wore a tiny {adjective} adjective hat.", true)]
    [InlineData("Then a {food} appeared.", false)]
    [InlineData("{@} bought a {object} and smiled.", false)]
    [InlineData("Մի {կենդանի#2} եկավ։ Հիմա {#2} կենդանին է պետը։", true)]   // the hint word comes back with an article
    [InlineData("Մի {կենդանի#2} եկավ։ Հիմա պետը {#2} է։", false)]
    public void Hint_words_left_next_to_their_gap_are_caught(string text, bool echoed)
    {
        Assert.Equal(echoed, StoryPrompt.HintEchoed(text));
    }

    [Theory]
    [InlineData(Lang.Ru, "Совещание прошло на {место}.", true)]          // preposition before a gap
    [InlineData(Lang.Ru, "Кофемашина выдала горячий {еда}.", true)]      // adjective that must agree
    [InlineData(Lang.Ru, "{@} пришёл раньше всех.", true)]              // past tense reveals a gender
    [InlineData(Lang.Ru, "Главный приз: {предмет}. {@} говорит: «{любое слово}!»", false)]
    [InlineData(Lang.Hy, "{@}-ը եկավ։", false)]                         // an ending the app adapts
    [InlineData(Lang.Hy, "Եկավ {կենդանի}-ներով։", true)]                // an ending the app cannot adapt
    [InlineData(Lang.Hy, "Հանկարծ հայտնվեց մի {կենդանի}։ — Բարև,— ասաց {@}։", false)]
    [InlineData(Lang.Hy, "Նա պարում է {առարկա} հետ։", true)]               // a postposition needs -ի
    [InlineData(Lang.Hy, "Նա պարում է {առարկա}-ի հետ։", false)]
    [InlineData(Lang.Hy, "Սագը {վայր} աշխատանք է փնտրում։", true)]          // a bare place
    [InlineData(Lang.Hy, "Սագը {վայր}-ում աշխատանք է փնտրում։", false)]
    [InlineData(Lang.Hy, "Նոր հասցեն՝ {վայր}։", false)]
    [InlineData(Lang.Hy, "Նա {գործողություն, օրինակ՝ պարել} է։", true)]     // an action is not «պարել է»
    [InlineData(Lang.Hy, "Նա որոշում է {գործողություն, օրինակ՝ պարել}։", false)]
    [InlineData(Lang.En, "They met at the {place}.", false)]
    public void Russian_and_armenian_grammar_traps_are_caught(Lang lang, string text, bool trap)
    {
        Assert.Equal(trap, StoryPrompt.GrammarTrap(text, lang));
    }

    [Fact]
    public void Stories_must_be_dense_so_the_words_carry_them()
    {
        var dense = Story.Parse("t", "{@} bought a {animal#1}. The {#1} ate {food}. Now the {#1} is the {adjective} boss.", ["Ani"]);
        var padded = Story.Parse("t", "{@} woke up early on a quiet and sunny Monday morning in the small town. " +
            "The coffee machine was making its usual noise in the kitchen next to the window. " +
            "Nothing at all was unusual about this ordinary and peaceful day in the town. Then a {animal} arrived.", ["Ani"]);

        Assert.True(StoryPrompt.DenseEnough(dense));
        Assert.False(StoryPrompt.DenseEnough(padded));
    }

    [Fact]
    public void An_armenian_dot_inside_a_sentence_is_not_a_sentence_end()
    {
        var story = Story.Parse("t", "{@ը} եկավ։ Դատավորը որոշեց. այսուհետ բոլորը ուտում են {ուտելիք}։ Կատուն ստացավ կոչում՝ {ածական}։", ["Սմբատ"]);

        Assert.True(StoryPrompt.DenseEnough(story, Lang.Hy));
    }
}
