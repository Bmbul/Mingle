using System.Collections.Immutable;
using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class TopicDealTests
{
    private static readonly ImmutableArray<TopicInfo> Pool =
        [.. Enumerable.Range(1, 25).Select(i => new TopicInfo($"t{i:00}", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, $"Topic {i}")))];

    private static readonly Lang[] Everyone = [Lang.En, Lang.Hy, Lang.Ru];

    [Fact]
    public void Offers_ten_different_topics_the_same_way_for_the_same_seed()
    {
        var offer = TopicDeal.Offer(Pool, seed: 3);

        Assert.Equal(10, offer.Length);
        Assert.Equal(10, offer.Distinct().Count());
        Assert.Equal(offer.ToArray(), TopicDeal.Offer(Pool, seed: 3).ToArray());
    }

    [Fact]
    public void A_new_offer_avoids_the_previous_one_while_it_can()
    {
        var first = TopicDeal.Offer(Pool, seed: 3);
        var second = TopicDeal.Offer(Pool, seed: 4, avoid: first);
        var third = TopicDeal.Offer(Pool, seed: 5, avoid: [.. first, .. second]);

        Assert.Empty(second.Intersect(first));
        Assert.Equal(10, third.Length); // only 5 fresh topics are left, so 5 come back
        Assert.Equal(5, third.Intersect([.. first, .. second]).Count());
    }

    [Fact]
    public void Library_fallback_prefers_cards_on_the_picked_topics()
    {
        Card Tagged(string id, params string[] topics) => Card(id, CardKinds.OpenQuestion) with { Topics = [.. topics] };
        var deck = Deck([.. Enumerable.Range(1, 30).Select(i => Tagged($"other-{i}", "food")),
                         Tagged("travel-1", "travel"), Tagged("travel-2", "travel", "dreams"), Tagged("dreams-1", "dreams")]);

        var cards = TopicDeal.FromLibrary([deck], ["travel", "dreams"], Everyone, seed: 1, count: 5).Cards;

        Assert.Equal(5, cards.Length);
        Assert.Equal("travel-2", cards[0].Id); // on both picked topics
        Assert.Equal(["dreams-1", "travel-1"], cards.Skip(1).Take(2).Select(c => c.Id).Order());
    }

    [Fact]
    public void Library_fallback_skips_cards_someone_cannot_read()
    {
        var deck = Deck(Card("en-only", CardKinds.OpenQuestion, 1, Lang.En), Card("all", CardKinds.OpenQuestion));

        var cards = TopicDeal.FromLibrary([deck], [], [Lang.En, Lang.Hy], seed: 1).Cards;

        Assert.Equal(["all"], cards.Select(c => c.Id));
    }

    [Fact]
    public void Library_fallback_prefers_the_chosen_depths()
    {
        var deck = Deck([.. Enumerable.Range(1, 10).Select(i => Card($"light-{i}", CardKinds.OpenQuestion, 1)),
                         .. Enumerable.Range(1, 3).Select(i => Card($"deep-{i}", CardKinds.OpenQuestion, 3))]);

        var cards = TopicDeal.FromLibrary([deck], [], Everyone, seed: 1, count: 5, depths: [3]).Cards;

        Assert.Equal(3, cards.Count(c => c.Depth == 3)); // all deep cards first, then light ones fill up
        Assert.Equal(5, cards.Length);
    }
}
