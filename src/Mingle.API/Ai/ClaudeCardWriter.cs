using System.Collections.Immutable;
using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>Writes cards with Claude: one structured call returns every card in every language at the table.</summary>
public sealed class ClaudeCardWriter(AnthropicClient client, IOptions<AiOptions> options, DailyLimit limit, ILogger<ClaudeCardWriter> log) : ICardWriter
{
    public bool IsAvailable => true;

    public async Task<ImmutableArray<Card>> WriteAsync(CardRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        if (!limit.TryTake(settings.MaxGenerationsPerDay))
            throw new CardWriterException("Today's AI limit is used up.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        var parameters = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = settings.MaxTokens,
            System = CardPrompt.System,
            OutputConfig = new OutputConfig
            {
                Effort = settings.Effort.ToLowerInvariant() switch
                {
                    "medium" => Effort.Medium,
                    "high" => Effort.High,
                    _ => Effort.Low,
                },
                Format = new JsonOutputFormat { Schema = CardPrompt.Schema(request) },
            },
            Messages = [new UserMessageParam { Content = CardPrompt.UserMessage(request) }],
        };

        var json = new StringBuilder();
        var started = DateTimeOffset.UtcNow;
        try
        {
            // Streaming keeps the connection busy while a long answer is written.
            await foreach (var streamEvent in client.Messages.CreateStreaming(parameters, timeout.Token))
            {
                if (streamEvent.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                    json.Append(text.Text);
            }
        }
        catch (Exception ex) when (ex is AnthropicApiException or HttpRequestException or OperationCanceledException or IOException)
        {
            log.LogWarning(ex, "Writing cards failed after {Seconds:0}s", (DateTimeOffset.UtcNow - started).TotalSeconds);
            throw new CardWriterException("The AI could not write cards.", ex);
        }

        var cards = CardParser.Parse(json.ToString(), request);
        log.LogInformation("Wrote {Count} of {Asked} cards on {Topics} in {Seconds:0}s",
            cards.Length, request.Count, string.Join(",", request.Topics.Select(t => t.Code)), (DateTimeOffset.UtcNow - started).TotalSeconds);
        if (cards.IsEmpty) throw new CardWriterException("The AI answer had no usable cards.");
        return cards;
    }
}
