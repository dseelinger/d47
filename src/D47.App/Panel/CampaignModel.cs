using System.Globalization;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>One faction the Campaign faction picker offers, with the line under its name.</summary>
public sealed record CampaignFactionChoice(string Name, string Detail);

/// <summary>The words, figures and columns of Commander › BGS.</summary>
public static class CampaignModel
{
    public const string Dash = "—";

    public const string NoFactionText = "No BGS faction yet. Choose one, or say 'our BGS faction is' and its name.";

    public const string OffLabel = "BGS OFF";

    public const string OffSwitchLabel = "BGS ON";

    public const string NotWhileOff = "Not while off";

    public const string OffBody =
        "D47 fetches no faction report, proposes no front and puts nothing on the NEXT strip. "
        + "The Liaison, the arcs and the BGS warnings are quiet.";

    public const string OffKept =
        "Your faction, the intent on each system and the chronicle are kept. Turn BGS on and D47 picks up from them.";

    public const string OffHint = "Tick BGS ON, or say \"turn on BGS\".";

    /// <summary>The faction an off campaign would resume with: the one named, else the squadron's, else null.</summary>
    public static string? OffFaction(Campaign campaign, string? squadronFaction) =>
        campaign.Faction is { Length: > 0 } named ? named : squadronFaction is { Length: > 0 } squadron ? squadron : null;

    /// <summary>The first sentence of the off block and the header summary.</summary>
    public static string OffOpening(string? faction) =>
        faction is null ? "Your BGS work is off." : $"Your BGS work for {faction} is off.";

    /// <summary>The off block's first line on the desktop.</summary>
    public static string OffText(string? faction) => OffOpening(faction) + " " + OffBody;

    /// <summary>The off block's second and third lines joined, as the headset draws them.</summary>
    public static string OffCompact => OffKept + " " + OffHint;

    /// <summary>The header summary for the page.</summary>
    public static string Summary(string? faction, bool off, bool headset) =>
        off ? OffOpening(faction)
        : faction is null ? "Choose the faction your BGS work is for."
        : headset ? $"Your BGS work for {faction}."
        : $"Your BGS work for {faction}: the front you chose, and what to do next.";

    /// <summary>The line under the top line when no faction is known; none while the campaign is off.</summary>
    public static string? NoFactionLine(string? faction, bool off) => faction is null && !off ? NoFactionText : null;

    /// <summary>The faction tile's label; CHOOSE FACTION only when the campaign is on and has no faction.</summary>
    public static string FactionTileLabel(string? faction, bool off) => faction is null && !off ? "CHOOSE FACTION" : "CHANGE FACTION";

    public const string NoReportText = "No faction report yet. D47 fetches it after the next tick.";

    public const string NoSystemText = "No system of the campaign faction has that name.";

    public const string Legend =
        "Intents with a bar like this one are protected. You set them here or by a phrase such as \"hold\" or "
        + "\"grow\" and the system's name; D47's model cannot change them.";

    public const string Hint =
        "The six intents in order: Hold, Grow, Expand from here, Stay under 75%, Take control, Leave alone. A system "
        + "you have not set follows the default in grey: Hold where the faction controls it, Leave alone where it does not.";

    public const string PickerHelp =
        "Your BGS work starts with your squadron's faction. Choose any faction D47 has seen in your journal, or type a "
        + "name; its report comes from the same source as Search › Faction.";

    /// <summary>The sections under the frame, in order.</summary>
    public static readonly IReadOnlyList<string> Sections = ["FRONTS", "SYSTEMS", "WORK", "CHRONICLE"];

    public const int FrontsSection = 0;
    public const int SystemsSection = 1;
    public const int WorkSection = 2;
    public const int ChronicleSection = 3;

    public const string NoFrontsText = "The faction has no front that needs work.";

    public const string NoChronicleText = "No front has resolved yet.";

    public const string NoMatchingFrontText = "No front is in a system with that name.";

    public const string ProposalNote = "D47 proposes the top front after each tick. Nothing is chosen until you take it.";

    public const string ChronicleHint =
        "A front enters the chronicle when fresh data shows it resolved. Each entry says how it ended and what you did "
        + "while it ran. D47 sees only your own work, so it claims no share of the outcome.";

    public const string FailedLabel = "Faction report not fetched";

    public const string OldLabel = "OLD REPORT";

    /// <summary>The phrases beside the chosen front's tiles.</summary>
    public const string ChosenPhrases = "Or say \"next front\", \"drop the front\" or \"what should I do for the faction?\"";

    /// <summary>One front with its place in the ranking and its system's distance in light years, null when unknown.</summary>
    public sealed record FrontRow(int Rank, Front Front, bool Chosen, double? Distance);

    /// <summary>One resolved front as the Chronicle section draws it.</summary>
    public sealed record ChronicleRow(ChronicleEntry Entry, DateTimeOffset? Ended);

    /// <summary>The fronts in rank order, each with its system's distance from <paramref name="distances"/>; the one being worked is marked chosen.</summary>
    public static IReadOnlyList<FrontRow> FrontRows(IReadOnlyList<Front> fronts, ChosenFront? chosen, IReadOnlyDictionary<string, double> distances) =>
    [
        .. fronts.Select((front, index) => new FrontRow(
            index + 1,
            front,
            chosen is not null && chosen.Kind == front.Kind && string.Equals(chosen.System, front.System, StringComparison.OrdinalIgnoreCase),
            distances.TryGetValue(front.System, out var distance) ? distance : null)),
    ];

    /// <summary>The chronicle newest first. An entry ended at the last tick it spanned; it has no end time when it spanned none.</summary>
    public static IReadOnlyList<ChronicleRow> ChronicleRows(Campaign campaign) =>
    [
        .. campaign.Chronicle.Reverse().Select(entry => new ChronicleRow(entry, entry.Ticks.Count == 0 ? null : entry.Ticks.Max())),
    ];

    /// <summary>The Fronts columns.</summary>
    public static IReadOnlyList<SortColumn<FrontRow>> FrontHeads() =>
    [
        SortColumn<FrontRow>.Figure("Rank", row => (int?)row.Rank, SortDirection.Ascending),
        SortColumn<FrontRow>.Text("Kind", row => CampaignFronts.Describe(row.Front.Kind)),
        SortColumn<FrontRow>.Text("System", row => row.Front.System),
        SortColumn<FrontRow>.Figure("Distance", row => row.Distance, SortDirection.Ascending),
        SortColumn<FrontRow>.Text("Reason", row => row.Front.Reason),
        SortColumn<FrontRow>.Figure("Data age", row => row.Front.Updated, SortDirection.Descending),
        SortColumn<FrontRow>.Plain(string.Empty),
    ];

    public static readonly SortOrder FrontOrder = new(0, SortDirection.Ascending);

    /// <summary>The Chronicle columns.</summary>
    public static IReadOnlyList<SortColumn<ChronicleRow>> ChronicleHeads() =>
    [
        SortColumn<ChronicleRow>.Figure("Ended", row => row.Ended, SortDirection.Descending),
        SortColumn<ChronicleRow>.Text("Kind", row => row.Entry.Kind),
        SortColumn<ChronicleRow>.Text("System", row => row.Entry.System) with { Second = "Opponent" },
        SortColumn<ChronicleRow>.Text("Outcome", row => row.Entry.Outcome),
        SortColumn<ChronicleRow>.Figure("Ticks", row => (int?)row.Entry.Ticks.Count, SortDirection.Descending),
        SortColumn<ChronicleRow>.Figure<int>("Your work", row => row.Entry.Work.Count > 0 ? row.Entry.Work.Count : null, SortDirection.Descending) with { Second = "While it ran" },
    ];

    public static readonly SortOrder ChronicleOrder = new(0, SortDirection.Descending);

    /// <summary>The count under a section's name on the desktop: <c>7 fronts</c>, <c>12 systems</c>, <c>6 factions</c>, <c>4 resolved</c>.</summary>
    public static IReadOnlyList<string> SectionCounts(int fronts, int systems, int work, int resolved) =>
    [
        fronts == 1 ? "1 front" : $"{fronts} fronts",
        systems == 1 ? "1 system" : $"{systems} systems",
        work == 1 ? "1 faction" : $"{work} factions",
        $"{resolved} resolved",
    ];

    /// <summary>Whether the front was read from a galaxy report older than <see cref="FactionRow.OldAfter"/>; the Commander's own readings never are.</summary>
    public static bool IsOld(Front front, DateTimeOffset now) =>
        !front.OwnReading && front.Updated is { } at && now - at > FactionRow.OldAfter;

    /// <summary>The Data age cell: <c>2 hours ago</c>, or <c>you saw it 2 hours ago</c> for the Commander's own reading.</summary>
    public static string AgeText(Front front, DateTimeOffset now, SinceTick sinceTick = SinceTick.NoReading) =>
        front.Updated is not { } at ? Dash
        : front.OwnReading && sinceTick == SinceTick.Unchanged ? FactionModel.UnchangedSinceTick
        : front.OwnReading ? "you saw it " + FactionModel.Ago(now - at)
        : FactionModel.Ago(now - at);

    /// <summary>The days won in a conflict the Commander has seen, <c>2 – 1</c>; null otherwise.</summary>
    public static string? DaysWonText(Front front) =>
        front.Conflict is { Visited: true } conflict ? $"{conflict.OursWon} – {conflict.TheirsWon}" : null;

    /// <summary>The cyan note beside the days won: <c>days won · you saw it 20 minutes ago</c>, or <c>days won · unchanged since the tick</c>.</summary>
    public static string DaysWonNote(Front front, DateTimeOffset now, SinceTick sinceTick = SinceTick.NoReading) =>
        front.Updated is not { } at
            ? "days won"
            : front.OwnReading && sinceTick == SinceTick.Unchanged
                ? "days won · " + FactionModel.UnchangedSinceTick
                : "days won · " + (front.OwnReading ? "you saw it " : string.Empty) + FactionModel.Ago(now - at);

    /// <summary>The count beside the chosen front's heading: <c>3 ticks</c>.</summary>
    public static string TicksText(ChosenFront chosen) => chosen.Ticks.Count == 1 ? "1 tick" : $"{chosen.Ticks.Count} ticks";

    /// <summary>The phrase that takes <paramref name="front"/>, as the campaign tool is told it.</summary>
    public static string TakePhrase(Front front)
    {
        var noun = front.Kind == FrontKind.Conflict && front.Conflict is { } conflict
            ? conflict.Type.ToLowerInvariant()
            : CampaignFronts.Describe(front.Kind).ToLowerInvariant();

        return $"take the {noun} in {front.System}";
    }

    /// <summary>The phrases beside the proposal's tiles; the one that names a front is a phrase the tool takes.</summary>
    public static string ProposalPhrases(Front front) =>
        $"Or say \"take it\", \"next front\", or name one: \"{TakePhrase(front)}\".";

    /// <summary>The note under the Fronts table: how the ranking is made and how a front is taken.</summary>
    public static string FrontsHint(IReadOnlyList<Front> fronts) =>
        "D47 ranks the fronts from the faction report and your own readings after each tick: conflicts first, stale reports last. "
        + "TAKE makes one the chosen front"
        + (fronts.Count > 0 ? $", as \"{TakePhrase(fronts[0])}\" does." : ".");

    /// <summary>The warning after a failed fetch: the fronts shown were computed before it.</summary>
    public static string FailedText(string faction, CampaignFrontsSnapshot snapshot, DateTimeOffset? tick)
    {
        var computed = ComputedText(snapshot, tick);
        var when = computed.StartsWith("After", StringComparison.Ordinal) ? "after" + computed[5..] : "at " + computed;

        return $"The report for {faction} could not be fetched. These fronts were computed {when}. "
            + $"D47 tries again in {(int)CampaignFrontsWatch.RetryAfter.TotalMinutes} minutes.";
    }

    /// <summary>The Commander's work on a resolved front, one line per kind: <c>+12 marks</c>, <c>1,200,000 CR in bounties</c>; <c>none</c> without any.</summary>
    public static IReadOnlyList<string> WorkLines(ChronicleEntry entry) => WorkLines(entry.Work);

    /// <summary>The lines for <paramref name="work"/>, one per kind; <c>none</c> without any.</summary>
    public static IReadOnlyList<string> WorkLines(IReadOnlyList<ChronicleWork> work) =>
        work.Count == 0
            ? ["none"]
            : [.. work.Select(WorkLine)];

    /// <summary>The totals of <paramref name="entries"/> by kind, as a chronicle entry holds them.</summary>
    public static IReadOnlyList<ChronicleWork> WorkOf(IEnumerable<BgsEntry> entries) =>
    [
        .. entries
            .GroupBy(entry => entry.Kind)
            .Select(group => new ChronicleWork(group.Key, group.Sum(entry => entry.Amount)))
            .OrderBy(work => work.Kind),
    ];

    /// <summary>The second line of the Your work head: <c>Since the tick</c>, or <c>Last 24 hours</c> without a tick time.</summary>
    public static string WorkPeriod(bool ticked) => ticked ? "Since the tick" : "Last 24 hours";

    /// <summary>The headset's line above the Systems table, which has no column to name the period in.</summary>
    public static string WorkUnderNames(string faction, DateTimeOffset? tick) =>
        $"Under each system: your work for {faction} " + (tick is { } at ? $"since the {PageKit.Clock(at)} UTC tick." : "in the last 24 hours.");

    private static string WorkLine(ChronicleWork work) => work.Kind switch
    {
        BgsWorkKind.Mission => (work.Amount < 0 ? "−" : "+") + Math.Abs(work.Amount).ToString(CultureInfo.InvariantCulture) + (Math.Abs(work.Amount) == 1 ? " mark" : " marks"),
        BgsWorkKind.Bounty => BgsWorkTable.Credits(work.Amount) + " in bounties",
        BgsWorkKind.CombatBond => BgsWorkTable.Credits(work.Amount) + " in bonds",
        BgsWorkKind.Trade => BgsWorkTable.Credits(work.Amount) + " of trade",
        _ => BgsWorkTable.Credits(work.Amount) + " in data sales",
    };

    /// <summary>The date a chronicle entry ended: <c>2 Nov</c>, or a dash when it spanned no tick.</summary>
    public static string EndedText(DateTimeOffset? ended) =>
        ended is { } at ? at.ToString("d MMM", CultureInfo.InvariantCulture) : Dash;

    /// <summary>The intents in the order the Stepper steps through them.</summary>
    public static readonly IReadOnlyList<SystemIntent> Intents = Enum.GetValues<SystemIntent>();

    /// <summary>The intent as the Stepper writes it.</summary>
    public static string Label(SystemIntent intent) => intent switch
    {
        SystemIntent.Hold => "Hold",
        SystemIntent.Grow => "Grow",
        SystemIntent.ExpandFromHere => "Expand from here",
        SystemIntent.StayUnder75 => "Stay under 75%",
        SystemIntent.TakeControl => "Take control",
        _ => "Leave alone",
    };

    /// <summary>The six labels, with <c>· default</c> after the row's intent when it is the default rather than set.</summary>
    public static IReadOnlyList<string> Labels(SystemIntent intent, bool isDefault) =>
    [
        .. Intents.Select(each => isDefault && each == intent ? $"{Label(each)} · default" : Label(each)),
    ];

    /// <summary><c>6.12 ly</c>, or a dash when unknown.</summary>
    public static string DistanceText(double? lightYears) =>
        lightYears is { } distance ? StarSystemPage.DistanceText(distance) : Dash;

    /// <summary><c>68.0%</c>, or a dash when unknown.</summary>
    public static string InfluenceText(double? influence) =>
        influence is { } fraction ? (fraction * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : Dash;

    /// <summary>The lead in points: <c>+49.2</c>, <c>−4.2</c>, or a dash when there is no rival.</summary>
    public static string LeadText(double? lead)
    {
        if (lead is not { } fraction)
        {
            return Dash;
        }

        var points = Math.Round(fraction * 100, 1, MidpointRounding.AwayFromZero);

        return (points < 0 ? "−" : "+") + Math.Abs(points).ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary><c>12 · 7 controlled</c>.</summary>
    public static string SystemsText(IReadOnlyList<CampaignSystemRow> rows) =>
        $"{rows.Count} · {rows.Count(row => row.Controls)} controlled";

    /// <summary>When the fronts were computed: after the last BGS tick when the report is newer than it, else the time.</summary>
    public static string ComputedText(CampaignFrontsSnapshot? snapshot, DateTimeOffset? tick) =>
        snapshot?.ComputedAt is not { } computed
            ? "Not yet"
            : tick is { } at && snapshot.ReportFetchedAt is { } fetched && fetched >= at
                ? $"After the {PageKit.Clock(at)} UTC tick"
                : $"{PageKit.Clock(computed)} UTC";

    /// <summary>The Systems columns; <paramref name="workOn"/> counts the Commander's kinds of work in a system.</summary>
    public static IReadOnlyList<SortColumn<CampaignSystemRow>> Heads(bool headset, bool ticked = true, Func<string, int>? workOn = null) =>
    [
        SortColumn<CampaignSystemRow>.Text("System", row => row.System) with { Second = headset ? null : "Controlled or not" },
        SortColumn<CampaignSystemRow>.Figure("Distance", row => row.Distance, SortDirection.Ascending),
        SortColumn<CampaignSystemRow>.Figure(headset ? "Infl." : "Influence", row => row.Influence, SortDirection.Descending),
        SortColumn<CampaignSystemRow>.Figure("Lead", row => row.Lead, SortDirection.Descending) with { Second = headset ? null : "Points" },
        .. headset
            ? Array.Empty<SortColumn<CampaignSystemRow>>()
            : [SortColumn<CampaignSystemRow>.Figure<int>("Your work", row => workOn?.Invoke(row.System) is > 0 and var kinds ? kinds : null, SortDirection.Descending) with { Second = WorkPeriod(ticked) }],
        SortColumn<CampaignSystemRow>.Text("State", row => row.State),
        SortColumn<CampaignSystemRow>.Text("Intent", row => Label(row.Intent)),
    ];

    /// <summary>The order the rows come in, drawn as Influence ▼.</summary>
    public static readonly SortOrder DefaultOrder = new(2, SortDirection.Descending);

    public const string HubLabel = "HUB OF THE FIRST TEN";

    public const string HubMark = "HUB";

    public const string FirstTenRule = "▲ FIRST TEN";

    /// <summary>The Systems section's hub block: the hub's system, or null for a dash, and the line beside it.</summary>
    public sealed record HubReading(string? System, string Line);

    private static readonly string[] NumberWords = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>
    /// The hub of the first <see cref="CampaignHub.Span"/> of <paramref name="sorted"/>, which is the table in its sort and search,
    /// with the line under the HUB OF THE FIRST TEN label. <paramref name="sortLabel"/> is the sorted column's desktop heading.
    /// </summary>
    public static HubReading Hub(IReadOnlyList<CampaignSystemRow> sorted, string sortLabel, SortDirection direction, bool headset)
    {
        ArgumentNullException.ThrowIfNull(sorted);

        var ten = sorted.Take(CampaignHub.Span).ToList();
        var index = CampaignHub.IndexOf([.. ten.Select(row => row.Position)]);
        var rows = ten.Count is >= CampaignHub.Span or 0 ? "first ten" : ten.Count.ToString(CultureInfo.InvariantCulture);
        var without = ten.Where(row => row.Position is null).Select(row => row.System).ToList();

        if (index is not { } hub)
        {
            return new HubReading(null, $"Fewer than two of the first ten rows below have a position.");
        }

        var others = NumberWords[ten.Count - without.Count - 1];
        var by = $"by {sortLabel} {(direction == SortDirection.Ascending ? "▲" : "▼")}";
        var line = headset
            ? $"of the {rows} rows, {by}"
            : $"The system nearest on average to the other {others} of the {rows} rows below, {by}.";

        if (without.Count > 0)
        {
            line += headset ? "." : string.Empty;
            line += without.Count == 1
                ? $" {without[0]} has no position, so it is left out."
                : $" {string.Join(", ", without.Take(without.Count - 1))} and {without[^1]} have no position, so they are left out.";
        }

        return new HubReading(ten[hub].System, line);
    }

    /// <summary>The rows whose system name contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<CampaignSystemRow> Matching(IReadOnlyList<CampaignSystemRow> rows, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? rows
            : [.. rows.Where(row => row.System.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The squadron's faction first, then the campaign faction when it is another, then every faction in the
    /// Commander's journal readings, most systems first, each with where it was seen.
    /// </summary>
    public static IReadOnlyList<CampaignFactionChoice> FactionChoices(string? squadron, string? current, SystemStandings standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var seen = standings.Systems
            .SelectMany(system => (standings.Latest(system)?.Factions ?? []).Select(faction => (faction.Name, System: system)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.System).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var choices = new List<CampaignFactionChoice>();

        void Add(string name, string detail)
        {
            if (!choices.Any(choice => string.Equals(choice.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new CampaignFactionChoice(name, detail));
            }
        }

        if (squadron is { Length: > 0 })
        {
            Add(squadron, "Your squadron's faction");
        }

        if (current is { Length: > 0 })
        {
            Add(current, seen.TryGetValue(current, out var at) ? $"The BGS faction · {SeenIn(at)}" : "The BGS faction");
        }

        foreach (var (name, systems) in seen.OrderByDescending(pair => pair.Value.Count).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Add(name, SeenIn(systems));
        }

        return choices;
    }

    private static string SeenIn(IReadOnlyList<string> systems) =>
        systems.Count <= 3
            ? $"Seen in {string.Join(", ", systems)}"
            : $"Seen in {string.Join(", ", systems.Take(3))} and {systems.Count - 3} more";
}
