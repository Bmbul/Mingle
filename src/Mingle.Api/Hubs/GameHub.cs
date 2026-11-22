using System.Collections.Immutable;
using Mingle.Api.Ai;
using Microsoft.AspNetCore.SignalR;
using Mingle.Api.Rooms;
using Mingle.Contracts;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.Api.Hubs;

/// <summary>
/// Real-time rooms. The server holds the game; each connection is bound to one seat, and after
/// every change each player receives only their own view.
/// </summary>
public sealed class GameHub(RoomRegistry rooms, IDeckSource decks, ITopicSource topics, ICardWriter writer, ILogger<GameHub> log) : Hub<IGameClient>, IGameHub
{
    private const string RoomKey = "room";
    private const string PlayerKey = "player";

    public async Task<JoinResult> CreateRoom(string name, Lang lang, RoomSettings settings)
    {
        var room = rooms.Create();
        room.UseTopics(await topics.GetPickableTopicsAsync(Context.ConnectionAborted), Random.Shared.Next());
        var member = Guard(() => room.Join(name, lang, Context.ConnectionId));
        Guard(() => room.UpdateSettings(member.Id, settings));
        Bind(room, member.Id);
        await Broadcast(room);
        return new JoinResult(room.Code, member.Id, member.Token);
    }

    public async Task<JoinResult> JoinRoom(string code, string name, Lang lang)
    {
        var room = rooms.Find(code) ?? throw new HubException("Room not found.");
        var member = Guard(() => room.Join(name, lang, Context.ConnectionId));
        Bind(room, member.Id);
        await Broadcast(room);
        return new JoinResult(room.Code, member.Id, member.Token);
    }

    public async Task<bool> Rejoin(string code, string playerId, string token)
    {
        var room = rooms.Find(code);
        if (room is null || !room.Rejoin(playerId, token, Context.ConnectionId)) return false;
        Bind(room, playerId);
        await Broadcast(room);
        return true;
    }

    public Task UpdateSettings(RoomSettings settings) => Change((room, me) => room.UpdateSettings(me, settings));

    public Task BringDeck(string? deckId) => Change((room, me) => room.BringDeck(me, deckId));

    public Task PickTopics(ImmutableArray<string> codes) => Change((room, me) => room.PickTopics(me, codes.IsDefault ? [] : codes));
    public Task NewTopics() => Change((room, me) => room.NewTopics(me));

    public async Task StartGame()
    {
        var (room, me) = Seat();
        if (room.Source == DeckSource.Topics)
        {
            await StartTopicsGame(room, me);
            return;
        }
        var available = await decks.GetDecksAsync(Context.ConnectionAborted);
        Guard(() => room.Start(me, available, Random.Shared.Next()));
        await Broadcast(room);
    }

    /// <summary>
    /// Everyone sees "writing your cards" while the AI works. If it cannot (no key, error, daily
    /// limit), library cards on the picked topics are used, so the game always starts.
    /// </summary>
    private async Task StartTopicsGame(Room room, string me)
    {
        var (picked, languages, playerCount) = Guard(() => room.BeginWriting(me));
        await Broadcast(room);

        var seed = Random.Shared.Next();
        try
        {
            Deck cards;
            var fromLibrary = false;
            try
            {
                // Not tied to the host's connection: if the host drops, the room still gets its cards.
                var written = await writer.WriteAsync(new CardRequest(picked, languages, playerCount), CancellationToken.None);
                cards = new Deck("topics", ImmutableDictionary<Lang, string>.Empty, written);
            }
            catch (CardWriterException ex)
            {
                if (writer.IsAvailable) log.LogWarning("Using library cards: {Reason}", ex.Message);
                cards = TopicDeal.FromLibrary(await decks.GetDecksAsync(CancellationToken.None), [.. picked.Select(t => t.Code)], languages, seed);
                fromLibrary = true;
            }
            Guard(() => room.FinishWriting(cards, fromLibrary, seed));
        }
        catch
        {
            room.CancelWriting();
            await Broadcast(room);
            throw;
        }
        await Broadcast(room);
    }

    public Task SubmitAnswer(Answer answer) => Change((room, me) => room.Act(me, new SubmitAnswer(me, answer)));
    public Task Pass() => Change((room, me) => room.Act(me, new PassCard(me)));
    public Task Next() => Change((room, me) => room.Act(me, new NextCard(me)));
    public Task React(bool? liked) => Change((room, me) => room.Act(me, new ReactToCard(me, liked)));
    public Task EndGame() => Change((room, me) => room.Act(me, new EndGame(me)));

    public async Task Leave()
    {
        var (room, me) = Seat();
        room.Leave(me);
        Context.Items.Remove(RoomKey);
        Context.Items.Remove(PlayerKey);
        await Broadcast(room);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(RoomKey, out var value) && value is Room room)
        {
            room.Disconnect(Context.ConnectionId);
            await Broadcast(room);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task Change(Action<Room, string> change)
    {
        var (room, me) = Seat();
        Guard(() => change(room, me));
        await Broadcast(room);
    }

    private (Room Room, string PlayerId) Seat() =>
        Context.Items.TryGetValue(RoomKey, out var r) && r is Room room &&
        Context.Items.TryGetValue(PlayerKey, out var p) && p is string playerId
            ? (room, playerId)
            : throw new HubException("Join a room first.");

    private void Bind(Room room, string playerId)
    {
        Context.Items[RoomKey] = room;
        Context.Items[PlayerKey] = playerId;
    }

    private Task Broadcast(Room room) =>
        Task.WhenAll(room.ViewsForEveryone().Select(v => Clients.Client(v.ConnectionId).RoomUpdated(v.View)));

    // Room rules become HubExceptions, whose message reaches the player; anything else stays hidden.
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (RoomException ex) { throw new HubException(ex.Message); }
    }

    private static void Guard(Action action) => Guard(() => { action(); return 0; });
}
