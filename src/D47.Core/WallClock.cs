namespace D47.Core;

/// <summary>What time it is, asked for rather than taken.</summary>
public interface IWallClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The real one.</summary>
public sealed class SystemWallClock : IWallClock
{
    public static readonly SystemWallClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
