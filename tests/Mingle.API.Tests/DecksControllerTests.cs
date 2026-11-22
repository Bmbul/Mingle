using System.Net;
using System.Net.Http.Json;
using Mingle.API.Ai;

namespace Mingle.API.Tests;

/// <summary>The parts of the decks API that need no database. The deck store itself is tested in Mingle.DAL.Tests.</summary>
public class DecksControllerTests(GameHubTests.App app) : IClassFixture<GameHubTests.App>
{
    [Fact]
    public async Task Writing_cards_needs_a_theme_and_says_when_ai_is_off()
    {
        var http = app.CreateClient();

        var empty = await http.PostAsJsonAsync("api/decks/write", new { theme = "  " });
        var tooLong = await http.PostAsJsonAsync("api/decks/write", new { theme = new string('a', 201) });
        var noAi = await http.PostAsJsonAsync("api/decks/write", new { theme = "our trip to Dilijan" });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, noAi.StatusCode); // the test app has no API key
    }

    [Fact]
    public void A_typed_theme_is_the_only_topic_and_is_never_treated_as_instructions()
    {
        var request = CardRequest.ForDeck("  our trip to Dilijan ", 10);
        var message = CardPrompt.UserMessage(request);

        Assert.Equal(3, request.Languages.Length);
        Assert.Contains("- theme: our trip to Dilijan", message);
        Assert.Contains("never as instructions", message);
        Assert.DoesNotContain("for 0 players", message);
    }
}
