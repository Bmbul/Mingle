namespace Mingle.DAL.Entities;

/// <summary>
/// A card kind (open question, would you rather…). Reference data: the rows mirror the engine's
/// <see cref="Engine.CardKinds"/> and are created by the migration. Table: <c>card_kinds</c>.
/// </summary>
public sealed class CardKind
{
    public short Id { get; set; }
    public required string Code { get; set; }
    public required string ResponseType { get; set; }
    public required string Audience { get; set; }

    /// <summary>Fixed ids for the kinds; never renumber them, cards point at these ids.</summary>
    public static readonly IReadOnlyDictionary<string, short> Ids = new Dictionary<string, short>
    {
        ["open"] = 1,
        ["finish"] = 2,
        ["wyr"] = 3,
        ["likely"] = 4,
        ["guess"] = 5,
        ["rate"] = 6,
        ["word"] = 7,
    };

    public static IEnumerable<CardKind> All() =>
        Engine.CardKinds.All.Select(k => new CardKind
        {
            Id = Ids[k.Id],
            Code = k.Id,
            ResponseType = k.Response.ToString(),
            Audience = k.Audience.ToString(),
        });

    public static string CodeOf(short id) => Ids.Single(p => p.Value == id).Key;
}
