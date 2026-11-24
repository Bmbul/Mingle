namespace Mingle.API.Ai;

/// <summary>
/// Starting ideas for fill-in stories, so stories differ from game to game instead of always being a dinner or a
/// date. Each theme chip draws from its own group; "surprise me" draws from all of them.
/// </summary>
public static class StoryPremises
{
    public static readonly IReadOnlyDictionary<string, string[]> ByTheme = new Dictionary<string, string[]>
    {
        ["travel"] =
        [
            "the airport scanner starts reading everyone's thoughts out loud",
            "a guided tour where the guide is clearly making everything up",
            "the hotel room comes with a roommate nobody mentioned",
            "a road trip where the navigator only speaks in riddles",
            "the suitcase arrives, but it belongs to a famous magician",
            "a cruise ship that is secretly very small",
            "a camping trip where the tent has strong opinions",
        ],
        ["love"] =
        [
            "a first date where both people secretly brought their mothers",
            "a marriage proposal that keeps getting interrupted by the same pigeon",
            "a dating app that matched one of the players with a vending machine",
            "a romantic dinner where the waiter falls in love with the dessert",
            "an anniversary gift that turns out to be alive",
            "a love letter delivered to the wrong apartment, which answers back",
        ],
        ["work"] =
        [
            "the office printer starts demanding a salary",
            "a job interview where the interviewer is a very confident goose",
            "the boss announces that from today all meetings are held underwater",
            "the new intern is obviously a cat in a tie",
            "a team-building day that turns into a kingdom with its own king",
            "the coffee machine goes on strike and publishes a list of demands",
            "a video call where everyone's camera shows the wrong person",
        ],
        ["family"] =
        [
            "grandma has been secretly running a pirate radio station",
            "the cat files an official complaint against the family",
            "the family dog wins the lottery and refuses to share",
            "a wedding where the groom is replaced by a goat and nobody notices",
            "grandpa starts a startup in the garage",
            "the neighbors install a giant mirror facing the family's window",
            "a family photo shoot where the photographer is afraid of faces",
            "the fridge has started leaving notes about everyone's habits",
        ],
        ["fairy"] =
        [
            "a dragon who is afraid of mice opens a bakery",
            "a princess hires a knight, but the knight is extremely lazy",
            "a wizard's spell turns the village into very polite furniture",
            "a talking frog runs for mayor",
            "three wishes, but the genie misunderstands every one of them",
            "a fairy godmother who only grants wishes on Tuesdays",
        ],
        ["misc"] =
        [
            "aliens land, but they only want to learn how to make dolma",
            "a superhero whose only power is finding lost socks",
            "the whole city loses its sense of direction for one day",
            "a TV cooking show where the ingredients fight back",
            "a museum statue comes to life and complains about the visitors",
            "the elevator in the building gets stuck between two time zones",
            "a sports match where the ball has decided to retire",
        ],
    };

    /// <summary>A random premise for a theme chip; an unknown theme or "random" draws from every group.</summary>
    public static string Pick(string? theme, Random? random = null)
    {
        random ??= Random.Shared;
        var pool = theme is not null && ByTheme.TryGetValue(theme, out var group) ? group : [.. ByTheme.Values.SelectMany(g => g)];
        return pool[random.Next(pool.Length)];
    }
}
