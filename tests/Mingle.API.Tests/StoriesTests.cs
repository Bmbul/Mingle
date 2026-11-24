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
        const string six = "{@}-ը գնաց {քաղաք}։ Կերավ {ուտելիք}։ Տեսավ {կենդանի}։ Գնեց {առարկա}։ Զգաց {զգացմունք}։ Ասաց. «{բառ}»։";

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
}
