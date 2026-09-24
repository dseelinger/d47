using D47.Core.Utilities;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The date and time in both worlds, registered on every run.</summary>
public static class ClockCapability
{
    public const string Id = "clock";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static CapabilityDescriptor Create(
        Func<DateTimeOffset>? now = null,
        Func<TimeZoneInfo>? zone = null)
    {
        var clock = now ?? (() => DateTimeOffset.MinValue);
        var where = zone ?? (() => TimeZoneInfo.Local);

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Interface",
            Name = "Clock",
            Summary = "The date and time in both worlds, answered without the model.",
            Examples = ["what's the date", "what time is it", "what day is it"],
            Display = new CapabilityDisplay { PanelTitle = "Clock", Order = 40, ShowOnPanel = false },

            // Phrases, never bare words: "time" alone would match any sentence containing it.
            Keywords =
            [
                new("what is the date", "say_the_time"),
                new("what's the date", "say_the_time"),
                new("what time is it", "say_the_time"),
            ],

            Tools =
            [
                // Protected, so it is never advertised and costs nothing per turn.
                new ToolDefinition
                {
                    Name = "say_the_time",
                    Description =
                        "The date and time in both worlds. Answered by D47 itself rather than by the "
                        + "model: no turn, no provider, no tokens.",
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("what is the date", Nothing),
                        new ToolCommandPhrase("what's the date", Nothing),
                        new ToolCommandPhrase("what is the time", Nothing),
                        new ToolCommandPhrase("what's the time", Nothing),
                        new ToolCommandPhrase("what time is it", Nothing),
                        new ToolCommandPhrase("what day is it", Nothing),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        GalacticTime.Read(clock(), where()).Say())),
                },
            ],
        };
    }

    /// <summary>
    /// The two dates for the game-state block (prompt position 7), and the running reminders when there is a
    /// timekeeper.
    /// </summary>
    public static string? Live(Timekeeper? timekeeper, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (now == DateTimeOffset.MinValue)
        {
            return null;
        }

        var clocks = GalacticTime.Read(now, zone);

        var said =
            $"Date: {clocks.GalacticDate}, {clocks.GalacticTimeOfDay} in the galaxy. "
            + $"The Commander's own clock reads {clocks.LocalTimeOfDay} on {clocks.LocalDate}. "
            + "Both are given here already worked out; do not calculate either from the other.";

        var running = timekeeper?.Running ?? [];

        if (running.Count == 0)
        {
            return said;
        }

        var names = string.Join(", ", running.Select(reminder =>
            $"{reminder.Name} ({reminder.Describe(now, zone)})"));

        return said + $" Running: {names}. D47 cannot cancel these; the Commander does.";
    }
}
