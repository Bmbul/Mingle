using Mingle.Api.Rooms;
using Mingle.Contracts;
using Mingle.Engine;

namespace Mingle.Api.Tests;

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
}
