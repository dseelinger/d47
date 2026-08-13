using System.Text;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>
/// What changed aboard while a core was switched off.
/// <para>
/// This is the one thing every core can see regardless of which of them was running, and that
/// asymmetry is the design: they all read the same instrument panel and none of them can see
/// each other (guardian-personas.md). A returning core gets the delta and interprets it
/// according to its own damage — Cora logs it, Sentinel demands the combat log, Chart measures
/// it against the sky, the Quartermaster reconciles it.
/// </para>
/// <para>
/// Derived from the session summary d47 is already folding, rather than from anything new. It
/// is deliberately a handful of plain sentences and never raw journal text: the delta is
/// authored here and goes into a prompt, and journal content is untrusted input
/// (architecture.md §7).
/// </para>
/// </summary>
public static class TelemetryDelta
{
    /// <summary>
    /// The change between two snapshots, or null when nothing worth remarking on happened.
    /// Null matters: a core that returns after two minutes in which nobody moved should not be
    /// handed a paragraph saying nothing changed, because it will find something to say about
    /// it anyway.
    /// </summary>
    public static string? Between(SessionSummary? before, SessionSummary? after, CommanderGameState? state)
    {
        if (after is null || !after.IsKnown)
        {
            return null;
        }

        var was = before ?? SessionSummary.Empty;
        var lines = new List<string>();

        if (after.Jumps - was.Jumps is > 0 and var jumps)
        {
            lines.Add(jumps == 1 ? "One hyperspace jump." : $"{jumps} hyperspace jumps.");
        }

        if (after.DistanceTravelled - was.DistanceTravelled is > 0.5 and var distance)
        {
            lines.Add($"{distance:0} light years covered.");
        }

        if (after.TotalEarnings - was.TotalEarnings is > 0 and var earned)
        {
            lines.Add($"{earned:N0} credits earned.");
        }

        // Balance is the Commander's whole account rather than session earnings, so a fall in
        // it is the only signal here for money spent — outfitting, rebuy, restocking.
        if (was.Balance is { } wasBalance && after.Balance is { } nowBalance && nowBalance < wasBalance)
        {
            lines.Add($"{wasBalance - nowBalance:N0} credits spent.");
        }

        if (after.Deaths - was.Deaths is > 0 and var deaths)
        {
            lines.Add(deaths == 1 ? "The ship was destroyed once." : $"The ship was destroyed {deaths} times.");
        }

        if (after.Interdictions - was.Interdictions is > 0 and var interdictions)
        {
            lines.Add(interdictions == 1
                ? "One interdiction."
                : $"{interdictions} interdictions.");
        }

        if (after.MaterialsGained - was.MaterialsGained is > 0 and var materials)
        {
            lines.Add($"{materials} materials collected.");
        }

        if (after.BodiesScanned - was.BodiesScanned is > 0 and var bodies)
        {
            lines.Add(bodies == 1 ? "One body scanned." : $"{bodies} bodies scanned.");
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var delta = new StringBuilder("While you were not running:");

        foreach (var line in lines)
        {
            delta.Append("\n  ").Append(line);
        }

        if (Situation.Describe(state) is { Length: > 0 } situation)
        {
            delta.Append("\n\nWhere the ship is now:\n").Append(situation);
        }

        return delta.ToString();
    }

    /// <summary>
    /// How long the core was off, written the way a person would say it. Used in the prompt
    /// rather than a raw <see cref="TimeSpan"/>, because "PT2H14M" is not something a companion
    /// can react to and every core is going to be asked to react to it.
    /// </summary>
    public static string Spoken(TimeSpan away) => away switch
    {
        { TotalMinutes: < 2 } => "barely a minute",
        { TotalHours: < 1 } => $"{away.TotalMinutes:0} minutes",
        { TotalHours: < 2 } => "about an hour",
        { TotalDays: < 1 } => $"{away.TotalHours:0} hours",
        { TotalDays: < 2 } => "about a day",
        _ => $"{away.TotalDays:0} days",
    };
}
