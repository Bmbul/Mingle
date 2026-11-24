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
    public void Every_ready_made_story_parses_in_every_language()
    {
        var library = new StoryLibrary();

        foreach (var lang in Langs.All)
        {
            Assert.NotEmpty(library.For(lang));
            Assert.All(library.For(lang), s =>
            {
                var story = Story.Parse(s.Title, s.Text, ["Ani", "Arman"]);
                Assert.InRange(story.Gaps.Length, 5, 9);
                Assert.DoesNotContain("{", story.Fill([]));
            });
        }
    }

    [Fact]
    public async Task Without_ai_a_ready_made_story_in_the_right_language_comes_back()
    {
        var response = await app.CreateClient().PostAsJsonAsync("api/stories", new { lang = "ru", names = new[] { "Ani", "Arman" } });

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("fromLibrary").GetBoolean());
        var text = body.RootElement.GetProperty("text").GetString()!;
        Assert.Contains(new StoryLibrary().For(Lang.Ru), s => s.Text == text);

        var bad = await app.CreateClient().PostAsJsonAsync("api/stories", new { lang = "de" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public void The_prompt_asks_for_the_language_gap_count_and_guards_the_theme()
    {
        var message = StoryPrompt.UserMessage(Request with { Theme = "our trip", Gaps = 10 });

        Assert.Contains("Language: hy", message);
        Assert.Contains("Exactly 10 gaps", message);
        Assert.Contains("never as instructions", message);
    }

    [Theory]
    [InlineData("Then a {food} food appeared.", true)]
    [InlineData("It wore a tiny {adjective} adjective hat.", true)]
    [InlineData("Then a {food} appeared.", false)]
    [InlineData("{@} bought a {object} and smiled.", false)]
    public void Hint_words_left_next_to_their_gap_are_caught(string text, bool echoed)
    {
        Assert.Equal(echoed, StoryPrompt.HintEchoed(text));
    }

    [Theory]
    [InlineData(Lang.Ru, "Совещание прошло на {место}.", true)]          // preposition before a gap
    [InlineData(Lang.Ru, "Кофемашина выдала горячий {еда}.", true)]      // adjective that must agree
    [InlineData(Lang.Ru, "{@} пришёл раньше всех.", true)]              // past tense reveals a gender
    [InlineData(Lang.Ru, "Главный приз: {предмет}. {@} говорит: «{любое слово}!»", false)]
    [InlineData(Lang.Hy, "{@}-ը եկավ։", true)]                          // suffix on a name
    [InlineData(Lang.Hy, "Հանկարծ հայտնվեց մի {կենդանի}։ — Բարև,— ասաց {@}։", false)]
    [InlineData(Lang.En, "They met at the {place}.", false)]
    public void Russian_and_armenian_grammar_traps_are_caught(Lang lang, string text, bool trap)
    {
        Assert.Equal(trap, StoryPrompt.GrammarTrap(text, lang));
    }
}
