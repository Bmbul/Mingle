using System.Collections.Immutable;

namespace Mingle.Engine.Tests;

public class AuctionTests
{
    private static readonly Player[] Three = [.. Enumerable.Range(1, 3).Select(i => new Player($"p{i}", $"P{i}", Lang.En))];

    private static Card[] Lots(int n) => [.. Enumerable.Range(1, n).Select(i => new Card($"l{i}", CardKinds.AuctionLot.Id, 1, [],
        ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText($"Lot {i}"))))];

    [Fact]
    public void The_highest_bid_wins_and_pays_while_bids_stay_secret_until_the_hammer()
    {
        var game = new AuctionGame(Three, Lots(3), new AuctionSettings(3), seed: 1);
        game.Bid("p1", 40);
        game.Bid("p2", 10);
        Assert.Null(game.ForPlayer("p2", Lang.En).Outcome);
        game.Bid("p3", 0);

        Assert.Equal(AuctionStage.Revealed, game.Stage);
        var outcome = game.ForPlayer("p2", Lang.En).Outcome!;
        Assert.Equal<string>(["p1"], outcome.WinnerIds);
        Assert.Equal(40, outcome.Price);
        Assert.Equal(60, game.CoinsOf("p1"));
        Assert.Equal(100, game.CoinsOf("p2"));
    }

    [Fact]
    public void A_tie_at_the_top_shares_the_lot_and_both_pay()
    {
        var game = new AuctionGame(Three, Lots(3), new AuctionSettings(3), seed: 1);
        game.Bid("p1", 30);
        game.Bid("p2", 30);
        game.Bid("p3", 5);
        Assert.Equal<string>(["p1", "p2"], game.ForPlayer("p1", Lang.En).Outcome!.WinnerIds);
        Assert.Equal(70, game.CoinsOf("p1"));
        Assert.Equal(70, game.CoinsOf("p2"));
    }

    [Fact]
    public void A_lot_nobody_bids_on_stays_unsold()
    {
        var game = new AuctionGame(Three, Lots(3), new AuctionSettings(3), seed: 1);
        game.Bid("p1", 0);
        game.Reveal();
        Assert.Empty(game.ForPlayer("p1", Lang.En).Outcome!.WinnerIds);
    }

    [Fact]
    public void Nobody_can_bid_more_than_they_have_and_a_broke_player_passes_on_their_own()
    {
        var game = new AuctionGame(Three, Lots(3), new AuctionSettings(3), seed: 1);
        Assert.Throws<AuctionRuleException>(() => game.Bid("p1", 101));
        Assert.Throws<AuctionRuleException>(() => game.Bid("p1", -1));
        game.Bid("p1", 100);
        game.Bid("p2", 1);
        game.Bid("p3", 1);
        game.Next();
        Assert.Equal(0, game.CoinsOf("p1"));
        Assert.Contains("p1", game.ForPlayer("p2", Lang.En).BidIds);
    }

    [Fact]
    public void The_end_shows_what_each_heart_wanted_most_and_the_most_wanted_lot()
    {
        var game = new AuctionGame(Three, Lots(2), new AuctionSettings(2), seed: 1);
        var first = game.ForPlayer("p1", Lang.En).Lot;
        game.Bid("p1", 50);
        game.Bid("p2", 45);
        game.Bid("p3", 0);
        game.Next();
        game.Bid("p1", 0);
        game.Bid("p2", 20);
        game.Bid("p3", 0);
        game.Next();

        Assert.Equal(AuctionStage.Ended, game.Stage);
        var view = game.ForPlayer("p1", Lang.En);
        var purses = view.Purses.ToDictionary(p => p.PlayerId);
        Assert.Equal(first, purses["p2"].Favourite!.Lot); // lost it, but wanted it most
        Assert.Equal(45, purses["p2"].Favourite!.Price);
        Assert.Null(purses["p3"].Favourite);
        Assert.Equal(first, view.MostWanted!.Lot);
        Assert.Equal(95, view.MostWanted.Price);
        Assert.Single(purses["p1"].Won);
    }

    [Fact]
    public void Two_players_can_play_head_to_head()
    {
        Assert.Throws<AuctionRuleException>(() => new AuctionGame(Three[..1], Lots(3), new AuctionSettings(), seed: 1));
        var game = new AuctionGame(Three[..2], Lots(3), new AuctionSettings(3), seed: 1);
        game.Bid("p1", 20);
        game.Bid("p2", 35);
        Assert.Equal<string>(["p2"], game.ForPlayer("p1", Lang.En).Outcome!.WinnerIds);
    }
}
