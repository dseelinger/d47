namespace D47.Core.Callouts;

/// <summary>Decides, line by line, whether an accented voice's words are written to suit its accent.</summary>
public sealed class AccentRoll(Random? choice = null)
{
    /// <summary>Hits at every percent.</summary>
    public static readonly AccentRoll Always = new(new Fixed());

    private readonly Random _choice = choice ?? Random.Shared;

    /// <summary>True on a hit against <paramref name="percent"/> (0-100, clamped). 0 never hits; 100 always does.</summary>
    public bool Hits(int percent) => _choice.Next(100) < Math.Clamp(percent, 0, 100);

    private sealed class Fixed : Random
    {
        public override int Next(int maxValue) => 0;
    }
}
