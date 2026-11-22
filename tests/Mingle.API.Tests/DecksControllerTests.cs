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
}
