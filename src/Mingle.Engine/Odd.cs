using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>Answering: everyone has their question and answers. Voting: who had the different one? Revealed: the answer.</summary>
public enum OddStage { Answering, Voting, Revealed, Ended }

/// <param name="Rounds">How many question pairs the game plays.</param>
/// <param name="TwoOdd">Two players get the different question each round (for bigger groups).</param>
public sealed record OddSettings(int Rounds = 5, bool TwoOdd = false);

/// <param name="Points">A point for each vote that found a different-question player, two for escaping the vote.</param>
/// <param name="Escaped">Rounds this player had the different question and the group did not catch them.</param>
public sealed record OddScore(string PlayerId, int Points, int Found, int Escaped);

public sealed record OddAnswer(string PlayerId, string Text);

public sealed record OddVote(string VoterId, string ForId);

/// <summary>Everything one player's screen needs. A player never learns their question was the different one until the reveal.</summary>
/// <param name="MyQuestion">This player's question, in their language.</param>
/// <param name="Answers">Online, once everyone has answered: every answer with its author, questions still hidden.</param>
/// <param name="MainQuestion">After the reveal: the question most players had.</param>
/// <param name="OddQuestion">After the reveal: the different question.</param>
/// <param name="OddIds">After the reveal: who had the different question.</param>
/// <param name="Caught">After the reveal: true when the group's top vote found a different-question player.</param>
public sealed record OddView(
    OddStage Stage,
    int Round,
    int Rounds,
    int OddCount,
    string? MyQuestion,
    bool YouAnswered,
    ImmutableArray<string> AnsweredIds,
    ImmutableArray<OddAnswer> Answers,
    ImmutableArray<string> VotedIds,
    string? MyVote,
    string? MainQuestion,
    string? OddQuestion,
    ImmutableArray<string> OddIds,
    ImmutableArray<OddVote> Votes,
    bool Caught,
    ImmutableArray<OddScore> Scores);

public sealed class OddRuleException(string message) : Exception(message);

/// <summary>
/// "Who got a different question?": every round everyone secretly gets the same question, except one player (two in
/// big groups) who gets a similar but different one, without knowing it. Everyone answers, then votes for who had the
/// different question. Online everyone types and votes on their own phone, with points; on one phone
/// (<c>votes: false</c>) answers are said out loud and the group picks who to accuse. Not thread-safe.
/// </summary>
public sealed class OddGame
{
    public const int MinPlayers = 3;
    /// <summary>Two different-question players need a group where they are still the few.</summary>
    public const int MinPlayersForTwo = 6;
    public const int MaxAnswerLength = 120;

    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> cards;
    private readonly Random random;
    private readonly Dictionary<string, string> answers = [];
    private readonly Dictionary<string, string> votes = [];
    private readonly Dictionary<string, int> found;
    private readonly Dictionary<string, int> escaped;
    private ImmutableArray<string> oddIds = [];
    private ImmutableArray<string> accused = [];

    public OddSettings Settings { get; }
    public bool Votes { get; }
    public OddStage Stage { get; private set; } = OddStage.Answering;
    public int Index { get; private set; }
    public int Count => cards.Length;
    public ImmutableArray<Player> Players => players;
    /// <summary>How many players get the different question each round.</summary>
    public int OddCount => Settings.TwoOdd && players.Length >= MinPlayersForTwo ? 2 : 1;

    public OddGame(IEnumerable<Player> players, IEnumerable<Card> pool, OddSettings settings, int seed, bool votes = true)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new OddRuleException($"At least {MinPlayers} players are needed.");
        random = new Random(seed);
        cards = [.. pool.Where(c => c.KindId == CardKinds.OddPair.Id).DistinctBy(c => c.Id)
            .OrderBy(_ => random.Next()).Take(Math.Max(1, settings.Rounds))];
        if (cards.IsEmpty) throw new OddRuleException("The chosen decks have no question pairs.");
        Settings = settings;
        Votes = votes;
        found = this.players.ToDictionary(p => p.Id, _ => 0);
        escaped = this.players.ToDictionary(p => p.Id, _ => 0);
        Deal();
    }

    private void Deal()
    {
        oddIds = [.. players.OrderBy(_ => random.Next()).Take(OddCount).Select(p => p.Id)];
        answers.Clear();
        votes.Clear();
        accused = [];
    }

    public Card? Card => Stage == OddStage.Ended ? null : cards[Index];

    public bool IsOdd(string playerId) => oddIds.Contains(playerId);

    public ImmutableArray<string> OddIds => oddIds;

    /// <summary>The question this player reads: the card's text, or its option for the different-question players.</summary>
    public string? QuestionFor(string playerId, Lang lang)
    {
        if (Card is not { } card) return null;
        var text = card.TextFor(lang);
        return IsOdd(playerId) && !text.Options.IsEmpty ? text.Options[0] : text.Text;
    }

    public string? MainQuestion(Lang lang) => Card?.TextFor(lang).Text;

    public string? OddQuestion(Lang lang) => Card?.TextFor(lang).Options.FirstOrDefault();

    /// <summary>Who answers first out loud on one phone, so it is not always the same player.</summary>
    public Player FirstToAnswer => players[(Index + 1) * 7 % players.Length];

    /// <summary>Online: a player's answer. When everyone has answered, the voting starts.</summary>
    public void Answer(string playerId, string text)
    {
        RequireStage(OddStage.Answering);
        if (!Votes) throw new OddRuleException("Answers are said out loud in this game.");
        Require(playerId);
        var clean = (text ?? "").Trim();
        if (clean.Length == 0) throw new OddRuleException("Write your answer.");
        answers[playerId] = clean.Length > MaxAnswerLength ? clean[..MaxAnswerLength] : clean;
        if (players.All(p => answers.ContainsKey(p.Id))) Stage = OddStage.Voting;
    }

    /// <summary>On to the vote (the host online, without waiting for a missing answer; on one phone after the answers).</summary>
    public void StartVoting()
    {
        RequireStage(OddStage.Answering);
        Stage = OddStage.Voting;
    }

    /// <summary>Online: one vote each, for someone else. When everyone has voted, the answer comes out.</summary>
    public void Vote(string playerId, string forId)
    {
        RequireStage(OddStage.Voting);
        if (!Votes) throw new OddRuleException("The group picks together in this game.");
        Require(playerId);
        if (forId == playerId || players.All(p => p.Id != forId)) throw new OddRuleException("Vote for someone else.");
        votes[playerId] = forId;
        if (players.All(p => votes.ContainsKey(p.Id))) Reveal();
    }

    /// <summary>On one phone: the players the group accuses (as many as there are different-question players).</summary>
    public void Accuse(IReadOnlyCollection<string> playerIds)
    {
        RequireStage(OddStage.Voting);
        if (playerIds.Count == 0 || playerIds.Count > OddCount || playerIds.Any(id => players.All(p => p.Id != id)))
            throw new OddRuleException($"Pick up to {OddCount}.");
        accused = [.. playerIds.Distinct()];
        Reveal();
    }

    /// <summary>The answer comes out (the host online may do it before everyone has voted).</summary>
    public void Reveal()
    {
        RequireStage(OddStage.Voting);
        if (Votes)
        {
            // The group's pick: the most-voted players, as many as there are different-question players.
            accused = [.. votes.Values.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Take(OddCount).Select(g => g.Key)];
            foreach (var (voter, forId) in votes)
                if (!IsOdd(voter) && IsOdd(forId)) found[voter]++;
        }
        foreach (var id in oddIds.Where(id => !accused.Contains(id))) escaped[id]++;
        Stage = OddStage.Revealed;
    }

    /// <summary>True when the group's pick found a different-question player.</summary>
    public bool Caught => accused.Any(IsOdd);

    public void Next()
    {
        if (Stage == OddStage.Ended) throw new OddRuleException("The game is over.");
        if (Stage != OddStage.Revealed) throw new OddRuleException("Reveal the answer first.");
        Index++;
        if (Index < cards.Length)
        {
            Deal();
            Stage = OddStage.Answering;
        }
        else
        {
            Stage = OddStage.Ended;
        }
    }

    public void End() => Stage = OddStage.Ended;

    public ImmutableArray<OddScore> Scores =>
        [.. players.Select(p => new OddScore(p.Id, found[p.Id] + 2 * escaped[p.Id], found[p.Id], escaped[p.Id])).OrderByDescending(s => s.Points)];

    public ImmutableArray<string> Accused => accused;

    public OddView ForPlayer(string playerId, Lang lang)
    {
        var revealed = Stage == OddStage.Revealed;
        var ended = Stage == OddStage.Ended;
        return new OddView(
            Stage,
            Math.Min(Index + 1, cards.Length),
            cards.Length,
            OddCount,
            ended ? null : QuestionFor(playerId, lang),
            answers.ContainsKey(playerId),
            [.. players.Where(p => answers.ContainsKey(p.Id)).Select(p => p.Id)],
            Stage is OddStage.Voting or OddStage.Revealed ? [.. players.Where(p => answers.ContainsKey(p.Id)).Select(p => new OddAnswer(p.Id, answers[p.Id]))] : [],
            [.. players.Where(p => votes.ContainsKey(p.Id)).Select(p => p.Id)],
            votes.GetValueOrDefault(playerId),
            revealed ? MainQuestion(lang) : null,
            revealed ? OddQuestion(lang) : null,
            revealed ? oddIds : [],
            revealed ? [.. votes.Select(v => new OddVote(v.Key, v.Value))] : [],
            revealed && Caught,
            Votes && (revealed || ended) ? Scores : []);
    }

    private void RequireStage(OddStage stage)
    {
        if (Stage != stage) throw new OddRuleException(stage switch
        {
            OddStage.Answering => "The answers are already in.",
            OddStage.Voting => "It is not time to vote.",
            _ => "Not now.",
        });
    }

    private void Require(string playerId)
    {
        if (players.All(p => p.Id != playerId)) throw new OddRuleException("You are not in this game.");
    }
}
