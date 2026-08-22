using System.Text;

namespace D47.Core.Adventures;

/// <summary>
/// The story as standing prompt context, below the cache breakpoint beside the game state
/// (list.md Phase 47, "The story is told from inside, between the beats").
/// <para>
/// <b>The persona knows what the Commander knows, plus the stake.</b> The premise, the want and the
/// stake are always here, so the core tells from inside the story rather than reciting the next
/// gate; the turn and the ending appear <em>only once their beats have fired</em>; and the beats
/// ahead never appear. A storyteller who knows the ending will leak it — models especially — and a
/// leaked ending is the whole story lost. Foreshadowing is authored into the earlier beats' lines by
/// the turn that did know the ending, so the persona foreshadows by having been given lines that
/// do, and cannot spoil what it has not been told.
/// </para>
/// </summary>
public static class AdventureContext
{
    public const string Label =
        "Adventure — a story the Commander agreed to hear, told by you. The places in it are real and "
        + "the people in it may not be. Speak from inside it between beats: wonder, foreshadow, apply "
        + "pressure, notice where the Commander is relative to it — but state no new fact about the "
        + "story, and do not recite it. You do not know how it ends. Asked what is actually at a place, "
        + "answer from your tools and say which is which.";

    /// <summary>
    /// The block, or null when nothing is under way — null rather than a block saying so, because an
    /// empty report still costs tokens on every turn.
    /// </summary>
    public static string? Describe(
        IReadOnlyList<AdventureStanding> standings,
        Func<string?, string?> personaName,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(personaName);

        var active = standings.Where(standing => standing.Adventure.IsActive && !standing.IsDone).ToList();

        if (active.Count == 0)
        {
            return null;
        }

        var block = new StringBuilder(Label);

        foreach (var standing in active)
        {
            var adventure = standing.Adventure;

            block.AppendLine().AppendLine();
            block.Append(adventure.Name);

            var by = adventure.Source == AdventureSource.Commander
                ? "written by the Commander"
                : personaName(adventure.WrittenBy) is { } name
                    ? $"written by {name}"
                    : "written without a persona";

            block.Append(", ").Append(by);

            if (adventure.AcceptedAt is { } begun)
            {
                block.Append($", begun {begun:d MMM yyyy}");
            }

            block.Append('.');

            if (adventure.Spine is { } spine)
            {
                Line(block, "Premise", spine.Premise);
                Line(block, "What the Commander is after", spine.Want);
                Line(block, "What is at stake", spine.Stake);

                if (standing.TurnReached)
                {
                    Line(block, "The turn, now reached", spine.Turn);
                }
            }

            if (!string.IsNullOrWhiteSpace(adventure.Opening))
            {
                Line(block, "Opening", adventure.Opening);
            }

            if (standing.Fired.Count > 0)
            {
                var titles = string.Join(", ", standing.Adventure.Beats.Take(standing.Fired.Count).Select(beat => beat.Title));
                Line(block, "So far", titles);

                if (standing.LastBeat is { } last && standing.LastFiredAt is { } at)
                {
                    Line(block, $"Last beat, {last.Title}, {Ago(now - at)}", last.Line);
                }
            }

            if (standing.CurrentBeat is { } current)
            {
                var function = string.IsNullOrWhiteSpace(current.Function) ? string.Empty : $" ({current.Function})";
                Line(block, $"Now: {current.Title}{function}", $"waiting to {current.Trigger.Describe()}.");
            }
        }

        return block.ToString().TrimEnd();
    }

    private static void Line(StringBuilder block, string label, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        block.AppendLine().Append("  ").Append(label).Append(": ").Append(text.Trim());
    }

    private static string Ago(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => $"{(int)age.TotalMinutes} minutes ago",
        < 1440 => $"{(int)age.TotalHours} hours ago",
        _ => $"{(int)age.TotalDays} days ago",
    };
}
