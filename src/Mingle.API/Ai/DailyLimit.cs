namespace Mingle.API.Ai;

/// <summary>Counts generations per UTC day in memory (the app runs as one copy).</summary>
public sealed class DailyLimit(TimeProvider clock)
{
    private readonly Lock gate = new();
    private DateOnly day;
    private int used;

    /// <summary>Takes one generation from today's allowance; false when it is used up.</summary>
    public bool TryTake(int perDay)
    {
        lock (gate)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (today != day) (day, used) = (today, 0);
            if (used >= perDay) return false;
            used++;
            return true;
        }
    }
}
