using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Campaigns;

/// <summary>What the campaign Liaison says a line about.</summary>
public enum LiaisonBeat
{
    Briefing,
    Dispatch,
    MorningReport,
    HandIn,
    FirstDayWon,
    MarginRestored,
    Resolution,
    Conflicts,
}

/// <summary>
/// One Liaison line before it is written: the beat, the faction, the Liaison's part and manner, and the facts, each a
/// sentence code decided. <paramref name="Conflict"/> marks a line about a conflict, which is measured in days won only.
/// </summary>
public sealed record CampaignLiaisonLine(
    LiaisonBeat Beat,
    string Faction,
    string Part,
    string Manner,
    IReadOnlyList<string> Facts,
    bool Conflict)
{
    /// <summary>The systems a rewrite must name, or none.</summary>
    public IReadOnlyList<string> Systems { get; init; } = [];

    /// <summary>The facts as one passage, which is also what is said when the model writes nothing.</summary>
    public string Text => string.Join(" ", Facts);
}

/// <summary>The facts of each Liaison beat, the brief the model writes a line from, and the Liaison's voice.</summary>
public static partial class CampaignLiaison
{
    /// <summary>Who the line is credited to.</summary>
    public const string Speaker = "Liaison";

    /// <summary>The link strength the Liaison is heard through: a clear comms link.</summary>
    public const double Link = 1;

    /// <summary>The rule a dispatch line keeps: the Commander's work and the movement are never set against each other.</summary>
    public const string NoShareRule =
        "Keep what the Commander did and what moved as two separate facts: never say how much of the change the Commander "
        + "caused, and give no share, ratio or percentage of it.";

    /// <summary>The rule a conflict line keeps.</summary>
    public const string DaysWonRule = "Measure the conflict in days won and in nothing else.";

    /// <summary>The Liaison's part on <paramref name="front"/>: field officer in a war, campaign manager in an election, relief coordinator in a bad state.</summary>
    public static string PartFor(Front? front) => front?.Kind switch
    {
        FrontKind.Conflict when IsElection(front) => "campaign manager",
        FrontKind.Conflict => "field officer",
        FrontKind.BadState => "relief coordinator",
        _ => "liaison officer",
    };

    /// <summary>The Liaison's manner under the faction's <paramref name="government"/>, as the galaxy report names it.</summary>
    public static string MannerFor(string? government)
    {
        var name = (government ?? string.Empty).Replace("$government_", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd(';');

        return name.ToUpperInvariant() switch
        {
            "DEMOCRACY" => "measured and civic; you speak of voters and the public good",
            "DICTATORSHIP" => "clipped and commanding; you expect orders to be carried out",
            "CORPORATE" => "brisk and businesslike; you speak of returns, contracts and markets",
            "PATRONAGE" => "courtly and mindful of favours; you speak of loyalty and patrons",
            "FEUDAL" => "formal and honour-bound; you speak of duty and fealty",
            "COMMUNISM" => "earnest and collective; you speak of the people and the plan",
            "COOPERATIVE" => "warm and consensual; you speak of members and common effort",
            "CONFEDERACY" => "plain-spoken and independent-minded",
            "THEOCRACY" => "solemn and devout",
            "ANARCHY" => "rough and informal",
            "PRISON" or "PRISON COLONY" or "PRISONCOLONY" => "terse and guarded",
            _ => "professional and courteous",
        };
    }

    /// <summary>The briefing when a front is chosen: the situation, the margin, what is at stake and the remedy, with what does not count.</summary>
    public static CampaignLiaisonLine Briefing(Front front, CampaignNextAction action, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(action);

        var facts = new List<string> { $"The front is the {Named(front)} in {front.System}.", front.Reason };

        if (front.Kind is FrontKind.Retreat or FrontKind.BadState or FrontKind.Expansion && front.Margin is { } lead)
        {
            facts.Add($"{faction}'s lead over the nearest rival there is {Points(lead)}.");
        }

        if (front.Stake.Length == 0 && front.Controls)
        {
            facts.Add($"{faction} controls {front.System}.");
        }

        facts.Add(action.Sentence);

        if (action.NotCounting is { Length: > 0 } notCounting)
        {
            facts.Add(Unreferenced(notCounting));
        }

        return Line(LiaisonBeat.Briefing, front, faction, government, facts);
    }

    /// <summary>
    /// The dispatch after <paramref name="tick"/>, as two separate facts: the Commander's work for the faction in the front's
    /// system since the tick, and what the newest reading shows moved, with its age. A conflict gives days won.
    /// </summary>
    public static CampaignLiaisonLine Dispatch(
        Front front, string faction, BgsWork work, SystemStandings standings, DateTimeOffset tick, DateTimeOffset now, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(standings);

        var done = WorkIn(work, faction, front.System, tick);
        var facts = new List<string>
        {
            done.Count == 0
                ? $"Since the tick you have handed in nothing for {faction} in {front.System}."
                : $"Since the tick you have {Described(done)} for {faction} in {front.System}.",
        };

        if (front.Kind == FrontKind.Conflict)
        {
            facts.Add(standings.Latest(front.System) is { } reading
                && reading.Conflicts.FirstOrDefault(conflict => Involves(conflict, faction)) is { } conflict
                    ? $"By your reading from {Age(now - reading.SeenAt)} ago, days won stand at {Ours(conflict, faction)} to {Theirs(conflict, faction)}."
                    : $"Your newest reading of {front.System} shows no conflict for {faction}.");
        }
        else if (standings.InfluenceAcross(front.System, faction, tick) is { } across)
        {
            facts.Add(
                $"By your reading from {Age(now - across.LastSeen)} ago, {faction}'s influence in {front.System} moved from "
                + $"{Percent(across.Before)} before the tick to {Percent(across.After)}.");
        }
        else if (standings.Latest(front.System) is { } reading && reading.InfluenceOf(faction) is { } influence)
        {
            facts.Add(
                $"By your reading from {Age(now - reading.SeenAt)} ago, {faction}'s influence in {front.System} is {Percent(influence)}; "
                + "there is no reading from before the tick to set it against.");
        }

        return Line(LiaisonBeat.Dispatch, front, faction, government, facts);
    }

    /// <summary>The request to fly in after a tick that no reading of the front's system is newer than.</summary>
    public static CampaignLiaisonLine MorningReport(Front front, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);

        return Line(
            LiaisonBeat.MorningReport,
            front,
            faction,
            government,
            [$"The BGS tick has passed, and there is no reading of {front.System} newer than it.", $"Fly in to {front.System} for the morning report."]);
    }

    /// <summary>The acknowledgement of work handed in for the faction on the front.</summary>
    public static CampaignLiaisonLine HandIn(Front front, IReadOnlyList<BgsEntry> handedIn, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(handedIn);

        return Line(LiaisonBeat.HandIn, front, faction, government, [$"You have just {Described(handedIn)} for {faction} in {front.System}."]);
    }

    /// <summary>The acknowledgement of the faction's first day won in a conflict.</summary>
    public static CampaignLiaisonLine FirstDayWon(Front front, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);

        var conflict = front.Conflict;

        return Line(
            LiaisonBeat.FirstDayWon,
            front,
            faction,
            government,
            [
                $"{faction} has won its first day in the {Named(front)} in {front.System}.",
                $"Days won stand at {conflict?.OursWon ?? 1} to {conflict?.TheirsWon ?? 0}.",
            ]);
    }

    /// <summary>The acknowledgement of a margin back within its threshold.</summary>
    public static CampaignLiaisonLine MarginRestored(Front front, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(front);

        var rival = front.Opponent is { Length: > 0 } named ? named : "the nearest rival";
        var fact = front.Kind switch
        {
            FrontKind.Conflict => $"{faction} is level or ahead again in days won in {front.System}: {front.Conflict?.OursWon} to {front.Conflict?.TheirsWon}.",
            FrontKind.Coup => $"{rival} is back under the {Percent(FrontThresholds.Coup)} at which it can take {front.System} by a coup.",
            FrontKind.UnwantedExpansion => $"{faction}'s influence in {front.System} is back under {Percent(FrontThresholds.Expansion)}.",
            _ => $"{faction} is ahead of {rival} in {front.System} again.",
        };

        return Line(LiaisonBeat.MarginRestored, front, faction, government, [fact]);
    }

    /// <summary>
    /// The outcome of a resolved front, then the next front proposed, or that there is none. <paramref name="resolved"/> is the
    /// front as it was last seen, for the Liaison's part; null when it was not seen.
    /// </summary>
    public static CampaignLiaisonLine Resolution(ChronicleEntry entry, Front? resolved, Front? next, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var what = resolved is not null ? Named(resolved) : entry.Kind.ToLowerInvariant();
        var facts = new List<string>
        {
            $"The {what} in {entry.System} is over.",
            entry.Outcome switch
            {
                "Won" => $"{faction} won it.",
                "Lost" => entry.Opponent is { Length: > 0 } opponent ? $"{faction} lost it to {opponent}." : $"{faction} lost it.",
                "Ended" => "It ended with neither side ahead.",
                _ => "It is resolved.",
            },
            next is null
                ? "There is no other front to work."
                : $"The next front proposed is the {Named(next)} in {next.System}: {next.Reason}",
        };

        return new CampaignLiaisonLine(LiaisonBeat.Resolution, faction, PartFor(resolved), MannerFor(government), facts, Conflict: false);
    }

    /// <summary>
    /// The briefing of every conflict the faction is in: wars and civil wars, then elections, active before pending within
    /// each. Each states its type, system, status and opponent, with days won when the Commander has read the system and
    /// the faction's stake when it has one.
    /// </summary>
    public static CampaignLiaisonLine Conflicts(IReadOnlyList<FactionConflict> conflicts, string faction, string? government)
    {
        ArgumentNullException.ThrowIfNull(conflicts);

        var ordered = conflicts
            .OrderBy(conflict => IsElection(conflict.Type))
            .ThenByDescending(conflict => conflict.Active)
            .ToList();

        return new CampaignLiaisonLine(
            LiaisonBeat.Conflicts,
            faction,
            "liaison officer",
            MannerFor(government),
            [.. ordered.Select(conflict => ConflictFact(conflict, faction))],
            Conflict: true)
        {
            Systems = [.. ordered.Select(conflict => conflict.System).Distinct(StringComparer.OrdinalIgnoreCase)],
        };
    }

    private static string ConflictFact(FactionConflict conflict, string faction)
    {
        var type = conflict.Type.ToLowerInvariant();
        var status = conflict.Active ? "active" : "pending";
        var fact = conflict.Kind == OpponentKind.Named
            ? $"In {conflict.System}: {status} {type} against {conflict.Opponent}."
            : $"In {conflict.System}: {status} {type}; opponent not yet known.";

        if (conflict.Visited)
        {
            fact += $" Days won stand at {conflict.OursWon} to {conflict.TheirsWon}.";
        }

        return conflict.OurStake.Length > 0 ? $"{fact} {faction}'s stake is {conflict.OurStake}." : fact;
    }

    private static bool IsElection(string type) => type.Contains("election", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether work of <paramref name="kind"/> helps the faction on <paramref name="front"/>, by the rules its next action gives.</summary>
    public static bool Counts(Front front, BgsWorkKind kind, long amount)
    {
        ArgumentNullException.ThrowIfNull(front);

        if (kind == BgsWorkKind.Mission && amount <= 0)
        {
            return false;
        }

        return front.Kind switch
        {
            FrontKind.Conflict when IsElection(front) => kind is BgsWorkKind.Mission or BgsWorkKind.Trade or BgsWorkKind.Cartography,
            FrontKind.Conflict => kind is BgsWorkKind.Mission or BgsWorkKind.CombatBond,
            FrontKind.BadState when front.States.Contains("Lockdown", StringComparer.OrdinalIgnoreCase) => kind == BgsWorkKind.Bounty,
            FrontKind.BadState when front.States.Contains("Outbreak", StringComparer.OrdinalIgnoreCase) => kind != BgsWorkKind.Bounty,
            FrontKind.UnwantedExpansion or FrontKind.Expansion => false,
            _ => true,
        };
    }

    /// <summary>The brief the model writes a Liaison line from: the part, the manner, the facts and the rules the line keeps.</summary>
    public static FlavourBrief Brief(CampaignLiaisonLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var rules = new List<string>
        {
            "Use these facts and no others: add no number, name, place or event you were not given.",
        };

        if (line.Beat == LiaisonBeat.Dispatch)
        {
            rules.Add(NoShareRule);
        }

        if (line.Conflict)
        {
            rules.Add(DaysWonRule);
        }

        return new FlavourBrief
        {
            Speaker =
                $"You are the {line.Part} for {line.Faction}, a minor faction, speaking over comms to the Commander, an "
                + $"independent pilot working for the faction. Your manner is {line.Manner}.",
            Instruction =
                $"{Asking(line.Beat)} The facts, which are all you know: {line.Text} {string.Join(" ", rules)} Say it in two or "
                + "three short sentences, as yourself. Mention no game, developer or AI.",
            NeedsPersona = false,
            NeedsGameState = false,
            NeedsAboutMe = false,
            NeedsScenario = false,
        };
    }

    /// <summary>
    /// The Liaison's voice on the NPC voices' provider: the Commander's choice while that provider offers it, else a voice
    /// picked by the faction's name, the same in every session. Heard through a clear comms link. Null with no voice to give.
    /// </summary>
    public static PinnedVoice? Voice(string faction, SpeechSettings speech, Func<string, VoiceCast> casting)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(casting);

        var provider = VoiceGroups.ProviderFor(speech, VoiceGroups.Of(VoiceRole.Comms));

        if (string.Equals(provider, TtsProviderCatalog.NoneId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cast = casting(provider);
        var voice = speech.LiaisonVoice is { Length: > 0 } chosen
                    && (cast.Pool.Count == 0 || cast.Pool.Contains(chosen, StringComparer.OrdinalIgnoreCase))
            ? chosen
            : cast.Lasting(faction) ?? cast.For(VoiceRole.Comms).VoiceId;

        return voice is { Length: > 0 } ? new PinnedVoice(provider, voice) { Link = Link } : null;
    }

    /// <summary>The faction's BGS work in <paramref name="system"/> since <paramref name="since"/>.</summary>
    public static IReadOnlyList<BgsEntry> WorkIn(BgsWork work, string faction, string system, DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(work);

        return
        [
            .. work.Entries.Where(entry => entry.At >= since
                && string.Equals(entry.Faction, faction, StringComparison.OrdinalIgnoreCase)
                && string.Equals(work.NameOf(entry.SystemAddress), system, StringComparison.OrdinalIgnoreCase)),
        ];
    }

    private static CampaignLiaisonLine Line(LiaisonBeat beat, Front front, string faction, string? government, IReadOnlyList<string> facts) =>
        new(beat, faction, PartFor(front), MannerFor(government), facts, front.Kind == FrontKind.Conflict);

    private static string Asking(LiaisonBeat beat) => beat switch
    {
        LiaisonBeat.Briefing =>
            "The Commander has just taken on this front. Brief them: the situation, the margin, what is at stake and what to do, "
            + "including what does not count.",
        LiaisonBeat.Dispatch => "The BGS tick has passed. Give the Commander the morning dispatch.",
        LiaisonBeat.MorningReport => "The BGS tick has passed and you have no fresh reading. Ask the Commander to fly in for the morning report.",
        LiaisonBeat.HandIn => "Acknowledge what the Commander has just handed in, briefly.",
        LiaisonBeat.FirstDayWon => "Acknowledge the first day won, briefly.",
        LiaisonBeat.MarginRestored => "Acknowledge that the margin is restored, briefly.",
        LiaisonBeat.Conflicts => "Give the Commander the daily briefing on the faction's conflicts. Name every system.",
        _ => "The front is over. Give the outcome, then the next front proposed.",
    };

    /// <summary>The work as a phrase: missions for and against, and the credits of each other kind.</summary>
    private static string Described(IReadOnlyList<BgsEntry> entries)
    {
        var parts = new List<string>();
        var helped = entries.Count(entry => entry is { Kind: BgsWorkKind.Mission, Amount: > 0 });
        var hurt = entries.Count(entry => entry is { Kind: BgsWorkKind.Mission, Amount: < 0 });

        if (helped > 0)
        {
            parts.Add($"completed {Count(helped, "mission")}");
        }

        if (hurt > 0)
        {
            parts.Add($"had {Count(hurt, "mission")} count against the faction");
        }

        void Add(BgsWorkKind kind, string phrase)
        {
            var credits = entries.Where(entry => entry.Kind == kind).Sum(entry => entry.Amount);

            if (credits > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, phrase, Credits(credits)));
            }
        }

        Add(BgsWorkKind.Bounty, "redeemed {0} in bounties");
        Add(BgsWorkKind.CombatBond, "redeemed {0} in combat bonds");
        Add(BgsWorkKind.Trade, "sold {0} of cargo");
        Add(BgsWorkKind.Cartography, "sold {0} of exploration data");

        return parts.Count switch
        {
            0 => "handed in nothing",
            1 => parts[0],
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
        };
    }

    internal static string Named(Front front) =>
        front.Kind == FrontKind.Conflict && front.Conflict is { Type.Length: > 0 } conflict
            ? conflict.Type.ToLowerInvariant()
            : CampaignFronts.Describe(front.Kind).ToLowerInvariant();

    private static bool IsElection(Front? front) =>
        front?.Conflict?.Type.Contains("election", StringComparison.OrdinalIgnoreCase) == true;

    private static bool Involves(SystemConflict conflict, string faction) =>
        string.Equals(conflict.Faction1.Name, faction, StringComparison.OrdinalIgnoreCase)
        || string.Equals(conflict.Faction2.Name, faction, StringComparison.OrdinalIgnoreCase);

    private static int Ours(SystemConflict conflict, string faction) =>
        string.Equals(conflict.Faction1.Name, faction, StringComparison.OrdinalIgnoreCase) ? conflict.Faction1.WonDays : conflict.Faction2.WonDays;

    private static int Theirs(SystemConflict conflict, string faction) =>
        string.Equals(conflict.Faction1.Name, faction, StringComparison.OrdinalIgnoreCase) ? conflict.Faction2.WonDays : conflict.Faction1.WonDays;

    /// <summary>A not-counting sentence without its guide page reference, which is not for saying aloud.</summary>
    private static string Unreferenced(string sentence) => GuideReference().Replace(sentence, string.Empty);

    [GeneratedRegex(@"\s*\(guide p\.\s*\d+\)")]
    private static partial Regex GuideReference();

    private static string Age(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "under a minute",
        < 60 => Count((int)age.TotalMinutes, "minute"),
        < 48 * 60 => Count((int)Math.Round(age.TotalHours), "hour"),
        _ => Count((int)age.TotalDays, "day"),
    };

    private static string Count(int count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")}";

    private static string Credits(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " credits";

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Points(double fraction)
    {
        var points = (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture);

        return points == "1" ? "1 point" : $"{points} points";
    }
}
