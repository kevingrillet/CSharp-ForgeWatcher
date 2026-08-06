using CSharpForgeWatcher.Application.Abstractions;

namespace CSharpForgeWatcher.Infrastructure.Time;

/// <summary>Horloge système (adaptateur de <see cref="IClock"/>).</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Attente réelle (adaptateur de <see cref="IDelayScheduler"/>).</summary>
public sealed class SystemDelayScheduler : IDelayScheduler
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
