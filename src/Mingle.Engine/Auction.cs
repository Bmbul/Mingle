using System.Collections.Immutable;

namespace Mingle.Engine;

public enum AuctionStage { Bidding, Revealed, Ended }

/// <param name="Lots">How many life things go under the hammer.</param>
/// <param name="Coins">What everyone starts with.</param>
public sealed record AuctionSettings(int Lots = 10, int Coins = 100);

public sealed record AuctionBid(string PlayerId, int Amount);

/// <summary>A lot as one player won it (or wanted it most), with what they paid (or bid).</summary>
public sealed record AuctionWin(string Lot, int Price);

/// <param name="WinnerIds">The highest bidders; more than one when they tied, and then they share it and each pays.
/// Empty when nobody bid.</param>
public sealed record AuctionOutcome(ImmutableArray<AuctionBid> Bids, ImmutableArray<string> WinnerIds, int Price);

/// <param name="Favourite">At the end: the lot this player bid the most on, won or not.</param>
public sealed record AuctionPurse(string PlayerId, int Coins, int Spent, ImmutableArray<AuctionWin> Won, AuctionWin? Favourite);

/// <param name="MostWanted">At the end: the lot with the most coins bid on it in all, with that total.</param>
public sealed record AuctionView(
    AuctionStage Stage,
    int Number,
    int Count,
    string? Lot,
    int MyCoins,
    int? MyBid,
    ImmutableArray<string> BidIds,
    AuctionOutcome? Outcome,
    ImmutableArray<AuctionPurse> Purses,
    AuctionWin? MostWanted);

public sealed class AuctionRuleException(string message) : Exception(message);

/// <summary>
/// "Life auction", online: everyone starts with 100 coins and bids in secret on life things ("A house by the sea",
/// "Never doing dishes again"). The highest bid wins the lot and pays; a tie at the top shares it and each pays; a
/// lot nobody bids on stays unsold. At the end everyone sees what each person spent the most on.
/// Not thread-safe: a room calls it under its lock.
/// </summary>
public sealed class AuctionGame
{
    public const int MinPlayers = 2;

    private sealed record Sale(int Lot, ImmutableDictionary<string, int> Bids, ImmutableArray<string> WinnerIds, int Price);

    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> cards;
    private readonly int startCoins;
    private readonly Dictionary<string, int> coins;
    private readonly Dictionary<string, int> bids = [];
    private readonly List<Sale> sales = [];

    public AuctionStage Stage { get; private set; } = AuctionStage.Bidding;
    public int Index { get; private set; }
    public int Count => cards.Length;

    public AuctionGame(IEnumerable<Player> players, IEnumerable<Card> pool, AuctionSettings settings, int seed)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new AuctionRuleException($"At least {MinPlayers} players are needed.");
        var random = new Random(seed);
        cards = [.. pool.Where(c => c.KindId == CardKinds.AuctionLot.Id).DistinctBy(c => c.Id)
            .OrderBy(_ => random.Next()).Take(Math.Clamp(settings.Lots, 1, 30))];
        if (cards.IsEmpty) throw new AuctionRuleException("The chosen decks have no lots for this game.");
        startCoins = Math.Clamp(settings.Coins, 10, 1000);
        coins = this.players.ToDictionary(p => p.Id, _ => startCoins);
        OpenLot();
    }

    public int CoinsOf(string playerId) => coins[playerId];

    public void Bid(string playerId, int amount)
    {
        RequireStage(AuctionStage.Bidding);
        if (!coins.TryGetValue(playerId, out var has)) throw new AuctionRuleException("You are not in this game.");
        if (amount < 0) throw new AuctionRuleException("A bid can't be negative.");
        if (amount > has) throw new AuctionRuleException("You don't have that many coins.");
        bids[playerId] = amount;
        if (players.All(p => bids.ContainsKey(p.Id))) Reveal();
    }

    /// <summary>The hammer falls; anyone who has not bid yet passes (the host may do it before everyone has bid).</summary>
    public void Reveal()
    {
        RequireStage(AuctionStage.Bidding);
        var all = players.ToImmutableDictionary(p => p.Id, p => bids.GetValueOrDefault(p.Id));
        var top = all.Values.Max();
        ImmutableArray<string> winners = top == 0 ? [] : [.. players.Where(p => all[p.Id] == top).Select(p => p.Id)];
        foreach (var id in winners) coins[id] -= top;
        sales.Add(new Sale(Index, all, winners, top));
        Stage = AuctionStage.Revealed;
    }

    public void Next()
    {
        if (Stage == AuctionStage.Ended) throw new AuctionRuleException("The auction is over.");
        if (Stage == AuctionStage.Bidding) Reveal();
        Index++;
        if (Index >= cards.Length)
        {
            Stage = AuctionStage.Ended;
            return;
        }
        Stage = AuctionStage.Bidding;
        OpenLot();
    }

    public void End() => Stage = AuctionStage.Ended;

    /// <summary>A new lot: whoever has no coins left passes at once, so nobody waits for them.</summary>
    private void OpenLot()
    {
        bids.Clear();
        foreach (var p in players.Where(p => coins[p.Id] == 0)) bids[p.Id] = 0;
        if (players.All(p => bids.ContainsKey(p.Id))) Reveal();
    }

    private string LotText(int lot, Lang lang) => cards[lot].TextFor(lang).Text;

    public ImmutableArray<AuctionPurse> Purses(Lang lang, bool favourites) =>
        [.. players.Select(p =>
        {
            var won = sales.Where(s => s.WinnerIds.Contains(p.Id)).Select(s => new AuctionWin(LotText(s.Lot, lang), s.Price)).ToImmutableArray();
            var best = sales.Where(s => s.Bids[p.Id] > 0).OrderByDescending(s => s.Bids[p.Id]).FirstOrDefault();
            return new AuctionPurse(p.Id, coins[p.Id], startCoins - coins[p.Id], won,
                favourites && best is not null ? new AuctionWin(LotText(best.Lot, lang), best.Bids[p.Id]) : null);
        }).OrderByDescending(p => p.Won.Length).ThenByDescending(p => p.Coins)];

    public AuctionWin? MostWanted(Lang lang)
    {
        var best = sales.Select(s => (s.Lot, Total: s.Bids.Values.Sum())).Where(s => s.Total > 0).OrderByDescending(s => s.Total).FirstOrDefault();
        return best.Total > 0 ? new AuctionWin(LotText(best.Lot, lang), best.Total) : null;
    }

    public AuctionView ForPlayer(string playerId, Lang lang)
    {
        var sale = Stage == AuctionStage.Revealed ? sales[^1] : null;
        var ended = Stage == AuctionStage.Ended;
        return new(
            Stage,
            Math.Min(Index + 1, cards.Length),
            cards.Length,
            ended ? null : LotText(Index, lang),
            coins.GetValueOrDefault(playerId),
            bids.TryGetValue(playerId, out var mine) ? mine : null,
            [.. players.Where(p => bids.ContainsKey(p.Id)).Select(p => p.Id)],
            sale is null ? null : new AuctionOutcome(
                [.. players.Select(p => new AuctionBid(p.Id, sale.Bids[p.Id])).OrderByDescending(b => b.Amount)], sale.WinnerIds, sale.Price),
            Purses(lang, ended),
            ended ? MostWanted(lang) : null);
    }

    private void RequireStage(AuctionStage stage)
    {
        if (Stage != stage) throw new AuctionRuleException(stage == AuctionStage.Bidding ? "The hammer has already fallen." : "Not now.");
    }
}
