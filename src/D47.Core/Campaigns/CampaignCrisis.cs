using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>The open crises after a step, and the lines due.</summary>
public sealed record CrisisStep(IReadOnlyList<OpenCrisis> Crises, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>What the crisis arc reads: the faction, the seed key, the report now and the one before it.</summary>
public sealed record CrisisView(string Faction, string SeedKey, FactionReport? Report, FactionReport? Previous, DateTimeOffset Now);

/// <summary>
/// The Crisis and blame arc: it opens when a bad state becomes active in a system the campaign faction controls, blames the
/// rival with the most influence there, sees that rival exploit it when its influence there rises, and closes when the state
/// is no longer active. Pure: the caller supplies the time and keeps the state.
/// </summary>
public static class CampaignCrisis
{
    /// <summary>The rival with the most influence in <paramref name="system"/>, or null when none has a known influence.</summary>
    public static string? Blamed(FactionSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        return system.Others
            .Where(other => other.Influence is not null)
            .OrderByDescending(other => other.Influence)
            .ThenBy(other => other.Name, StringComparer.Ordinal)
            .FirstOrDefault()?.Name;
    }

    /// <summary>The bad states active in <paramref name="system"/>, as the service spells them.</summary>
    public static IReadOnlyList<string> BadStates(FactionSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        return [.. system.ActiveStates.Select(FactionStates.Display).Where(CampaignFronts.IsBadState).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The crises after one look, and the lines due. A bad state active in a controlled system that the previous report held
    /// without it opens a crisis, blaming the rival with the most influence there. A crisis whose blamed rival has gained
    /// influence since the previous report is exploited, once. A crisis whose state is no longer active, in a system the
    /// report still lists, closes. A crisis whose system has left the report is dropped without a word. With no previous
    /// report nothing opens: what is already there is the baseline.
    /// </summary>
    public static CrisisStep Step(IReadOnlyList<OpenCrisis> crises, CrisisView view)
    {
        ArgumentNullException.ThrowIfNull(crises);
        ArgumentNullException.ThrowIfNull(view);

        var lines = new List<CampaignArcLine>();
        var next = new List<OpenCrisis>();

        foreach (var crisis in crises)
        {
            var system = Find(view.Report, crisis.System);

            if (system is null)
            {
                continue;
            }

            if (!BadStates(system).Contains(crisis.State, StringComparer.Ordinal))
            {
                lines.Add(Relief(view, crisis));
                continue;
            }

            if (!crisis.Exploited && Gained(view, crisis, system))
            {
                lines.Add(Exploit(view, crisis));
                next.Add(crisis with { Exploited = true });
                continue;
            }

            next.Add(crisis);
        }

        if (view.Report is not null && view.Previous is not null)
        {
            foreach (var system in view.Report.Systems.Where(system => system.Controls))
            {
                var before = Find(view.Previous, system.Name);

                if (before is null)
                {
                    continue;
                }

                var held = BadStates(before);

                foreach (var state in BadStates(system).Where(state => !held.Contains(state, StringComparer.Ordinal)))
                {
                    if (next.Any(crisis => Same(crisis, system.Name, state)))
                    {
                        continue;
                    }

                    var crisis = new OpenCrisis(system.Name, state, Blamed(system), view.Now, false);

                    next.Add(crisis);
                    lines.Add(Opened(view, crisis));
                }
            }
        }

        return new CrisisStep(next, lines);
    }

    private static bool Same(OpenCrisis crisis, string system, string state) =>
        string.Equals(crisis.System, system, StringComparison.OrdinalIgnoreCase) && string.Equals(crisis.State, state, StringComparison.Ordinal);

    private static FactionSystem? Find(FactionReport? report, string system) =>
        report?.Systems.FirstOrDefault(row => string.Equals(row.Name, system, StringComparison.OrdinalIgnoreCase));

    private static bool Gained(CrisisView view, OpenCrisis crisis, FactionSystem system)
    {
        if (crisis.Blamed is not { } blamed || Find(view.Previous, crisis.System) is not { } before)
        {
            return false;
        }

        double? Influence(FactionSystem row) =>
            row.Others.FirstOrDefault(other => string.Equals(other.Name, blamed, StringComparison.OrdinalIgnoreCase))?.Influence;

        return Influence(system) is { } now && Influence(before) is { } was && now > was;
    }

    private static CampaignArcLine Opened(CrisisView view, OpenCrisis crisis)
    {
        var facts = new List<string> { $"{crisis.State} has become active in {crisis.System}, which {view.Faction} controls." };

        if (crisis.Blamed is { } blamed)
        {
            facts.Add($"{blamed} is the strongest rival in {crisis.System} and is blamed.");
        }

        return new CampaignArcLine(ArcAct.Crisis, view.Faction, crisis.Blamed ?? string.Empty, CampaignLiaison.Speaker, crisis.System, facts);
    }

    private static CampaignArcLine Exploit(CrisisView view, OpenCrisis crisis) =>
        new(
            ArcAct.Exploit,
            view.Faction,
            crisis.Blamed!,
            CampaignArcs.Leader(crisis.Blamed!, view.SeedKey),
            crisis.System,
            [
                $"{crisis.State} is active in {crisis.System}, which {view.Faction} controls.",
                $"{crisis.Blamed} has gained influence in {crisis.System} since the last report.",
            ]);

    private static CampaignArcLine Relief(CrisisView view, OpenCrisis crisis) =>
        new(
            ArcAct.Relief,
            view.Faction,
            crisis.Blamed ?? string.Empty,
            CampaignLiaison.Speaker,
            crisis.System,
            [$"{crisis.State} is no longer active in {crisis.System}. The crisis is over."]);
}
