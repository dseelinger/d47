using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>The Ideology arc after a step, and the lines due.</summary>
public sealed record IdeologyStep(IdeologyArc? Arc, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>
/// The Ideology arc: it opens when the campaign faction's government differs from that of a rival leading a front against
/// it, and then acts on a new front against the rival, a front won against it, and an election with both present. Pure:
/// the caller supplies the time and keeps the state.
/// </summary>
public static class CampaignIdeology
{
    /// <summary>The link strength a defector is heard through: a hailing frequency, thinner than a broadcast.</summary>
    public const double DefectorLink = 0.7;

    /// <summary>The speaker credited with a defection line, for a rival.</summary>
    public static string Defector(string rival) => $"Defector from {rival}";

    /// <summary>The government the report gives the faction, or null.</summary>
    public static string? OurGovernment(FactionReport? report) =>
        report?.Government is { Length: > 0 } government ? government : null;

    /// <summary>The government the report gives <paramref name="rival"/> in any system it shares with the faction, or null.</summary>
    public static string? TheirGovernment(FactionReport? report, string rival)
    {
        ArgumentNullException.ThrowIfNull(rival);

        return report?.Systems
            .SelectMany(system => system.Others)
            .FirstOrDefault(other => string.Equals(other.Name, rival, StringComparison.OrdinalIgnoreCase) && other.Government is { Length: > 0 })
            ?.Government;
    }

    /// <summary>Whether both governments are known and differ.</summary>
    public static bool Differ(string? ours, string? theirs) =>
        ours is { Length: > 0 } && theirs is { Length: > 0 } && !string.Equals(ours.Trim(), theirs.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The first rival, in front rank order, leading a front against the faction with a government that differs from the
    /// faction's, with that front; null when there is none.
    /// </summary>
    public static (string Rival, Front Front)? Opening(ArcView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var ours = OurGovernment(view.Report);

        if (ours is null)
        {
            return null;
        }

        foreach (var front in view.Fronts)
        {
            if (front.Opponent is { Length: > 0 } rival
                && CampaignArcs.Against(rival, [front]).Count == 1
                && Differ(ours, TheirGovernment(view.Report, rival)))
            {
                return (rival, front);
            }
        }

        return null;
    }

    /// <summary>
    /// The arc after one look, and the lines due. With no arc, it opens on <see cref="Opening"/> with a propaganda broadcast
    /// naming both governments; the fronts and elections already there are the baseline for what follows. With one, and
    /// while the two governments still differ: a front against the rival in a system not yet broadcast on is broadcast
    /// (one a look); a chronicle entry won against the rival draws a defection; and an election active or pending in a system
    /// both are in is framed as a contest of ideas, spoken by the Liaison (one a look).
    /// </summary>
    public static IdeologyStep Step(IdeologyArc? arc, ArcView now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (arc is null)
        {
            return Begin(now);
        }

        var ours = OurGovernment(now.Report);
        var theirs = TheirGovernment(now.Report, arc.Rival);
        var next = arc with { Answered = now.Chronicle.Count };

        if (!Differ(ours, theirs))
        {
            return new IdeologyStep(next, []);
        }

        var lines = new List<CampaignArcLine>();
        var leader = CampaignArcs.Leader(arc.Rival, now.SeedKey);

        for (var index = Math.Min(arc.Answered, now.Chronicle.Count); index < now.Chronicle.Count; index++)
        {
            var entry = now.Chronicle[index];

            if (entry.Outcome == "Won" && string.Equals(entry.Opponent, arc.Rival, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(new CampaignArcLine(
                    ArcAct.Defection,
                    now.Faction,
                    arc.Rival,
                    Defector(arc.Rival),
                    entry.System,
                    [
                        $"{now.Faction} won the {entry.Kind.ToLowerInvariant()} in {entry.System} against {arc.Rival}.",
                        $"A member of {arc.Rival} has defected to {now.Faction}.",
                        Contrast(now.Faction, ours!, arc.Rival, theirs!),
                    ]));
            }
        }

        var broadcast = CampaignArcs.Against(arc.Rival, now.Fronts)
            .FirstOrDefault(front => !arc.Fronts.Contains(front.System, StringComparer.OrdinalIgnoreCase));

        if (broadcast is not null)
        {
            next = next with { Fronts = [.. arc.Fronts, broadcast.System] };
            lines.Add(Propaganda(now, arc.Rival, leader, broadcast, ours!, theirs!));
        }

        if (Election(now, arc) is { } election)
        {
            next = next with { Elections = [.. arc.Elections, election.Key] };
            lines.Add(Framed(now, arc.Rival, election.System, election.State, ours!, theirs!));
        }

        return new IdeologyStep(next, lines);
    }

    private static IdeologyStep Begin(ArcView now)
    {
        if (Opening(now) is not var (rival, front))
        {
            return new IdeologyStep(null, []);
        }

        var ours = OurGovernment(now.Report)!;
        var theirs = TheirGovernment(now.Report, rival)!;
        var leader = CampaignArcs.Leader(rival, now.SeedKey);
        var arc = new IdeologyArc(
            rival,
            ours,
            theirs,
            now.Now,
            now.Chronicle.Count,
            [.. CampaignArcs.Against(rival, now.Fronts).Select(against => against.System).Distinct(StringComparer.OrdinalIgnoreCase)],
            []);

        return new IdeologyStep(arc, [Propaganda(now, rival, leader, front, ours, theirs)]);
    }

    private static CampaignArcLine Propaganda(ArcView now, string rival, string leader, Front front, string ours, string theirs) =>
        new(
            ArcAct.Propaganda,
            now.Faction,
            rival,
            leader,
            front.System,
            [
                $"{leader} leads {rival}.",
                Contrast(now.Faction, ours, rival, theirs),
                $"{rival} opposes {now.Faction} in the {CampaignLiaison.Named(front)} in {front.System}.",
            ]);

    private static CampaignArcLine Framed(ArcView now, string rival, string system, string state, string ours, string theirs) =>
        new(
            ArcAct.Election,
            now.Faction,
            rival,
            CampaignLiaison.Speaker,
            system,
            [
                $"An election is {state} in {system}, where {now.Faction} and {rival} are both present.",
                Contrast(now.Faction, ours, rival, theirs),
            ]);

    private static string Contrast(string faction, string ours, string rival, string theirs) =>
        $"{faction} has a {ours} government and {rival} has a {theirs} government.";

    /// <summary>The first election, active or pending, in a system the rival shares with the faction, that has not been framed.</summary>
    private static (string Key, string System, string State)? Election(ArcView now, IdeologyArc arc)
    {
        var election = FactionStates.Display("Election");

        bool Has(IReadOnlyList<string> states) =>
            states.Any(state => string.Equals(FactionStates.Display(state), election, StringComparison.Ordinal));

        foreach (var system in now.Report?.Systems ?? [])
        {
            var them = system.Others.FirstOrDefault(other => string.Equals(other.Name, arc.Rival, StringComparison.OrdinalIgnoreCase));

            if (them is null)
            {
                continue;
            }

            foreach (var (state, any) in new[]
            {
                ("active", Has(system.ActiveStates) || Has(them.ActiveStates)),
                ("pending", Has(system.PendingStates) || Has(them.PendingStates)),
            })
            {
                var key = $"{system.Name}|{state}";

                if (any && !arc.Elections.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    return (key, system.Name, state);
                }
            }
        }

        return null;
    }
}
