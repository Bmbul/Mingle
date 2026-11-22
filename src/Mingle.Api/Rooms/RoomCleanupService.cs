namespace Mingle.Api.Rooms;

/// <summary>Every few minutes, drops rooms that everyone has left.</summary>
public sealed class RoomCleanupService(RoomRegistry rooms, ILogger<RoomCleanupService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleLimit = TimeSpan.FromHours(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removed = rooms.RemoveIdle(IdleLimit);
            if (removed > 0) log.LogInformation("Removed {Count} idle rooms; {Open} still open.", removed, rooms.Count);
        }
    }
}
