using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Mingle.Engine;

public enum MatchStage { Answering, Revealed, Ended }

/// <param name="Rounds">How many prompts the game plays.</param>
public sealed record MatchSettings(int Rounds = 8);

/// <param name="Points">A point for every other player who wrote the same thing, round after round.</param>
/// <param name="Unique">Rounds where nobody else wrote this player's answer.</param>
public sealed record MatchScore(string PlayerId, int Points, int Unique);

/// <summary>One answer as the group wrote it, with everyone who wrote it (or something the host counted as the same).</summary>
public sealed record MatchGroup(string Key, string Text, ImmutableArray<string> PlayerIds);

public sealed record MatchView(
    MatchStage Stage,
    int Round,
    int Rounds,
    string? Prompt,
    bool YouAnswered,
    string? MyAnswer,
    ImmutableArray<string> AnsweredIds,
    ImmutableArray<MatchGroup> Groups,
    ImmutableArray<MatchScore> Scores);

public sealed class MatchRuleException(string message) : Exception(message);

/// <summary>
/// "Same wavelength", online only: everyone answers the same prompt ("A red fruit") and scores a point for every other
/// player who wrote the same thing, so the aim is to think like the group. Answers are matched ignoring case, spaces
/// and punctuation; the host can join two answers that mean the same. Not thread-safe: a room calls it under its lock.
/// </summary>
public sealed class MatchGame
{
    public const int MinPlayers = 3;
    public const int MaxAnswerLength = 60;

    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> cards;
    private readonly Dictionary<string, string> answers = [];
    /// <summary>Answer keys the host joined to another key this round.</summary>
    private readonly Dictionary<string, string> joined = [];
    private readonly Dictionary<string, int> points;
    private readonly Dictionary<string, int> unique;

    public MatchStage Stage { get; private set; } = MatchStage.Answering;
    public int Index { get; private set; }
    public int Count => cards.Length;

    public MatchGame(IEnumerable<Player> players, IEnumerable<Card> pool, MatchSettings settings, int seed)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new MatchRuleException($"At least {MinPlayers} players are needed.");
        var random = new Random(seed);
        cards = [.. pool.Where(c => c.KindId == CardKinds.MatchPrompt.Id).DistinctBy(c => c.Id)
            .OrderBy(_ => random.Next()).Take(Math.Max(1, settings.Rounds))];
        if (cards.IsEmpty) throw new MatchRuleException("The chosen decks have no prompts for this game.");
        points = this.players.ToDictionary(p => p.Id, _ => 0);
        unique = this.players.ToDictionary(p => p.Id, _ => 0);
    }

    public string? Prompt(Lang lang) => Stage == MatchStage.Ended ? null : cards[Index].TextFor(lang).Text;

    /// <summary>
    /// The form two answers are compared in: lower case, no accents or punctuation, single spaces, «ё» as «е», and an
    /// Armenian article left off the end («խնձորը» matches «խնձոր»).
    /// </summary>
    public static string Key(string text)
    {
        var lower = text.Trim().ToLowerInvariant().Replace('ё', 'е').Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder();
        foreach (var ch in lower)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            kept.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        var words = kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 3 && w.EndsWith('ը') ? w[..^1] : w);
        return string.Join(' ', words).Normalize(NormalizationForm.FormC);
    }

    public void Answer(string playerId, string text)
    {
        RequireStage(MatchStage.Answering);
        if (players.All(p => p.Id != playerId)) throw new MatchRuleException("You are not in this game.");
        var clean = (text ?? "").Trim();
        if (Key(clean).Length == 0) throw new MatchRuleException("Write your answer.");
        answers[playerId] = clean.Length > MaxAnswerLength ? clean[..MaxAnswerLength] : clean;
        if (players.All(p => answers.ContainsKey(p.Id))) Reveal();
    }

    /// <summary>The answers come out (the host may do it before everyone has written).</summary>
    public void Reveal()
    {
        RequireStage(MatchStage.Answering);
        Stage = MatchStage.Revealed;
    }

    private string GroupOf(string key)
    {
        while (joined.TryGetValue(key, out var into)) key = into;
        return key;
    }

    /// <summary>The host counts two answers as the same ("apple" and "a green apple").</summary>
    public void Join(string key, string intoKey)
    {
        RequireStage(MatchStage.Revealed);
        var from = GroupOf(key);
        var into = GroupOf(intoKey);
        if (from == into) return;
        if (Groups.All(g => g.Key != from) || Groups.All(g => g.Key != into)) throw new MatchRuleException("Pick two answers.");
        joined[from] = into;
    }

    /// <summary>This round's answers, grouped, biggest group first.</summary>
    public ImmutableArray<MatchGroup> Groups =>
        [.. players.Where(p => answers.ContainsKey(p.Id))
            .GroupBy(p => GroupOf(Key(answers[p.Id])))
            .Select(g => new MatchGroup(g.Key, answers[g.First().Id], [.. g.Select(p => p.Id)]))
            .OrderByDescending(g => g.PlayerIds.Length).ThenBy(g => g.Text, StringComparer.CurrentCulture)];

    public void Next()
    {
        if (Stage == MatchStage.Ended) throw new MatchRuleException("The game is over.");
        if (Stage == MatchStage.Answering) Reveal();
        Score();
        Index++;
        answers.Clear();
        joined.Clear();
        Stage = Index < cards.Length ? MatchStage.Answering : MatchStage.Ended;
    }

    public void End()
    {
        if (Stage == MatchStage.Revealed) Score();
        Stage = MatchStage.Ended;
    }

    private void Score()
    {
        foreach (var group in Groups)
            foreach (var id in group.PlayerIds)
            {
                points[id] += group.PlayerIds.Length - 1;
                if (group.PlayerIds.Length == 1) unique[id]++;
            }
    }

    /// <summary>Points so far, with this round's counted once its answers are out.</summary>
    public ImmutableArray<MatchScore> Scores
    {
        get
        {
            var now = players.ToDictionary(p => p.Id, p => (Points: points[p.Id], Unique: unique[p.Id]));
            if (Stage == MatchStage.Revealed)
                foreach (var group in Groups)
                    foreach (var id in group.PlayerIds)
                        now[id] = (now[id].Points + group.PlayerIds.Length - 1, now[id].Unique + (group.PlayerIds.Length == 1 ? 1 : 0));
            return [.. players.Select(p => new MatchScore(p.Id, now[p.Id].Points, now[p.Id].Unique)).OrderByDescending(s => s.Points)];
        }
    }

    public MatchView ForPlayer(string playerId, Lang lang) => new(
        Stage,
        Math.Min(Index + 1, cards.Length),
        cards.Length,
        Prompt(lang),
        answers.ContainsKey(playerId),
        answers.GetValueOrDefault(playerId),
        [.. players.Where(p => answers.ContainsKey(p.Id)).Select(p => p.Id)],
        Stage == MatchStage.Revealed ? Groups : [],
        Stage == MatchStage.Answering && Index == 0 ? [] : Scores);

    private void RequireStage(MatchStage stage)
    {
        if (Stage != stage) throw new MatchRuleException(stage == MatchStage.Answering ? "The answers are already out." : "Not now.");
    }
}
