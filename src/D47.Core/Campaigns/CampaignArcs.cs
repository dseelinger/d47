using System.Globalization;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Campaigns;

/// <summary>What an arc's line is.</summary>
public enum ArcAct
{
    Introduction,
    Broadcast,
    Concession,
    Revenge,
    Leak,
    Escalation,
    Propaganda,
    Defection,
    Election,
    Argument,
    Sided,
    Crisis,
    Exploit,
    Relief,
    Legend,
}

/// <summary>A rival faction ranked for the Nemesis: conflicts fought, systems shared, and the closest influence gap in those systems.</summary>
public sealed record RivalScore(string Name, int Fought, int Shared, double? Gap);

/// <summary>What the arc engine reads: the campaign faction, the seed key, the faction's report, its fronts and the chronicle.</summary>
public sealed record ArcView(
    string Faction,
    string SeedKey,
    FactionReport? Report,
    IReadOnlyList<Front> Fronts,
    IReadOnlyList<ChronicleEntry> Chronicle,
    DateTimeOffset Now);

/// <summary>
/// One arc line before it is written: the act, the factions, the rival's leader, the system it is about and the facts, each
/// a sentence code decided.
/// </summary>
public sealed record CampaignArcLine(ArcAct Act, string Faction, string Rival, string Leader, string System, IReadOnlyList<string> Facts)
{
    /// <summary>The facts as one passage, which is also what is said when the model writes nothing.</summary>
    public string Text => string.Join(" ", Facts);
}

/// <summary>The arc after a step, and the lines due.</summary>
public sealed record ArcStep(CampaignArc? Arc, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>
/// Campaign arcs: fiction around the campaign's facts. Code picks the arc, its rival and when each act is due; every act
/// is pinned to a front or a chronicle entry, and the model only writes it. Pure: the caller supplies the time and keeps
/// the state.
/// </summary>
public static class CampaignArcs
{
    /// <summary>The link strength a Nemesis broadcast is heard through: an open broadcast picked up at range.</summary>
    public const double Link = 0.9;

    /// <summary>The rule every arc line keeps: fiction may be invented, game facts may not.</summary>
    public const string FictionRule =
        "You may invent your own manner, motives, history, documents and grudges. Every number, faction state, outcome and "
        + "place you mention comes from these facts: add no number, system, station, state or result you were not given.";

    private static readonly string[] Titles =
        ["Director", "Chancellor", "Commissioner", "Minister", "Overseer", "Prefect", "Administrator", "Envoy"];

    private static readonly string[] Surnames =
    [
        "Varga", "Okafor", "Halloran", "Lindqvist", "Moreau", "Tanaka", "Castellanos", "Volkov",
        "Achterberg", "Nakamura", "Ferreira", "Brannigan", "Kowalczyk", "Adeyemi", "Quist", "Marchetti",
        "Oyelaran", "Sandoval", "Thorne", "Kaur", "Ibarra", "Lindgren", "Mbeki", "Sorensen",
        "Drakos", "Haldane", "Nkemelu", "Petrakis", "Rusk", "Szabo", "Teague", "Ueda",
        "Valcourt", "Whitlock", "Yilmaz", "Zeller", "Abernathy", "Delacroix", "Morrow", "Calloway",
    ];

    /// <summary>
    /// The rival factions, ranked for the Nemesis: most conflicts fought (chronicle and current), then most systems shared,
    /// then the smallest influence gap in a shared system, then by name.
    /// </summary>
    public static IReadOnlyList<RivalScore> Rivals(string faction, FactionReport? report, IReadOnlyList<Front> fronts, IReadOnlyList<ChronicleEntry> chronicle)
    {
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(fronts);
        ArgumentNullException.ThrowIfNull(chronicle);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        names.UnionWith(chronicle.Select(entry => entry.Opponent).OfType<string>());
        names.UnionWith(fronts.Select(front => front.Opponent).OfType<string>());
        names.UnionWith(report?.Systems.SelectMany(system => system.Others).Select(other => other.Name) ?? []);
        names.RemoveWhere(name => string.IsNullOrWhiteSpace(name) || string.Equals(name, faction, StringComparison.OrdinalIgnoreCase));

        return
        [
            .. names
                .Select(name => Score(name, report, fronts, chronicle))
                .OrderByDescending(score => score.Fought)
                .ThenByDescending(score => score.Shared)
                .ThenBy(score => score.Gap ?? double.MaxValue)
                .ThenBy(score => score.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The leader invented for <paramref name="rival"/>, a title and a surname drawn from the rival's name and
    /// <paramref name="seedKey"/>, the same on every PC that has the same two.
    /// </summary>
    public static string Leader(string rival, string seedKey)
    {
        ArgumentNullException.ThrowIfNull(rival);
        ArgumentNullException.ThrowIfNull(seedKey);

        var hash = Hash($"{rival.Trim()}|{seedKey.Trim()}");

        return $"{Titles[hash % (uint)Titles.Length]} {Surnames[(hash >> 8) % (uint)Surnames.Length]}";
    }

    /// <summary>The Chronicler's name, a surname drawn from <paramref name="seedKey"/>, the same on every PC that has the same key.</summary>
    public static string Chronicler(string seedKey)
    {
        ArgumentNullException.ThrowIfNull(seedKey);

        return $"Chronicler {Surnames[(Hash($"chronicler|{seedKey.Trim()}") >> 8) % (uint)Surnames.Length]}";
    }

    /// <summary>The seed key: the squadron's key, so squadron members draw the same leader, else the campaign faction.</summary>
    public static string SeedKey(string? squadronKey, string faction) =>
        squadronKey is { Length: > 0 } key ? key : faction;

    /// <summary>
    /// The fronts <paramref name="rival"/> leads against the faction, in rank order: a conflict against it, a coup threat by
    /// it, or a thin margin over it.
    /// </summary>
    public static IReadOnlyList<Front> Against(string rival, IReadOnlyList<Front> fronts)
    {
        ArgumentNullException.ThrowIfNull(fronts);

        return
        [
            .. fronts.Where(front => front.Kind is FrontKind.Conflict or FrontKind.Coup or FrontKind.ThinMargin
                && string.Equals(front.Opponent, rival, StringComparison.OrdinalIgnoreCase)),
        ];
    }

    /// <summary>
    /// The arc after one look at <paramref name="now"/>, and the lines due. With no arc, the first of <see cref="Rivals"/>
    /// that leads a front against the faction becomes the Nemesis and is introduced. With one, a new chronicle entry the faction won against the rival is
    /// conceded the first time in that system and avenged after; and, against <paramref name="before"/>, the rival winning
    /// a day or taking the lead is broadcast. <paramref name="before"/> null is a first look, which broadcasts nothing.
    /// </summary>
    public static ArcStep Step(CampaignArc? arc, IReadOnlyList<Front>? before, ArcView now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (arc is null)
        {
            return Begin(now);
        }

        var lines = new List<CampaignArcLine>();

        for (var index = Math.Min(arc.Answered, now.Chronicle.Count); index < now.Chronicle.Count; index++)
        {
            var entry = now.Chronicle[index];

            if (entry.Outcome == "Won" && string.Equals(entry.Opponent, arc.Rival, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(Lost(arc, now, index));
            }
        }

        if (before is not null)
        {
            var was = Standing(arc.Rival, before);

            foreach (var (system, standing) in Standing(arc.Rival, now.Fronts))
            {
                var prior = was.GetValueOrDefault(system);

                if (standing.Conflict is { } conflict && prior?.TheirsWon is { } theirs && conflict.TheirsWon > theirs)
                {
                    lines.Add(GainedDay(arc, now, standing.Front, conflict));
                }
                else if (standing.Lead is < 0 and var lead && prior?.Lead is not < 0)
                {
                    lines.Add(TookLead(arc, now, standing.Front, lead));
                }
            }
        }

        return new ArcStep(arc with { Answered = now.Chronicle.Count }, lines);
    }

    /// <summary>The brief the model writes an arc line from: the leader, the facts and the fiction rule.</summary>
    public static FlavourBrief Brief(CampaignArcLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new FlavourBrief
        {
            Speaker = SpeakerOf(line),
            Instruction =
                $"{Asking(line.Act)} The facts: {line.Text} {FictionRule} Say it in two or three short sentences, as yourself. "
                + "Mention no game, developer or AI.",
            NeedsPersona = false,
            NeedsGameState = false,
            NeedsAboutMe = false,
            NeedsScenario = false,
        };
    }

    /// <summary>
    /// The Nemesis's voice on the NPC voices' provider: a pool voice picked by the rival's name, the same in every session,
    /// stepped on to the next when it is the Liaison's. Heard through a broadcast link. Null with no voice to give.
    /// </summary>
    public static PinnedVoice? Voice(string rival, string faction, SpeechSettings speech, Func<string, VoiceCast> casting)
    {
        ArgumentNullException.ThrowIfNull(rival);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(casting);

        var provider = VoiceGroups.ProviderFor(speech, VoiceGroups.Of(VoiceRole.Comms));

        if (string.Equals(provider, TtsProviderCatalog.NoneId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cast = casting(provider);
        var voice = cast.Lasting(rival) ?? cast.For(VoiceRole.Comms).VoiceId;

        if (CampaignLiaison.Voice(faction, speech, casting)?.VoiceId is { } liaison
            && string.Equals(voice, liaison, StringComparison.OrdinalIgnoreCase)
            && cast.Pool.Count > 1)
        {
            var at = cast.Pool.ToList().FindIndex(pooled => string.Equals(pooled, liaison, StringComparison.OrdinalIgnoreCase));

            voice = cast.Pool[(Math.Max(at, 0) + 1) % cast.Pool.Count];
        }

        return voice is { Length: > 0 } ? new PinnedVoice(provider, voice) { Link = Link } : null;
    }

    private static ArcStep Begin(ArcView now)
    {
        var leading = Rivals(now.Faction, now.Report, now.Fronts, now.Chronicle)
            .Select(score => (score.Name, Front: Against(score.Name, now.Fronts).FirstOrDefault()))
            .FirstOrDefault(candidate => candidate.Front is not null);

        if (leading is not (string rival, Front front))
        {
            return new ArcStep(null, []);
        }

        var arc = new CampaignArc(ArcKind.Nemesis, rival, Leader(rival, now.SeedKey), now.Now, now.Chronicle.Count);
        var facts = new List<string>
        {
            $"{arc.Leader} leads {rival}.",
            $"{rival} opposes {now.Faction} in the {CampaignLiaison.Named(front)} in {front.System}.",
            front.Reason,
        };

        Grudge(facts, arc, now, front.System, Contested(arc.Rival, now.Chronicle, front.System, now.Chronicle.Count) + 1);

        return new ArcStep(arc, [new CampaignArcLine(ArcAct.Introduction, now.Faction, rival, arc.Leader, front.System, facts)]);
    }

    private static CampaignArcLine Lost(CampaignArc arc, ArcView now, int index)
    {
        var entry = now.Chronicle[index];
        var count = Contested(arc.Rival, now.Chronicle, entry.System, index + 1);
        var facts = new List<string> { $"{now.Faction} won the {entry.Kind.ToLowerInvariant()} in {entry.System} against {arc.Rival}." };

        Grudge(facts, arc, now, entry.System, count);

        return new CampaignArcLine(count > 1 ? ArcAct.Revenge : ArcAct.Concession, now.Faction, arc.Rival, arc.Leader, entry.System, facts);
    }

    private static CampaignArcLine GainedDay(CampaignArc arc, ArcView now, Front front, FactionConflict conflict)
    {
        var facts = new List<string>
        {
            $"{arc.Rival} has won a day in the {CampaignLiaison.Named(front)} in {front.System}.",
            $"Days won stand at {conflict.TheirsWon} for {arc.Rival} and {conflict.OursWon} for {now.Faction}.",
        };

        Grudge(facts, arc, now, front.System, Contested(arc.Rival, now.Chronicle, front.System, now.Chronicle.Count) + 1);

        return new CampaignArcLine(ArcAct.Broadcast, now.Faction, arc.Rival, arc.Leader, front.System, facts);
    }

    private static CampaignArcLine TookLead(CampaignArc arc, ArcView now, Front front, double lead)
    {
        var facts = new List<string> { $"{arc.Rival} is now ahead of {now.Faction} in {front.System}, by {Points(-lead)}." };

        Grudge(facts, arc, now, front.System, Contested(arc.Rival, now.Chronicle, front.System, now.Chronicle.Count) + 1);

        return new CampaignArcLine(ArcAct.Broadcast, now.Faction, arc.Rival, arc.Leader, front.System, facts);
    }

    /// <summary>Adds the grudge, by count, when the two factions have contested <paramref name="system"/> before.</summary>
    private static void Grudge(List<string> facts, CampaignArc arc, ArcView now, string system, int count)
    {
        if (count > 1)
        {
            facts.Add($"This is the {Ordinal(count)} time {arc.Rival} and {now.Faction} have contested {system}.");
        }
    }

    /// <summary>The chronicle entries among the first <paramref name="upTo"/> fought against <paramref name="rival"/> in <paramref name="system"/>.</summary>
    private static int Contested(string rival, IReadOnlyList<ChronicleEntry> chronicle, string system, int upTo) =>
        chronicle.Take(upTo).Count(entry => string.Equals(entry.Opponent, rival, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.System, system, StringComparison.OrdinalIgnoreCase));

    private sealed record RivalStanding(Front Front, FactionConflict? Conflict, double? Lead)
    {
        public int? TheirsWon => Conflict is { Visited: true } visited ? visited.TheirsWon : null;
    }

    /// <summary>By system, where the rival stands on the fronts it leads: days it has won in a visited conflict, and the faction's lead over it.</summary>
    private static Dictionary<string, RivalStanding> Standing(string rival, IReadOnlyList<Front> fronts)
    {
        var standing = new Dictionary<string, RivalStanding>(StringComparer.OrdinalIgnoreCase);

        foreach (var front in Against(rival, fronts))
        {
            var held = standing.GetValueOrDefault(front.System);

            standing[front.System] = front.Kind == FrontKind.Conflict
                ? new RivalStanding(front, front.Conflict is { Visited: true } conflict ? conflict : null, held?.Lead)
                : new RivalStanding(held?.Front ?? front, held?.Conflict, front.Lead);
        }

        return standing;
    }

    private static RivalScore Score(string name, FactionReport? report, IReadOnlyList<Front> fronts, IReadOnlyList<ChronicleEntry> chronicle)
    {
        var fought = chronicle.Count(entry => string.Equals(entry.Opponent, name, StringComparison.OrdinalIgnoreCase))
            + fronts.Count(front => front.Kind == FrontKind.Conflict && string.Equals(front.Opponent, name, StringComparison.OrdinalIgnoreCase));

        var shared = (report?.Systems ?? [])
            .Select(system => (Ours: system.Influence, Theirs: system.Others.FirstOrDefault(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase))))
            .Where(pair => pair.Theirs is not null)
            .ToList();

        var gaps = shared
            .Where(pair => pair.Ours is not null && pair.Theirs!.Influence is not null)
            .Select(pair => Math.Abs(pair.Ours!.Value - pair.Theirs!.Influence!.Value))
            .ToList();

        return new RivalScore(name, fought, shared.Count, gaps.Count == 0 ? null : gaps.Min());
    }

    private static string SpeakerOf(CampaignArcLine line) => line.Act switch
    {
        ArcAct.Leak or ArcAct.Escalation =>
            $"You are {line.Leader}, an informant inside {line.Rival}, a minor faction and the rival of {line.Faction}. You speak "
            + $"over an encrypted line to the Commander, an independent pilot working for {line.Faction}, and you are afraid of being traced.",
        ArcAct.Defection =>
            $"You are a member of {line.Rival}, a minor faction and the rival of {line.Faction}, who has defected to {line.Faction}. You "
            + "speak over a hailing frequency to the Commander, an independent pilot working for it.",
        ArcAct.Election =>
            $"You are the Liaison of {line.Faction}, a minor faction. You speak to the Commander, an independent pilot working for it, "
            + $"for the idea of rule that {line.Faction}'s government stands for.",
        ArcAct.Crisis or ArcAct.Relief =>
            $"You are the Liaison of {line.Faction}, a minor faction. You speak to the Commander, an independent pilot working for it, "
            + $"about a crisis in one of {line.Faction}'s systems.",
        ArcAct.Exploit =>
            $"You are {line.Leader}, who leads {line.Rival}, a minor faction and the rival of {line.Faction}. You speak on "
            + $"an open broadcast that the Commander, an independent pilot working for {line.Faction}, picks up, and you are taking advantage of a crisis in {line.Faction}'s system.",
        ArcAct.Legend =>
            $"You are {line.Leader}, the Chronicler of {line.Faction}, a minor faction. You write its history, and the Commander, an independent pilot working for it, appears in it.",
        ArcAct.Argument or ArcAct.Sided =>
            $"You are {line.Leader}, a councillor of {line.Faction}, a minor faction. You speak to the Commander, an independent "
            + $"pilot working for {line.Faction}.",
        _ =>
            $"You are {line.Leader}, who leads {line.Rival}, a minor faction and the rival of {line.Faction}. You speak on "
            + $"an open broadcast that the Commander, an independent pilot working for {line.Faction}, picks up.",
    };

    private static string Asking(ArcAct act) => act switch
    {
        ArcAct.Propaganda => "Broadcast propaganda for your government's idea of rule against the Commander's faction's, in your own words.",
        ArcAct.Defection => "Say why you have left, in your own words, and what you bring.",
        ArcAct.Election => "Frame the election as a contest between two ideas of rule, and speak for your government's idea.",
        ArcAct.Argument => "Argue your case for the front you urge, in your own words.",
        ArcAct.Crisis => "Tell the Commander the crisis has landed, who is blamed, that a relief effort has begun and that refugees are leaving, in your own words.",
        ArcAct.Exploit => "Gloat over the crisis and say how you mean to profit from it.",
        ArcAct.Relief => "Tell the Commander the crisis is over and what the relief effort achieved, in your own words.",
        ArcAct.Legend => "Write this entry of the faction's history.",
        ArcAct.Sided => "Say what you make of being passed over, in your own words, and that you will remember it.",
        ArcAct.Introduction => "Introduce yourself to the Commander as the rival they now face.",
        ArcAct.Leak => "Deliver the intercepted plan to the Commander, as a warning, in your own words.",
        ArcAct.Escalation => "Warn the Commander that the plan has moved on since your last message, and what it now threatens.",
        ArcAct.Broadcast => "Taunt the Commander over your gain.",
        ArcAct.Concession => "Concede the defeat, with what grace you can find.",
        _ => "Vow revenge for the defeat.",
    };

    internal static string Ordinal(int count) => count switch
    {
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        9 => "ninth",
        10 => "tenth",
        _ => count.ToString(CultureInfo.InvariantCulture) + (count % 10 == 1 && count % 100 != 11 ? "st" : count % 10 == 2 && count % 100 != 12 ? "nd" : count % 10 == 3 && count % 100 != 13 ? "rd" : "th"),
    };

    private static string Points(double fraction)
    {
        var points = (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture);

        return points == "1" ? "1 point" : $"{points} points";
    }

    /// <summary>FNV-1a over the upper-cased text, the same in every process.</summary>
    internal static uint Hash(string text)
    {
        var hash = 2166136261u;

        foreach (var character in text)
        {
            hash = (hash ^ char.ToUpperInvariant(character)) * 16777619u;
        }

        return hash;
    }
}
