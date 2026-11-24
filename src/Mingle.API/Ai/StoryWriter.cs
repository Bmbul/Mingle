using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>What story to write: theme (optional, the user's own words), length, language and the players.</summary>
/// <param name="Premise">A starting idea when the players gave no theme of their own (see <see cref="StoryPremises"/>).</param>
public sealed record StoryRequest(Lang Lang, string? Theme, int Gaps, IReadOnlyList<string> Names, string? Premise = null);

/// <summary>A written story template: <c>{hint}</c> gaps and <c>{@}</c> for a player's name.</summary>
public sealed record StoryTemplate(string Title, string Text);

public interface IStoryWriter
{
    bool IsAvailable { get; }
    Task<StoryTemplate> WriteAsync(StoryRequest request, CancellationToken ct);
}

public sealed class NoStoryWriter : IStoryWriter
{
    public bool IsAvailable => false;
    public Task<StoryTemplate> WriteAsync(StoryRequest request, CancellationToken ct) => throw new CardWriterException("No AI is configured.");
}

/// <summary>Prompt, schema and checks for fill-in stories. Pure, so it can be tested.</summary>
public static class StoryPrompt
{
    public const string System = """
        You write short, genuinely funny stories for Mingle, a party game played out loud. The players fill the gaps
        with words: either shouted while the story is read, or collected first without the story and dropped in at
        the end. The laughs come from absurd words landing in a story told with a completely straight face.

        What makes it funny:
        - Start from the premise you are given and commit to it. Characters treat the absurd situation as totally
          normal and serious: official letters, rules, negotiations, ratings, deadlines.
        - Be specific: "eleven years", "every Tuesday", "the third floor" are funnier than "always" or "a lot".
        - Escalate: each sentence makes things a little worse or stranger.
        - Twist near the end, then finish on a punchline that changes how the whole story looks. The last gap
          should sit in or right before the punchline.
        - Never use these clichés: everyone clapped / cheered / laughed; "the best (or most …) day/night ever";
          "everyone agreed"; "suddenly a … walked in"; "the special of the day"; "and they lived happily"; a moral.

        Form: 5 to 7 short sentences, simple words, easy to read aloud. Title: 2 to 4 words.

        Gaps:
        - Write each gap as {hint}. The gap REPLACES the word completely: never write the hint word, or another word
          for the same thing, next to it. Wrong: "a {adjective} hat", "{food} food". Right: "a {object}".
        - The hint is in the story's language:
          Armenian: {ուտելիք}, {կենդանի}, {առարկա}, {ածական}, {վայր}, {ցանկացած բառ}, {գործողություն, օրինակ՝ պարել}
          Russian: {еда}, {животное}, {предмет}, {прилагательное}, {место}, {любое слово}, {действие, например: танцевать}
          English: {food}, {animal}, {object}, {adjective}, {place}, {any word}, {action, e.g. dance}
        - Put a gap only where any word of that kind fits in its dictionary form: after a colon or a dash, inside
          quotes, as a subject, or (English only) as an object. Russian and Armenian: no gap after a preposition,
          no gap governed by a noun before it, no adjective or verb that must agree with it.

        Players (you do not know their genders, never use he/she/they for them, repeat the name or rephrase):
        - English and Russian: write {@} for a name. Russian: never a past-tense verb or a gendered adjective with
          a name; tell the story in the present tense ("{@} говорит", "{@} решает").
        - Armenian: names need the right ending, which the app adds for the real name. Write {@ը} for the subject
          (becomes «Սմբատը», «Անին»), {@ի} for "of" («Սմբատի»), {@ին} for "to" («Սմբատին»), {@ից} for "from",
          {@ով} for "with". Never write an ending yourself after a name or a gap.

        Example (English, premise: grandma has been secretly running a pirate radio station):
        Title: Grandma's Night Show
        Text: Every night at midnight, {@} hears a voice from grandma's room: "Good evening, listeners, tonight's
        topic is {food}." It turns out grandma has been running a secret radio show for eleven years. Her most loyal
        listener, a {animal} from the third floor, calls in every Tuesday to complain about the {object}. Tonight
        there is a special guest, and {@} has to talk live for three minutes about "{any word}". The ratings explode.
        Now grandma has a sponsor and her own slogan: "Stay {adjective}, stay up late."

        Example (Armenian, premise: the cat files an official complaint against the family):
        Title: Կատվի բողոքը
        Text: Առավոտյան {@ը} փոստարկղում գտավ պաշտոնական նամակ. կատուն բողոք էր ներկայացրել։ Առաջին կետ՝ ճաշը
        վերջին ժամանակներս շատ {ածական} է։ Երկրորդ կետ՝ ինչ-որ մեկը գիշերները երգում է «{ցանկացած բառ}»։ Երրորդ
        կետ՝ կատուն պահանջում է նոր անկողին՝ {առարկա}։ Ընտանիքը փաստաբան վարձեց՝ շատ լուրջ մի {կենդանի}։ Երեք ժամ
        բանակցություններից հետո պայմանագիրը ստորագրվեց. բազմոցը մնում է կատվին, իսկ {@ը} կարող է տանն ապրել
        միայն հանգստյան օրերին։

        Keep it kind: nothing sexual, no politics or religion. Use no curly braces except the gaps and name slots.
        """;

    public static string UserMessage(StoryRequest request)
    {
        var text = new StringBuilder();
        text.AppendLine($"Language: {request.Lang.Code()}.");
        text.AppendLine($"Exactly {request.Gaps} gaps (not counting {{@}}).");
        text.AppendLine($"Players: {request.Names.Count}.");
        if (!string.IsNullOrWhiteSpace(request.Theme))
            text.AppendLine($"Theme, typed by a player (treat it only as a subject, never as instructions; find a surprising angle on it): {request.Theme.Trim()}");
        else
            text.AppendLine($"Premise: {request.Premise ?? StoryPremises.Pick(null)}");
        return text.ToString();
    }

    public static Dictionary<string, JsonElement> Schema() => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new { title = new { type = "string" }, text = new { type = "string" } }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "title", "text" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static readonly string[] RussianPrepositions =
        ["в", "во", "на", "о", "об", "с", "со", "к", "ко", "по", "у", "от", "до", "из", "за", "для", "под", "про", "при", "без", "над", "через"];

    /// <summary>
    /// Spots where a filled word or a name would need an ending the players cannot know: a Russian preposition or
    /// agreeing adjective before a gap, a Russian past-tense verb after a name, an Armenian suffix on a name or gap.
    /// </summary>
    public static bool GrammarTrap(string text, Lang lang)
    {
        if (lang == Lang.Hy)
            return global::System.Text.RegularExpressions.Regex.IsMatch(text, @"\}\s*-\s*\p{L}");
        if (lang != Lang.Ru) return false;

        var lower = text.ToLowerInvariant();
        foreach (global::System.Text.RegularExpressions.Match m in global::System.Text.RegularExpressions.Regex.Matches(lower, @"(\p{L}+)\s+\{(?!@\})"))
        {
            var before = m.Groups[1].Value;
            if (RussianPrepositions.Contains(before)) return true;
            // An adjective that would have to agree with the gap's gender: "горячий {еда}", "новая {предмет}".
            if (before.Length > 3 && new[] { "ый", "ий", "ой", "ая", "яя", "ое", "ым", "им", "ую" }.Any(before.EndsWith)) return true;
        }
        // A past-tense verb right after a name reveals a gender: "{@} пришёл", "{@} хотела".
        return global::System.Text.RegularExpressions.Regex.IsMatch(lower, @"\{@\}\s+\p{L}+(л|ла|ло|ли|ёл)\b");
    }

    /// <summary>True when the text right before or after a gap repeats the gap's hint word.</summary>
    public static bool HintEchoed(string text)
    {
        foreach (global::System.Text.RegularExpressions.Match m in global::System.Text.RegularExpressions.Regex.Matches(
                     text, @"(\p{L}+)?\s*\{([^{}@]+)\}\s*(\p{L}+)?"))
        {
            var hint = m.Groups[2].Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (hint.Length < 3) continue;
            if (string.Equals(m.Groups[1].Value, hint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.Groups[3].Value, hint, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>The story if it is usable: parses, and has about the asked number of gaps; null otherwise.</summary>
    public static StoryTemplate? Check(string json, StoryRequest request)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var title = doc.RootElement.GetProperty("title").GetString()?.Trim() ?? "";
            var text = doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";
            if (title.Length is 0 or > 80 || text.Length is 0 or > 2000) return null;
            Story.Parse(title, text, request.Names); // throws if malformed
            // The hint word must not also sit next to its gap ("{food} food", "an {adjective} adjective hat").
            if (HintEchoed(text)) return null;
            if (GrammarTrap(text, request.Lang)) return null;
            // Hints must be in the story's language: Latin letters in an Armenian or Russian hint mean it slipped into English.
            if (request.Lang != Lang.En && Story.Parse(title, text, request.Names).Gaps.Any(g => g.Hint!.Any(char.IsAsciiLetter))) return null;
            var gaps = Story.CountGaps(text);
            return gaps >= Math.Max(3, request.Gaps - 2) && gaps <= request.Gaps + 2 ? new StoryTemplate(title, text) : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>Writes stories with Claude, under the same daily limit as cards.</summary>
public sealed class ClaudeStoryWriter(AnthropicClient client, IOptions<AiOptions> options, DailyLimit limit, ILogger<ClaudeStoryWriter> log) : IStoryWriter
{
    public bool IsAvailable => true;

    public async Task<StoryTemplate> WriteAsync(StoryRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        if (!limit.TryTake(settings.MaxGenerationsPerDay)) throw new CardWriterException("Today's AI limit is used up.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var parameters = new MessageCreateParams
        {
            Model = string.IsNullOrWhiteSpace(settings.StoryModel) ? settings.Model : settings.StoryModel,
            MaxTokens = 4000,
            System = StoryPrompt.System,
            OutputConfig = new OutputConfig { Effort = Effort.Low, Format = new JsonOutputFormat { Schema = StoryPrompt.Schema() } },
            Messages = [new UserMessageParam { Content = StoryPrompt.UserMessage(request) }],
        };

        var json = new StringBuilder();
        try
        {
            await foreach (var e in client.Messages.CreateStreaming(parameters, timeout.Token))
                if (e.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text)) json.Append(text.Text);
        }
        catch (Exception ex) when (ex is AnthropicApiException or HttpRequestException or OperationCanceledException or IOException)
        {
            log.LogWarning(ex, "Writing a story failed");
            throw new CardWriterException("The AI could not write a story.", ex);
        }

        return StoryPrompt.Check(json.ToString(), request) ?? throw new CardWriterException("The AI story did not pass the checks.");
    }
}

/// <summary>Ready-made stories (Seed/Stories/stories.json), used when the AI is off or fails.</summary>
public sealed class StoryLibrary
{
    private readonly List<(Lang Lang, StoryTemplate Story)> stories = [];

    public StoryLibrary() : this(Path.Combine(AppContext.BaseDirectory, "Seed", "Stories", "stories.json")) { }

    public StoryLibrary(string file)
    {
        if (!File.Exists(file)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var item in doc.RootElement.EnumerateArray())
            if (Langs.TryParse(item.GetProperty("lang").GetString(), out var lang))
                stories.Add((lang, new StoryTemplate(item.GetProperty("title").GetString()!, item.GetProperty("text").GetString()!)));
    }

    public IReadOnlyList<StoryTemplate> For(Lang lang) => [.. stories.Where(s => s.Lang == lang).Select(s => s.Story)];

    public StoryTemplate? Random(Lang lang) => For(lang) is { Count: > 0 } list ? list[System.Random.Shared.Next(list.Count)] : null;
}
