using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mingle.API.Ai;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.API.Controllers;

/// <summary>
/// Fill-in stories: ready-made ones from the library, checked by hand. The AI writes one only when the players
/// typed their own theme, which no library story can cover.
/// </summary>
[ApiController]
[Route("api/stories")]
public sealed class StoriesController(IStoryWriter writer, IStorySource library, ILogger<StoriesController> log) : ControllerBase
{
    /// <param name="Theme">The players' own words, if they typed any.</param>
    /// <param name="Topic">A theme chip (travel, love, work, family, fairy, misc) or "random".</param>
    /// <param name="Length">"short" or "long".</param>
    /// <param name="Seen">Library stories this phone has already played, so they come back only when all are used.</param>
    public sealed record StoryRequestBody(string? Lang, string? Theme, string? Length, string[]? Names, string? Topic = null, string[]? Seen = null);

    /// <param name="Id">The library story's id, for the phone's list of played stories; none for an AI story.</param>
    public sealed record StoryAnswer(Guid? Id, string Title, string Text, bool FromLibrary);

    [HttpPost]
    [EnableRateLimiting(DecksController.WritePolicy)]
    public async Task<IActionResult> Next(StoryRequestBody body, CancellationToken ct)
    {
        if (!Langs.TryParse(body.Lang, out var lang)) return BadRequest(new { error = "Unknown language." });
        var theme = body.Theme?.Trim();
        if (theme is { Length: > CardRequest.MaxThemeLength }) return BadRequest(new { error = "The theme is too long." });
        var longStory = body.Length == "long";

        if (!string.IsNullOrWhiteSpace(theme) && writer.IsAvailable)
        {
            var names = (body.Names ?? []).Select(n => n.Trim()).Where(n => n.Length is > 0 and <= 24).Take(8).ToList();
            try
            {
                var written = await writer.WriteAsync(new StoryRequest(lang, theme, longStory ? 10 : 6, names), ct);
                return Ok(new StoryAnswer(null, written.Title, written.Text, false));
            }
            catch (CardWriterException ex)
            {
                log.LogWarning("Using a ready-made story: {Reason}", ex.Message);
            }
        }

        var seen = (body.Seen ?? []).Take(1000).Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).ToHashSet();
        return await library.PickAsync(lang, StoryShelf.ParseTheme(body.Topic), longStory, seen, ct) is { } ready
            ? Ok(new StoryAnswer(ready.Id, ready.Title, ready.Text, true))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No story is available." });
    }
}
