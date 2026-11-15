namespace D47.Core.Journal;

/// <summary>How a system's newest reading stands against a BGS tick time.</summary>
public enum SinceTick
{
    /// <summary>No reading at or after the tick.</summary>
    NoReading,

    /// <summary>The latest span was first seen before the tick and last seen at or after it.</summary>
    Unchanged,

    /// <summary>The latest span was first seen at or after the tick, and an earlier kept span was first seen before it.</summary>
    Changed,

    /// <summary>Every kept span was first seen at or after the tick.</summary>
    NoEarlier,
}
