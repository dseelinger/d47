using System.Globalization;
using System.Text;

namespace D47.Core.Adventures;

/// <summary>When a story has waited long enough at its next beat for the narrator to lean toward it.</summary>
public static class AdventureNudge
{
    /// <summary>Play sessions, counted by <c>LoadGame</c>, since the last beat or nudge.</summary>
    public const int Sessions = 3;

    /// <summary>Time since the last beat or nudge.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromDays(7);

    /// <summary>The last beat, the last nudge or the acceptance, whichever is latest.</summary>
    public static DateTimeOffset? Since(AdventureStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);

        var since = standing.LastFiredAt ?? standing.Adventure.AcceptedAt;

        foreach (var told in standing.Adventure.Told)
        {
            if (told.Kind == AdventureToldKind.Nudge && (since is null || told.At > since))
            {
                since = told.At;
            }
        }

        return since;
    }

    public static bool IsDue(AdventureStanding standing, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(standing);

        if (!standing.Adventure.IsActive || standing.CurrentBeat is null || Since(standing) is not { } since)
        {
            return false;
        }

        return now - since >= Wait && standing.Loads.Count(load => load > since) >= Sessions;
    }

    /// <summary>
    /// What the narrator is given: the next beat's trigger, the distance to it when known, and the spine
    /// up to what has been reached. Never the beat's line.
    /// </summary>
    public static string Facts(AdventureStanding standing, double? lightYears)
    {
        ArgumentNullException.ThrowIfNull(standing);

        var adventure = standing.Adventure;
        var text = new StringBuilder($"The story: {adventure.Name}.");

        if (standing.CurrentBeat is { } next)
        {
            text.Append(CultureInfo.InvariantCulture, $" Where it waits: {next.Trigger.Describe()}.");
        }

        if (lightYears is { } distance)
        {
            text.Append(CultureInfo.InvariantCulture, $" Distance from the Commander, in a straight line: {distance:0} light years.");
        }

        if (adventure.Spine is { } spine)
        {
            Line(text, "Premise", spine.Premise);
            Line(text, "What the Commander is after", spine.Want);
            Line(text, "What is at stake", spine.Stake);

            if (standing.TurnReached)
            {
                Line(text, "The turn, now reached", spine.Turn);
            }
        }

        return text.ToString();
    }

    /// <summary>The system the next beat is in, for the distance, or null for a rank.</summary>
    public static string? Destination(AdventureStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);

        return standing.CurrentBeat?.Trigger is { Kind: not TriggerKind.Rank, System: { Length: > 0 } system }
            ? system
            : null;
    }

    private static void Line(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.Append(' ').Append(label).Append(": ").Append(value.Trim());
        }
    }
}
