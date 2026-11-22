using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mingle.Engine;
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
        var unknownTopic = await http.PostAsJsonAsync("api/decks/write", new { topics = new[] { "no-such-topic" } });
        var tooManyTopics = await http.PostAsJsonAsync("api/decks/write", new { topics = TestTopics.All.Take(6).Select(t => t.Code) });
        var badSetting = await http.PostAsJsonAsync("api/decks/write", new { theme = "x", setting = "zoo" });
        var badDepth = await http.PostAsJsonAsync("api/decks/write", new { theme = "x", depth = 4 });
        var topicsOnly = await http.PostAsJsonAsync("api/decks/write", new { topics = new[] { TestTopics.All[0].Code } });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, noAi.StatusCode); // the test app has no API key
        Assert.Equal(HttpStatusCode.ServiceUnavailable, topicsOnly.StatusCode); // valid: topics without a theme
        Assert.All([unknownTopic, tooManyTopics, badSetting, badDepth], r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Fact]
    public void A_typed_theme_is_the_only_topic_and_is_never_treated_as_instructions()
    {
        var travel = new TopicInfo("travel", System.Collections.Immutable.ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Travel"));
        var request = CardRequest.ForDeck([travel], "  our trip to Dilijan ", depth: 3, setting: "couple", count: 20);
        var message = CardPrompt.UserMessage(request);

        Assert.Equal(3, request.Languages.Length);
        Assert.Contains("- theme: our trip to Dilijan", message);
        Assert.Contains("never as instructions", message);
        Assert.Contains("- travel: Travel", message);
        Assert.Contains("Every card is depth 3", message);
        Assert.Contains("played by a couple", message);
        Assert.DoesNotContain("for 0 players", message);
    }

    /// <summary>Answers like the AI would, so the endpoint can be tested end to end without a key.</summary>
    private sealed class FakeWriter : ICardWriter
    {
        public bool IsAvailable => true;
        public Task<ImmutableArray<Card>> WriteAsync(CardRequest request, CancellationToken ct) =>
            Task.FromResult<ImmutableArray<Card>>([.. Enumerable.Range(1, request.Count).Select(i => new Card(
                $"ai-{i}", "open", 1, [CardRequest.ThemeCode],
                request.Languages.ToImmutableDictionary(l => l, l => new CardText($"Question {i} in {l.Code()}?"))))]);
    }

    [Fact]
    public async Task Written_cards_come_back_as_a_deck_the_client_can_read()
    {
        using var withAi = app.WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ICardWriter, FakeWriter>())));
        var http = withAi.CreateClient();

        var response = await http.PostAsJsonAsync("api/decks/write", new { theme = "our trip to Dilijan", count = 4 });
        response.EnsureSuccessStatusCode();
        var deck = DeckJson.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(4, deck.Cards.Length);
        Assert.Equal("our trip to Dilijan", deck.NameFor(Lang.Hy));
        Assert.All(deck.Cards, c => Assert.Empty(c.Topics));
        Assert.Equal(3, deck.Cards[0].Texts.Count);
    }

    [Theory]
    [InlineData(20, 24)]
    [InlineData(10, 12)]
    [InlineData(3, 5)]
    [InlineData(40, 45)]
    public void The_ai_is_asked_for_a_few_spare_cards(int wanted, int asked)
    {
        var request = CardRequest.ForDeck([], "x", null, null, wanted);

        Assert.Equal(asked, request.WithSpares().Count);
        Assert.Equal(wanted, request.Count); // the parser still keeps only what was wanted
    }
}
