using System.Collections.Immutable;

namespace Mingle.Engine;

public sealed record SelectionContext(
    ImmutableArray<Card> Pool,
    IReadOnlySet<string> PlayedIds,
    int CardNumber,
    GameSettings Settings,
    int Seed);

public interface ICardSelector
{
    /// <summary>Picks the next card, or null when no unplayed card is left.</summary>
    Card? Pick(SelectionContext context);
}

/// <summary>
/// Starts light and ramps toward the deepest allowed depth over the game, choosing at random
/// among unplayed cards at the target depth. Deterministic for a given seed and card number.
/// </summary>
public sealed class RampCardSelector : ICardSelector
{
    public static RampCardSelector Instance { get; } = new();

    public Card? Pick(SelectionContext context)
    {
        var remaining = context.Pool
            .Where(c => !context.PlayedIds.Contains(c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
        if (remaining.Count == 0) return null;

        var target = TargetDepth(context.CardNumber, context.Settings);
        var bestDistance = remaining.Min(c => Distance(c.Depth, target));
        var candidates = remaining.Where(c => Distance(c.Depth, target) == bestDistance).ToList();

        var random = new Random(unchecked(context.Seed * 397 ^ context.CardNumber));
        return candidates[random.Next(candidates.Count)];
    }

    /// <summary>Splits the game into equal stretches, one per allowed depth, lightest first.</summary>
    public static int TargetDepth(int cardNumber, GameSettings settings)
    {
        var span = settings.MaxDepth - settings.MinDepth;
        if (span <= 0 || settings.CardCount <= 1) return settings.MinDepth;
        var stretch = (int)((cardNumber - 1) * (span + 1) / (double)settings.CardCount);
        return settings.MinDepth + Math.Clamp(stretch, 0, span);
    }

    // Prefer the target depth, then shallower cards over deeper ones at the same distance.
    private static double Distance(int depth, int target) =>
        depth == target ? 0 : depth < target ? target - depth : depth - target + 0.5;
}
