using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mingle.API.Ai;
using Mingle.Engine;

namespace Mingle.API.Controllers;

/// <summary>Fill-in stories: written by the AI when it can, otherwise a ready-made one in the same language.</summary>
[ApiController]
[Route("api/stories")]
public sealed class StoriesController(IStoryWriter writer, StoryLibrary library, ILogger<StoriesController> log) : ControllerBase
{
    /// <param name="Theme">The players' own words, if they typed any.</param>
    /// <param name="Topic">A theme chip (travel, love, work, family, fairy) or "random"; picks a premise from that group.</param>
    /// <param name="Length">"short" (about 6 gaps) or "long" (about 10).</param>
    public sealed record StoryRequestBody(string? Lang, string? Theme, string? Length, string[]? Names, string? Topic = null);

    public sealed record StoryAnswer(string Title, string Text, bool FromLibrary);

    [HttpPost]
    [EnableRateLimiting(DecksController.WritePolicy)]
    public async Task<IActionResult> Write(StoryRequestBody body, CancellationToken ct)
    {
        if (!Langs.TryParse(body.Lang, out var lang)) return BadRequest(new { error = "Unknown language." });
        var theme = body.Theme?.Trim();
        if (theme is { Length: > CardRequest.MaxThemeLength }) return BadRequest(new { error = "The theme is too long." });
        var names = (body.Names ?? []).Select(n => n.Trim()).Where(n => n.Length is > 0 and <= 24).Take(8).ToList();
        var premise = string.IsNullOrWhiteSpace(theme) ? StoryPremises.Pick(body.Topic) : null;
        var request = new StoryRequest(lang, theme, body.Length == "long" ? 10 : 6, names, premise);

        if (writer.IsAvailable)
        {
            try
            {
                var written = await writer.WriteAsync(request, ct);
                return Ok(new StoryAnswer(written.Title, written.Text, false));
            }
            catch (CardWriterException ex)
            {
                log.LogWarning("Using a ready-made story: {Reason}", ex.Message);
            }
        }

        return library.Random(lang) is { } ready
            ? Ok(new StoryAnswer(ready.Title, ready.Text, true))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No story is available." });
    }
}
