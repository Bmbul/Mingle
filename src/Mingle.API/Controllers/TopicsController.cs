using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.API.Controllers;

/// <summary>The topics players can pick, with their names in each language.</summary>
[ApiController]
[Route("api/topics")]
public sealed class TopicsController(ITopicSource topics) : ControllerBase
{
    /// <summary><c>[ { "code": "travel", "name": { "en": "Travel", "hy": "…", "ru": "…" } } ]</c></summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var list = new JsonArray();
        foreach (var topic in await topics.GetPickableTopicsAsync(ct))
        {
            var names = new JsonObject();
            foreach (var (lang, name) in topic.Names.OrderBy(n => n.Key)) names[lang.Code()] = name;
            list.Add(new JsonObject { ["code"] = topic.Code, ["name"] = names });
        }
        return Content(list.ToJsonString(), "application/json; charset=utf-8");
    }
}
