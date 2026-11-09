using System.Globalization;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Campaigns;

/// <summary>A kind of computed threat a leak is about, in rank order.</summary>
public enum LeakKind
{
    /// <summary>A rival rising toward the coup line in a system the faction controls.</summary>
    Coup,

    /// <summary>A rival with an Expansion pending in a system the faction is in.</summary>
    Expansion,

    /// <summary>A rival gaining in several of the faction's systems at once.</summary>
    Spread,
}

/// <summary>A threat computed from readings: its kind, the real system it targets, the rival and the rival's influence there.</summary>
/// <param name="Gain">The rival's rise over the readings held, as a fraction; zero for an Expansion with no trend.</param>
/// <param name="Systems">Every system the threat covers; the target is the first.</param>
public sealed record LeakThreat(LeakKind Kind, string System, string Rival, double Influence, double Gain, IReadOnlyList<string> Systems);

/// <summary>A leak already given: the threat it was about, the rival's influence then, and how far the escalation had gone.</summary>
public sealed record Leak(LeakKind Kind, string System, string Rival, double Influence, int Level, DateTimeOffset At);

/// <summary>The campaign's Informant: invented once, with the leaks already given.</summary>
public sealed record CampaignInformant(string Name, DateTimeOffset Since, IReadOnlyList<Leak> Leaks);

/// <summary>What the leak engine reads: the faction's current and previous reports and the Commander's readings.</summary>
public sealed record LeakView(
    string Faction,
    string SeedKey,
    FactionReport? Report,
    FactionReport? Previous,
    SystemStandings Standings,
    DateTimeOffset Now);

/// <summary>The Informant after a step, and the leaks due.</summary>
public sealed record LeakStep(CampaignInformant? Informant, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>
/// The Leaked plans arc: an Informant delivers an intercepted plan when a rival's rise is computed from the readings, before
/// the threat becomes a conflict. Pure: the caller supplies the time and keeps the state.
/// </summary>
public static class CampaignLeaks
{
    /// <summary>The link strength the Informant is heard through: a weak, hissy, dropping line.</summary>
    public const double Link = 0.55;

    /// <summary>The rise in influence, as a fraction, that counts as a trend.</summary>
    public const double MinimumGain = 0.02;

    /// <summary>The rise since a leak that earns the next one.</summary>
    public const double EscalatesAt = 0.05;

    /// <summary>The most escalation levels a threat reaches.</summary>
    public const int MostLevels = 3;

    /// <summary>The number of the faction's systems a rival must be gaining in to be a spread.</summary>
    public const int SpreadSystems = 3;

    private static readonly string[] Callsigns =
    [
        "Wren", "Sable", "Quill", "Marrow", "Fennel", "Cinder", "Lark", "Tallow",
        "Brindle", "Ember", "Hollis", "Juniper", "Kestrel Two", "Linnet", "Mica", "Nettle",
        "Osprey", "Pike", "Rook", "Sorrel", "Thistle", "Umber", "Vesper", "Whisper",
    ];

    /// <summary>The Informant's name, drawn from <paramref name="seedKey"/>, the same on every PC that has the same key.</summary>
    public static string NameFor(string seedKey)
    {
        ArgumentNullException.ThrowIfNull(seedKey);

        return Callsigns[CampaignArcs.Hash($"informant|{seedKey.Trim()}") % (uint)Callsigns.Length];
    }

    /// <summary>
    /// The threats the readings show, in rank order: kind, then the rival's influence, then system name. Systems in a conflict
    /// are left out, since the threat is already a conflict.
    /// </summary>
    public static IReadOnlyList<LeakThreat> Threats(LeakView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Report is not { } report)
        {
            return [];
        }

        var fighting = FactionConflictRows.Of(report, view.Standings).Select(conflict => conflict.System).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expansion = FactionStates.Display("Expansion");
        var threats = new List<LeakThreat>();
        var rising = new Dictionary<string, List<(string System, double Influence, double Gain)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var system in report.Systems.Where(system => !fighting.Contains(system.Name)))
        {
            foreach (var other in system.Others.Where(other => other.Influence is not null))
            {
                var series = Series(system.Name, other.Name, view);
                var influence = series.Count > 0 ? series[^1] : other.Influence!.Value;
                var gain = Gain(series);

                if (gain is { } rise)
                {
                    rising.TryAdd(other.Name, []);
                    rising[other.Name].Add((system.Name, influence, rise));

                    if (system.Controls && influence >= FrontThresholds.CoupApproach && influence < FrontThresholds.Coup)
                    {
                        threats.Add(new LeakThreat(LeakKind.Coup, system.Name, other.Name, influence, rise, [system.Name]));
                    }
                }

                if (other.PendingStates.Any(state => string.Equals(FactionStates.Display(state), expansion, StringComparison.Ordinal)))
                {
                    threats.Add(new LeakThreat(LeakKind.Expansion, system.Name, other.Name, influence, gain ?? 0, [system.Name]));
                }
            }
        }

        foreach (var (rival, places) in rising.Where(pair => pair.Value.Count >= SpreadSystems))
        {
            var ranked = places.OrderByDescending(place => place.Influence).ThenBy(place => place.System, StringComparer.OrdinalIgnoreCase).ToList();

            threats.Add(new LeakThreat(
                LeakKind.Spread,
                ranked[0].System,
                rival,
                ranked[0].Influence,
                ranked.Sum(place => place.Gain) / ranked.Count,
                [.. ranked.Select(place => place.System)]));
        }

        return
        [
            .. threats
                .OrderBy(threat => threat.Kind)
                .ThenByDescending(threat => threat.Influence)
                .ThenBy(threat => threat.System, StringComparer.OrdinalIgnoreCase)
                .ThenBy(threat => threat.Rival, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The Informant after one look, and the leak due, at most one. A threat not yet leaked is leaked; one already leaked is
    /// leaked again, a level higher, when the rival's influence has risen <see cref="EscalatesAt"/> since the last leak. A
    /// leaked threat the readings no longer show is forgotten, so a return of it is a new leak. With no report, nothing
    /// changes.
    /// </summary>
    public static LeakStep Step(CampaignInformant? informant, LeakView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Report is null)
        {
            return new LeakStep(informant, []);
        }

        var threats = Threats(view);
        var held = (informant?.Leaks ?? []).Where(leak => threats.Any(threat => Same(threat, leak))).ToList();
        var due = threats
            .Select(threat => (Threat: threat, Held: held.FirstOrDefault(leak => Same(threat, leak))))
            .FirstOrDefault(pair => pair.Held is null
                || (pair.Held.Level < MostLevels && pair.Threat.Influence >= pair.Held.Influence + EscalatesAt));

        if (due.Threat is null)
        {
            return new LeakStep(informant is null || held.Count == informant.Leaks.Count ? informant : informant with { Leaks = held }, []);
        }

        var level = due.Held is null ? 1 : due.Held.Level + 1;
        var given = new Leak(due.Threat.Kind, due.Threat.System, due.Threat.Rival, due.Threat.Influence, level, view.Now);
        var next = new CampaignInformant(
            informant?.Name ?? NameFor(view.SeedKey),
            informant?.Since ?? view.Now,
            [.. held.Where(leak => leak != due.Held), given]);
        var line = new CampaignArcLine(
            level == 1 ? ArcAct.Leak : ArcAct.Escalation,
            view.Faction,
            due.Threat.Rival,
            next.Name,
            due.Threat.System,
            Facts(due.Threat, due.Held, view.Faction));

        return new LeakStep(next, [line]);
    }

    /// <summary>
    /// The Informant's voice on the NPC voices' provider: a pool voice picked by the Informant's name, stepped past the
    /// Liaison's and the Nemesis's, heard through <see cref="Link"/>. Null with no voice to give.
    /// </summary>
    public static PinnedVoice? Voice(string informant, string faction, string? rival, SpeechSettings speech, Func<string, VoiceCast> casting)
    {
        ArgumentNullException.ThrowIfNull(informant);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(casting);

        var provider = VoiceGroups.ProviderFor(speech, VoiceGroups.Of(VoiceRole.Comms));

        if (string.Equals(provider, TtsProviderCatalog.NoneId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cast = casting(provider);
        var voice = cast.Lasting(informant) ?? cast.For(VoiceRole.Comms).VoiceId;
        var taken = new[]
            {
                CampaignLiaison.Voice(faction, speech, casting)?.VoiceId,
                rival is null ? null : CampaignArcs.Voice(rival, faction, speech, casting)?.VoiceId,
            }
            .OfType<string>()
            .ToList();

        for (var step = 0; step < cast.Pool.Count && taken.Contains(voice, StringComparer.OrdinalIgnoreCase); step++)
        {
            var at = cast.Pool.ToList().FindIndex(pooled => string.Equals(pooled, voice, StringComparison.OrdinalIgnoreCase));

            voice = cast.Pool[(Math.Max(at, 0) + 1) % cast.Pool.Count];
        }

        return voice is { Length: > 0 } ? new PinnedVoice(provider, voice) { Link = Link } : null;
    }

    private static bool Same(LeakThreat threat, Leak leak) =>
        threat.Kind == leak.Kind
        && string.Equals(threat.System, leak.System, StringComparison.OrdinalIgnoreCase)
        && string.Equals(threat.Rival, leak.Rival, StringComparison.OrdinalIgnoreCase);

    /// <summary>The rival's influence in a system over the readings held: the spans of the Commander's readings, else the previous and current reports.</summary>
    private static List<double> Series(string system, string rival, LeakView view)
    {
        var run = view.Standings.InfluenceRun(system, rival);

        if (run.Count >= 2)
        {
            return [.. run];
        }

        double? Reported(FactionReport? report) =>
            report?.Systems.FirstOrDefault(place => string.Equals(place.Name, system, StringComparison.OrdinalIgnoreCase))
                ?.Others.FirstOrDefault(other => string.Equals(other.Name, rival, StringComparison.OrdinalIgnoreCase))?.Influence;

        return Reported(view.Previous) is { } was && Reported(view.Report) is { } now ? [was, now] : [];
    }

    /// <summary>The rise across a series that is still rising, or null.</summary>
    private static double? Gain(List<double> series) =>
        series.Count >= 2 && series[^1] - series[0] >= MinimumGain && series[^1] >= series[^2] ? series[^1] - series[0] : null;

    private static List<string> Facts(LeakThreat threat, Leak? earlier, string faction)
    {
        var rival = threat.Rival;
        var facts = new List<string>();

        switch (threat.Kind)
        {
            case LeakKind.Coup:
                facts.Add($"{rival} is at {Percent(threat.Influence)} in {threat.System}, up {Points(threat.Gain)} on the earlier readings, and {faction} controls the system.");
                facts.Add($"At {Percent(FrontThresholds.Coup)} {rival} can take control of {threat.System} by a coup.");
                facts.Add($"No conflict has been declared in {threat.System}.");
                break;
            case LeakKind.Expansion:
                facts.Add($"{rival} has an Expansion pending in {threat.System}, where it stands at {Percent(threat.Influence)}.");
                facts.Add($"{faction} is present in {threat.System}.");
                break;
            default:
                facts.Add($"{rival} is gaining in {threat.Systems.Count} of the systems where {faction} is present: {string.Join(", ", threat.Systems)}.");
                facts.Add($"Its strongest hold of them is {threat.System}, at {Percent(threat.Influence)}.");
                break;
        }

        if (earlier is not null)
        {
            facts.Add($"An earlier leak put {rival} at {Percent(earlier.Influence)} in {threat.System}.");
        }

        return facts;
    }

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Points(double fraction)
    {
        var points = (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture);

        return points == "1" ? "1 point" : $"{points} points";
    }
}
