using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mingle.Contracts;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.API.Tests;

/// <summary>Two real SignalR clients play through the hub, end to end, without a database.</summary>
public class GameHubTests(GameHubTests.App app) : IClassFixture<GameHubTests.App>
{
    public sealed class App : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Ai:ApiKey", ""); // never call the real AI from tests: topics games use library cards
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IDeckSource, TestDecks>());
                services.Replace(ServiceDescriptor.Singleton<ITopicSource, TestTopics>());
            });
        }
    }

    /// <summary>A connected player that records every view the server sends.</summary>
    private sealed class Player : IAsyncDisposable
    {
        private readonly ConcurrentQueue<RoomView> views = new();
        public HubConnection Hub { get; }

        public Player(App app)
        {
            Hub = new HubConnectionBuilder()
                .WithUrl(new Uri(app.Server.BaseAddress, HubRoutes.Game), o =>
                {
                    o.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                    o.Transports = HttpTransportType.LongPolling;
                })
                .AddJsonProtocol(o => ContractsJsonContext.Register(o.PayloadSerializerOptions))
                .Build();
            Hub.On<RoomView>(nameof(IGameClient.RoomUpdated), views.Enqueue);
        }

        public RoomView? Latest => views.LastOrDefault();

        /// <summary>Waits until a view arrives that satisfies the condition.</summary>
        public async Task<RoomView> WaitFor(Func<RoomView, bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (views.LastOrDefault() is { } view && condition(view)) return view;
                await Task.Delay(25);
            }
            throw new TimeoutException($"No matching view. Last: {views.LastOrDefault()}");
        }

        public ValueTask DisposeAsync() => Hub.DisposeAsync();
    }

    private static readonly RoomSettings Settings = new(new GameSettings { KindIds = ["finish"], CardCount = 3 }, ["test"]);

    [Fact]
    public async Task Two_players_create_join_answer_and_see_the_reveal()
    {
        await using var ani = new Player(app);
        await using var arman = new Player(app);
        await ani.Hub.StartAsync();
        await arman.Hub.StartAsync();

        var created = await ani.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), "Ani", Lang.Hy, Settings);
        await arman.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), created.RoomCode.ToLowerInvariant(), "Arman", Lang.En);
        await ani.WaitFor(v => v.Players.Length == 2);

        await ani.Hub.InvokeAsync(nameof(IGameHub.StartGame));
        var armanCard = await arman.WaitFor(v => v.Stage == RoomStage.Playing);
        Assert.EndsWith("in en", armanCard.Game!.Prompt);

        await ani.Hub.InvokeAsync(nameof(IGameHub.SubmitAnswer), Answer.FromText("a secret"));
        var waiting = await arman.WaitFor(v => v.Game!.AnsweredCount == 1);
        Assert.Empty(waiting.Game!.RevealedAnswers); // Ani's answer must not reach Arman yet

        await arman.Hub.InvokeAsync(nameof(IGameHub.SubmitAnswer), Answer.FromText("mine"));
        var revealed = await ani.WaitFor(v => v.Game!.Phase == GamePhase.Discussing);
        Assert.Contains(revealed.Game!.RevealedAnswers, a => a.Text == "a secret");
        Assert.Contains(revealed.Game!.RevealedAnswers, a => a.Text == "mine");

        await arman.Hub.InvokeAsync(nameof(IGameHub.Next));
        await ani.WaitFor(v => v.Game!.CardNumber == 2);
    }

    [Fact]
    public async Task A_dropped_player_gets_their_seat_back_with_the_token()
    {
        await using var ani = new Player(app);
        await ani.Hub.StartAsync();
        var created = await ani.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), "Ani", Lang.Hy, Settings);
        await ani.Hub.StopAsync();

        await using var again = new Player(app);
        await again.Hub.StartAsync();
        Assert.False(await again.Hub.InvokeAsync<bool>(nameof(IGameHub.Rejoin), created.RoomCode, created.PlayerId, "not-the-token"));
        Assert.True(await again.Hub.InvokeAsync<bool>(nameof(IGameHub.Rejoin), created.RoomCode, created.PlayerId, created.Token));

        var view = await again.WaitFor(v => v.YouId == created.PlayerId);
        Assert.True(view.YouAreHost);
    }

    [Fact]
    public async Task Room_rules_reach_the_player_as_readable_errors()
    {
        await using var ani = new Player(app);
        await ani.Hub.StartAsync();
        await ani.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), "Ani", Lang.Hy, Settings);

        var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => ani.Hub.InvokeAsync(nameof(IGameHub.StartGame)));
        Assert.Contains("At least 2 players", ex.Message);
    }

    [Fact]
    public async Task Joining_an_unknown_room_fails_clearly()
    {
        await using var ani = new Player(app);
        await ani.Hub.StartAsync();

        var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(
            () => ani.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), "ZZZZ", "Ani", Lang.Hy));
        Assert.Contains("Room not found", ex.Message);
    }

    [Fact]
    public async Task A_topics_game_without_ai_starts_with_library_cards()
    {
        await using var ani = new Player(app);
        await using var arman = new Player(app);
        await ani.Hub.StartAsync();
        await arman.Hub.StartAsync();

        var settings = new RoomSettings(new GameSettings(), [], DeckSource.Topics);
        var created = await ani.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), "Ani", Lang.Hy, settings);
        await arman.Hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), created.RoomCode, "Arman", Lang.En);

        var armanLobby = await arman.WaitFor(v => v.Players.Length == 2 && v.Topics is not null);
        Assert.Equal(10, armanLobby.Topics!.Offer.Length);
        Assert.EndsWith("in en", armanLobby.Topics.Offer[0].Name);

        await arman.Hub.InvokeAsync(nameof(IGameHub.PickTopics), System.Collections.Immutable.ImmutableArray.Create(armanLobby.Topics.Offer[0].Code));
        var aniLobby = await ani.WaitFor(v => v.Players.Any(p => p.PickedTopics));
        Assert.Empty(aniLobby.Topics!.Picked); // Arman's picks are never shown to Ani

        await ani.Hub.InvokeAsync(nameof(IGameHub.StartGame));
        var playing = await arman.WaitFor(v => v.Stage == RoomStage.Playing);
        Assert.True(playing.LibraryCards);
        Assert.Equal(TestDecks.Deck.Cards.Length, playing.Game!.CardCount);
    }
}
