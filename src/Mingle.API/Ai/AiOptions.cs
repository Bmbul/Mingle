namespace Mingle.API.Ai;

/// <summary>
/// How cards are written with AI. Bound from the "Ai" configuration section; the API key comes
/// from <c>Ai:ApiKey</c> or the <c>ANTHROPIC_API_KEY</c> environment variable and is never logged.
/// Without a key the "Our topics" game uses library cards on the picked topics instead.
/// </summary>
public sealed class AiOptions
{
    public const string Section = "Ai";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-5-5"; // cheapest for now; Ai__Model switches it without a code change

    /// <summary>
    /// Model for fill-in stories: they need humor, a story arc and careful Armenian and Russian grammar, where
    /// Opus was clearly best in side-by-side tests (about $0.04 a story). Empty uses <see cref="Model"/>.
    /// Setting: Ai__StoryModel.
    /// </summary>
    public string? StoryModel { get; set; } = "claude-opus-5-5";

    /// <summary>low, medium or high. Lower is faster, which matters while a room waits.</summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 16000;
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Generations allowed per day across the whole server; a simple cost cap until accounts exist.</summary>
    public int MaxGenerationsPerDay { get; set; } = 100;
}
