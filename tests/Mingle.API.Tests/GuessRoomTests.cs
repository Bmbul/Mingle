using System.Collections.Immutable;
using Mingle.API.Rooms;
using Mingle.Contracts;
using Mingle.Engine;

namespace Mingle.API.Tests;

public class GuessRoomTests
{
    private static readonly Deck GuessDeck = new("guess", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Guess"),
        [.. Enumerable.Range(1, 4).Select(i => new Card($"g{i}", CardKinds.GuessMyAnswer.Id, 1, [],
            ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText("How many countries has {@} been to?"))))]);

    private static (Room Room, RoomMember Ani, RoomMember Arman, RoomMember Lilit) Started()
    {
        var room = new Room("GUES", new ManualClock());
        var ani = room.Join("Ani", Lang.En, "c-ani");
        var arman = room.Join("Arman", Lang.En, "c-arman");
        var lilit = room.Join("Lilit", Lang.En, "c-lilit");
        room.UpdateSettings(ani.Id, new RoomSettings(new GameSettings(), ["guess"], Guess: new GuessSettings(3)));
        room.Start(ani.Id, [GuessDeck, TestDecks.Deck], 1);
        return (room, ani, arman, lilit);
    }

    [Fact]
    public void Everyone_writes_then_the_turn_player_marks_who_got_it()
    {
        var (room, ani, arman, lilit) = Started();
        var view = room.ViewFor(arman.Id).Guess!;
        Assert.Equal("How many countries has Ani been to?", view.Prompt);
        Assert.Equal(3, view.Count);

        room.ActOnGuess(ani.Id, g => g.Write(ani.Id, "12"));
        room.ActOnGuess(arman.Id, g => g.Write(arman.Id, "11"));
        Assert.Empty(room.ViewFor(lilit.Id).Guess!.Answers);
        room.ActOnGuess(lilit.Id, g => g.Write(lilit.Id, "3"));
        Assert.Equal(GuessStage.Marking, room.ViewFor(lilit.Id).Guess!.Stage);

        // Only Ani (whose turn it is) or the host marks; Ani is both here, so Arman tries and fails.
        Assert.Throws<RoomException>(() => room.ActOnGuess(arman.Id, g => g.Mark(arman.Id, true), turnOrHost: true));
        room.ActOnGuess(ani.Id, g => g.Mark(arman.Id, true), turnOrHost: true);
        room.ActOnGuess(ani.Id, g => g.Next(), turnOrHost: true);

        var next = room.ViewFor(lilit.Id).Guess!;
        Assert.Equal(arman.Id, next.TurnPlayerId);
        Assert.Equal(1, next.Scores.Single(s => s.PlayerId == arman.Id).Points);
    }

    [Fact]
    public void Ending_the_game_shows_the_scores()
    {
        var (room, ani, _, _) = Started();
        room.Act(ani.Id, new EndGame(ani.Id));
        Assert.Equal(RoomStage.Ended, room.Stage);
        Assert.Equal(GuessStage.Ended, room.ViewFor(ani.Id).Guess!.Stage);
    }
}
