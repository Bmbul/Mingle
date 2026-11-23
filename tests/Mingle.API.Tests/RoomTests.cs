using System.Collections.Immutable;
using Mingle.API.Rooms;
using Mingle.Contracts;
using Mingle.Engine;

namespace Mingle.API.Tests;

public class RoomTests
{
    private static readonly RoomSettings Written = new(new GameSettings { KindIds = ["finish"], CardCount = 5 }, ["test"]);

    private static (Room Room, RoomMember Ani, RoomMember Arman) Lobby()
    {
        var room = new Room("ABCD", new ManualClock());
        var ani = room.Join("Ani", Lang.Hy, "c-ani");
        var arman = room.Join("Arman", Lang.En, "c-arman");
        room.UpdateSettings(ani.Id, Written);
        return (room, ani, arman);
    }

    [Fact]
    public void The_first_player_is_the_host()
    {
        var (room, ani, arman) = Lobby();
        var view = room.ViewFor(arman.Id);

        Assert.True(view.Players.Single(p => p.Id == ani.Id).IsHost);
        Assert.False(view.YouAreHost);
    }

    [Fact]
    public void Only_the_host_can_change_settings_or_start()
    {
        var (room, _, arman) = Lobby();

        Assert.Throws<RoomException>(() => room.UpdateSettings(arman.Id, Written));
        Assert.Throws<RoomException>(() => room.Start(arman.Id, TestDecks.All, 1));
    }

    [Fact]
    public void A_game_needs_two_players()
    {
        var room = new Room("ABCD", new ManualClock());
        var ani = room.Join("Ani", Lang.Hy, "c1");

        Assert.Throws<RoomException>(() => room.Start(ani.Id, TestDecks.All, 1));
    }

    [Fact]
    public void Nobody_can_join_once_the_game_has_started()
    {
        var (room, ani, _) = Lobby();
        room.Start(ani.Id, TestDecks.All, 1);

        Assert.Throws<RoomException>(() => room.Join("Lilit", Lang.Ru, "c3"));
    }

    [Fact]
    public void Players_only_act_for_themselves()
    {
        var (room, ani, arman) = Lobby();
        room.Start(ani.Id, TestDecks.All, 1);

        Assert.Throws<RoomException>(() => room.Act(arman.Id, new SubmitAnswer(ani.Id, Answer.FromText("not mine"))));
    }

    [Fact]
    public void Each_view_hides_the_other_answers_until_everyone_has_answered()
    {
        var (room, ani, arman) = Lobby();
        room.Start(ani.Id, TestDecks.All, 1);

        room.Act(ani.Id, new SubmitAnswer(ani.Id, Answer.FromText("secret")));
        var armanView = room.ViewFor(arman.Id).Game!;
        Assert.Empty(armanView.RevealedAnswers);
        Assert.True(armanView.MustAnswer);
        Assert.Equal("secret", room.ViewFor(ani.Id).Game!.MyAnswer!.Text);

        room.Act(arman.Id, new SubmitAnswer(arman.Id, Answer.FromText("mine")));
        Assert.Equal(2, room.ViewFor(arman.Id).Game!.RevealedAnswers.Length);
    }

    [Fact]
    public void Each_player_reads_the_card_in_their_own_language()
    {
        var (room, ani, arman) = Lobby();
        room.Start(ani.Id, TestDecks.All, 1);

        Assert.EndsWith("in hy", room.ViewFor(ani.Id).Game!.Prompt);
        Assert.EndsWith("in en", room.ViewFor(arman.Id).Game!.Prompt);
    }

    [Fact]
    public void A_seat_is_reclaimed_only_with_its_token()
    {
        var (room, ani, _) = Lobby();
        room.Disconnect("c-ani");

        Assert.False(room.Rejoin(ani.Id, "wrong-token", "c-new"));
        Assert.True(room.Rejoin(ani.Id, ani.Token, "c-new"));
        Assert.Contains(room.ViewsForEveryone(), v => v.ConnectionId == "c-new");
    }

    [Fact]
    public void When_the_host_leaves_the_lobby_someone_else_becomes_host()
    {
        var (room, ani, arman) = Lobby();
        room.Leave(ani.Id);

        var view = room.ViewFor(arman.Id);
        Assert.Single(view.Players);
        Assert.True(view.YouAreHost);
    }

    [Fact]
    public void Ending_the_game_gives_everyone_a_summary()
    {
        var (room, ani, arman) = Lobby();
        room.Start(ani.Id, TestDecks.All, 1);
        room.Act(ani.Id, new PassCard(ani.Id));
        room.Act(arman.Id, new EndGame(arman.Id));

        var view = room.ViewFor(ani.Id);
        Assert.Equal(RoomStage.Ended, view.Stage);
        Assert.Equal(1, view.Summary!.Passed);
    }

    [Fact]
    public void Rooms_nobody_uses_are_cleaned_up()
    {
        var clock = new ManualClock();
        var registry = new RoomRegistry(clock);
        var empty = registry.Create();
        var busy = registry.Create();
        busy.Join("Ani", Lang.Hy, "c1");

        clock.Now += TimeSpan.FromHours(3);
        var removed = registry.RemoveIdle(TimeSpan.FromHours(2));

        Assert.Equal(1, removed);
        Assert.Null(registry.Find(empty.Code));
        Assert.NotNull(registry.Find(busy.Code.ToLowerInvariant())); // codes are case-insensitive
    }

    private static Deck Pack(string id, int cards) => new(
        id, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, id),
        [.. Enumerable.Range(1, cards).Select(i => new Card(
            $"{id}-{i}", "open", 1 + i % 3, [],
            Langs.All.ToImmutableDictionary(l => l, l => new CardText($"{id} {i} in {l.Code()}"))))]);

    private static readonly RoomSettings EveryoneBrings = new(new GameSettings { KindIds = ["finish"], CardCount = 3 }, [], DeckSource.Everyone);

    [Fact]
    public void Everyone_brings_a_deck_and_ten_cards_come_from_each()
    {
        var (room, ani, arman) = Lobby();
        room.UpdateSettings(ani.Id, EveryoneBrings);
        room.BringDeck(ani.Id, "party");
        room.BringDeck(arman.Id, "deep");

        Assert.Equal("deep", room.ViewFor(ani.Id).Players.Single(p => p.Id == arman.Id).DeckId);

        room.Start(ani.Id, [Pack("party", 30), Pack("deep", 30), Pack("unused", 30)], 5);
        var game = room.ViewFor(ani.Id).Game!;

        // Every card type and depth counts, whatever the settings said: 10 from each of the two decks.
        Assert.Equal(20, game.CardCount);
        Assert.Equal("open", game.KindId);
        Assert.DoesNotContain("unused", game.Prompt);
    }

    [Fact]
    public void Everyone_brings_needs_at_least_one_deck_and_only_in_the_lobby()
    {
        var (room, ani, arman) = Lobby();
        room.UpdateSettings(ani.Id, EveryoneBrings);

        Assert.Throws<RoomException>(() => room.Start(ani.Id, [Pack("party", 30)], 5));

        room.BringDeck(arman.Id, "party");
        room.Start(ani.Id, [Pack("party", 30)], 5);

        Assert.Equal(10, room.ViewFor(ani.Id).Game!.CardCount);
        Assert.Throws<RoomException>(() => room.BringDeck(ani.Id, "party"));
    }

    private static (Room Room, RoomMember Ani, RoomMember Arman) TopicsLobby()
    {
        var room = new Room("TOPS", new ManualClock());
        room.UseTopics(TestTopics.All, 1);
        var ani = room.Join("Ani", Lang.Hy, "c-ani");
        var arman = room.Join("Arman", Lang.En, "c-arman");
        room.UpdateSettings(ani.Id, new RoomSettings(new GameSettings(), [], DeckSource.Topics));
        return (room, ani, arman);
    }

    [Fact]
    public void Topic_picks_come_from_your_own_offer_and_stay_private()
    {
        var (room, ani, arman) = TopicsLobby();
        var offer = room.ViewFor(ani.Id).Topics!.Offer;

        Assert.Equal(10, offer.Length);
        Assert.StartsWith("Topic", offer[0].Name);
        Assert.EndsWith("in hy", offer[0].Name);
        Assert.Throws<RoomException>(() => room.PickTopics(ani.Id, offer.Take(4).Select(o => o.Code)));

        var notOffered = TestTopics.All.Select(t => t.Code).Except(offer.Select(o => o.Code)).First();
        Assert.Throws<RoomException>(() => room.PickTopics(ani.Id, [notOffered]));

        room.PickTopics(ani.Id, [offer[0].Code, offer[1].Code]);
        Assert.Equal(2, room.ViewFor(ani.Id).Topics!.Picked.Length);
        Assert.Empty(room.ViewFor(arman.Id).Topics!.Picked);
        Assert.True(room.ViewFor(arman.Id).Players.Single(p => p.Id == ani.Id).PickedTopics);
    }

    [Fact]
    public void A_new_offer_keeps_your_picks()
    {
        var (room, ani, _) = TopicsLobby();
        var first = room.ViewFor(ani.Id).Topics!.Offer;
        room.PickTopics(ani.Id, [first[0].Code]);

        room.NewTopics(ani.Id);
        var view = room.ViewFor(ani.Id).Topics!;

        Assert.Contains(first[0].Code, view.Picked);
        Assert.Contains(view.Offer, o => o.Code == first[0].Code); // still shown, so it can be unpicked
        room.PickTopics(ani.Id, [first[0].Code, view.Offer[0].Code]);
    }

    [Fact]
    public void Writing_waits_between_the_lobby_and_play()
    {
        var (room, ani, arman) = TopicsLobby();
        Assert.Throws<RoomException>(() => room.BeginWriting(ani.Id)); // nobody picked yet

        var code = room.ViewFor(arman.Id).Topics!.Offer[0].Code;
        room.PickTopics(arman.Id, [code]);
        Assert.Throws<RoomException>(() => room.BeginWriting(arman.Id)); // only the host

        var (topics, languages, players, depths) = room.BeginWriting(ani.Id);
        Assert.Empty(depths); // nothing chosen: the usual mix
        Assert.Equal([code], topics.Select(t => t.Code).ToArray());
        Assert.Equal([Lang.En, Lang.Hy], languages.Order().ToArray());
        Assert.Equal(2, players);
        Assert.Equal(RoomStage.Writing, room.Stage);
        Assert.Throws<RoomException>(() => room.Join("Late", Lang.Ru, "c-late"));

        room.FinishWriting(TestDecks.Deck, fromLibrary: false, seed: 1);
        Assert.Equal(RoomStage.Playing, room.Stage);
        Assert.Equal(10, room.ViewFor(ani.Id).Game!.CardCount);
        Assert.False(room.ViewFor(ani.Id).LibraryCards);
    }

    [Fact]
    public void Writing_can_be_cancelled_back_to_the_lobby()
    {
        var (room, ani, arman) = TopicsLobby();
        room.PickTopics(arman.Id, [room.ViewFor(arman.Id).Topics!.Offer[0].Code]);
        room.BeginWriting(ani.Id);

        room.CancelWriting();

        Assert.Equal(RoomStage.Lobby, room.Stage);
    }

    [Fact]
    public void Chosen_depths_reach_the_writer_and_all_three_means_the_mix()
    {
        var (room, ani, arman) = TopicsLobby();
        room.PickTopics(arman.Id, [room.ViewFor(arman.Id).Topics!.Offer[0].Code]);

        room.UpdateSettings(ani.Id, new RoomSettings(new GameSettings(), [], DeckSource.Topics, [3, 1, 7]));
        Assert.Equal([1, 3], room.BeginWriting(ani.Id).Depths.ToArray()); // 7 is ignored
        room.CancelWriting();

        room.UpdateSettings(ani.Id, new RoomSettings(new GameSettings(), [], DeckSource.Topics, [1, 2, 3]));
        Assert.Empty(room.BeginWriting(ani.Id).Depths);
    }
}
