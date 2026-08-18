using System.Globalization;
using D47.Core.Utilities;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Clocks, timers and alarms (list.md Phase 24, "Utilities").
/// <para>
/// <b>"Ship AI" means the app, not the language model.</b> "What's the date?" is answered by the
/// keyword router, deterministically, with no turn taken and no provider needed — it works with
/// no key configured, no network, and no tokens spent, which is the correct posture for a
/// question d47 already holds the answer to. So the tool that answers it is <c>Protected</c>: the
/// router reaches it and the model never sees it, which is also why it costs nothing per turn.
/// </para>
/// <para>
/// <b>A tool was the obvious answer for the date and is the wrong one</b> — the advertised surface
/// is paid for on every turn whether or not anybody asks the time. Instead the two dates ride the
/// <em>game-state block</em> (see <see cref="Live"/>), which sits below the cache breakpoint, so a
/// value that changes every turn costs nothing in cache terms and the persona can reference it in
/// conversation with no tool call at all.
/// </para>
/// <para>
/// <b>Both dates are supplied pre-computed and the model is never asked to add 1286.</b> That is
/// arithmetic, and a model doing arithmetic in prose is wrong occasionally and confidently.
/// </para>
/// <para>
/// <b>Cancelling is the Commander's</b>, reachable from the panel and the keyword router and from
/// nothing else. Protected is a property of the caller rather than the modality, so a spoken
/// cancel works and a hostile in-game message cannot reach one — and an alarm somebody relies on
/// to leave the house is worth that line being drawn before anybody asks for it.
/// </para>
/// </summary>
public static class UtilitiesCapability
{
    public const string Id = "utilities";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <param name="timekeeper">
    /// The Commander's timers and alarms, or null under the designer and in tests that are not
    /// about them — the capability still registers, so its documentation page exists, and every
    /// tool answers that there is nothing keeping time rather than throwing.
    /// </param>
    /// <param name="now">
    /// The injected clock. Defaults to the epoch, which reads as "no time at all" rather than as
    /// a plausible wrong answer.
    /// </param>
    /// <param name="zone">
    /// How to present the instant locally. A function rather than a value because a Commander who
    /// changes time zone mid-session — which is what flying does to a laptop — should not have to
    /// restart to see it.
    /// </param>
    public static CapabilityDescriptor Create(
        Timekeeper? timekeeper = null,
        Func<DateTimeOffset>? now = null,
        Func<TimeZoneInfo>? zone = null)
    {
        var clock = now ?? (() => DateTimeOffset.MinValue);
        var where = zone ?? (() => TimeZoneInfo.Local);

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Interface",
            Name = "Clocks and timers",
            Summary = "The date in both worlds, and timers and alarms that say their own name.",
            Examples = ["what's the date", "set a timer for forty minutes", "cancel the mining timer"],
            Display = new CapabilityDisplay { PanelTitle = "Clocks and timers", Order = 41 },

            // Phrases, never bare words. "time" alone would hijack any sentence containing it,
            // which is the failure the router's whole-phrase rule exists against.
            Keywords = ["what is the date", "what's the date", "what time is it"],

            Tools =
            [
                // Protected, so it is never advertised and costs nothing per turn. The router is
                // the only way in, which is exactly what "the app answers this, not the model"
                // means in code.
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

                // Advertised, unlike the rest of this capability, and the asymmetry is the point.
                // A duration arrives as English — "about forty minutes", "an hour and a half" —
                // which is the one thing a model does better than a closed grammar, and the
                // router cannot extract an argument from free text without starting to guess.
                new ToolDefinition
                {
                    Name = "set_timer",
                    Description =
                        "Start a countdown that says its own name when it finishes. Minutes, from the "
                        + "moment it is set. Timers do not survive D47 restarting; use an alarm for a "
                        + "wall-clock moment.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "name",
                            Type = ToolParameterType.String,
                            Description = "What to call it. Said back when it finishes.",
                            Required = true,
                        },
                        new ToolParameter
                        {
                            Name = "minutes",
                            Type = ToolParameterType.Number,
                            Description = "How long, in minutes.",
                            Required = true,
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(StartTimer(timekeeper, clock, arguments)),
                },

                new ToolDefinition
                {
                    Name = "set_alarm",
                    Description =
                        "Set an alarm for a wall-clock time today or tomorrow. Alarms survive D47 "
                        + "restarting, and one that came round while D47 was closed is reported "
                        + "afterwards rather than sounded late.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "name",
                            Type = ToolParameterType.String,
                            Description = "What to call it. Said back when it goes off.",
                            Required = true,
                        },
                        new ToolParameter
                        {
                            Name = "at",
                            Type = ToolParameterType.String,
                            Description = "Local time for the Commander, as 24-hour HH:mm.",
                            Required = true,
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(SetAlarm(timekeeper, clock, where, arguments)),
                },

                // Protected. An alarm somebody relies on to leave the house is not a thing an
                // in-game message gets to clear, and the phrases below still make a spoken cancel
                // work — Protected is about the caller, not the modality.
                new ToolDefinition
                {
                    Name = "cancel_reminder",
                    Description =
                        "Cancel a timer or an alarm by name. The Commander's own act: not offered to "
                        + "the model, and refused if it asks.",
                    Protected = true,
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "name",
                            Type = ToolParameterType.String,
                            Description = "Which one. Omit to cancel everything running.",
                        },
                    ],
                    Commands =
                    [
                        new ToolCommandPhrase("cancel the timer", Nothing),
                        new ToolCommandPhrase("cancel my timer", Nothing),
                        new ToolCommandPhrase("cancel the alarm", Nothing),
                        new ToolCommandPhrase("cancel my alarm", Nothing),
                        new ToolCommandPhrase("stop the timer", Nothing),
                    ],
                    Handler = (arguments, _) => Task.FromResult(Cancel(timekeeper, arguments)),
                },
            ],
        };
    }

    /// <summary>
    /// The two dates, and what is keeping time, for the game-state block (prompt position 7).
    /// <para>
    /// Below the cache breakpoint, so this changes every turn without invalidating a byte above
    /// it — which is what makes putting a per-turn value here free where a tool would not be.
    /// </para>
    /// <para>
    /// <b>Pre-computed.</b> The model is handed both dates and never asked to derive one from the
    /// other.
    /// </para>
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

    private static ToolResult StartTimer(
        Timekeeper? timekeeper, Func<DateTimeOffset> now, ToolArguments arguments)
    {
        if (timekeeper is null)
        {
            return ToolResult.Error("Nothing here is keeping time.");
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("A timer needs a name, so I can say which one finished.");
        }

        if (!arguments.TryGetDouble("minutes", out var minutes))
        {
            return ToolResult.Error("I need to know how long, in minutes.");
        }

        var started = timekeeper.StartTimer(name, TimeSpan.FromMinutes(minutes), now());

        return started is null
            ? ToolResult.Error("That is not a length of time I can count down.")
            : ToolResult.Ok($"\"{started.Name}\" is running: {started.Describe(now(), TimeZoneInfo.Utc)}.");
    }

    private static ToolResult SetAlarm(
        Timekeeper? timekeeper,
        Func<DateTimeOffset> now,
        Func<TimeZoneInfo> zone,
        ToolArguments arguments)
    {
        if (timekeeper is null)
        {
            return ToolResult.Error("Nothing here is keeping time.");
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("An alarm needs a name, so I can say which one went off.");
        }

        if (!arguments.TryGetString("at", out var at)
            || !TimeOnly.TryParseExact(at, "HH\\:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
        {
            return ToolResult.Error("I need a time as 24-hour HH:mm.");
        }

        var due = Next(when, now(), zone());
        var alarm = timekeeper.SetAlarm(name, due, now());

        return alarm is null
            ? ToolResult.Error("That moment has already gone.")
            : ToolResult.Ok($"\"{alarm.Name}\" is set for {alarm.Describe(now(), zone())}.");
    }

    /// <summary>
    /// The next occurrence of a wall-clock time, in the Commander's own zone.
    /// <para>
    /// Tomorrow when today's has gone, because "seven in the morning" said at nine in the evening
    /// means the seven that is coming rather than the one that went — and refusing it would be
    /// refusing the commonest alarm anybody sets.
    /// </para>
    /// </summary>
    private static DateTimeOffset Next(TimeOnly at, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = GalacticTime.Local(now, zone);
        var today = new DateTimeOffset(local.Date.Add(at.ToTimeSpan()), local.Offset);

        return today > local ? today : today.AddDays(1);
    }

    private static ToolResult Cancel(Timekeeper? timekeeper, ToolArguments arguments)
    {
        if (timekeeper is null)
        {
            return ToolResult.Error("Nothing here is keeping time.");
        }

        var running = timekeeper.Running;

        if (running.Count == 0)
        {
            return ToolResult.Ok("Nothing is running.");
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            foreach (var reminder in running)
            {
                timekeeper.Cancel(reminder.Id);
            }

            return ToolResult.Ok($"Cancelled {running.Count}.");
        }

        if (timekeeper.Find(name) is not { } found)
        {
            // Nought and several answer the same way rather than the second guessing, which is
            // the rule the checklist's own matcher already follows: cancelling the wrong alarm of
            // two is worse than asking which.
            return ToolResult.Error($"I could not tell which one \"{name}\" means.");
        }

        timekeeper.Cancel(found.Id);
        return ToolResult.Ok($"Cancelled \"{found.Name}\".");
    }
}
