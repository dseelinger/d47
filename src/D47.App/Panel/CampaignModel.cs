using System.Globalization;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>One faction the Campaign faction picker offers, with the line under its name.</summary>
public sealed record CampaignFactionChoice(string Name, string Detail);

/// <summary>The words, figures and columns of Commander › Campaign.</summary>
public static class CampaignModel
{
    public const string Dash = "—";

    public const string NoFactionText = "No campaign faction yet. Choose one, or say 'our campaign faction is' and its name.";

    public const string NoReportText = "No faction report yet. D47 fetches it after the next tick.";

    public const string NoSystemText = "No system of the campaign faction has that name.";

    public const string Legend =
        "Intents with a bar like this one are protected. You set them here or by a phrase such as \"hold\" or "
        + "\"grow\" and the system's name; D47's model cannot change them.";

    public const string Hint =
        "The six intents in order: Hold, Grow, Expand from here, Stay under 75%, Take control, Leave alone. A system "
        + "you have not set follows the default in grey: Hold where the faction controls it, Leave alone where it does not.";

    public const string PickerHelp =
        "The campaign starts with your squadron's faction. Choose any faction D47 has seen in your journal, or type a "
        + "name; its report comes from the same source as Search › Faction.";

    /// <summary>The sections under the frame, in order.</summary>
    public static readonly IReadOnlyList<string> Sections = ["FRONTS", "SYSTEMS", "CHRONICLE"];

    public const int FrontsSection = 0;
    public const int SystemsSection = 1;
    public const int ChronicleSection = 2;

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

    /// <summary>One front with its place in the ranking.</summary>
    public sealed record FrontRow(int Rank, Front Front, bool Chosen);

    /// <summary>One resolved front as the Chronicle section draws it.</summary>
    public sealed record ChronicleRow(ChronicleEntry Entry, DateTimeOffset? Ended);

    /// <summary>The fronts in rank order; the one being worked is marked chosen.</summary>
    public static IReadOnlyList<FrontRow> FrontRows(IReadOnlyList<Front> fronts, ChosenFront? chosen) =>
    [
        .. fronts.Select((front, index) => new FrontRow(
            index + 1,
            front,
            chosen is not null && chosen.Kind == front.Kind && string.Equals(chosen.System, front.System, StringComparison.OrdinalIgnoreCase))),
    ];

    /// <summary>The chronicle newest first. An entry ended at the last tick it spanned; it has no end time when it spanned none.</summary>
    public static IReadOnlyList<ChronicleRow> ChronicleRows(Campaign campaign) =>
    [
        .. campaign.Chronicle.Reverse().Select(entry => new ChronicleRow(entry, entry.Ticks.Count == 0 ? null : entry.Ticks.Max())),
    ];

    /// <summary>The Fronts columns. Rank, Kind, System and Data age sort; Reason does not.</summary>
    public static IReadOnlyList<SortColumn<FrontRow>> FrontHeads() =>
    [
        SortColumn<FrontRow>.Figure("Rank", row => (int?)row.Rank, SortDirection.Ascending),
        SortColumn<FrontRow>.Text("Kind", row => CampaignFronts.Describe(row.Front.Kind)),
        SortColumn<FrontRow>.Text("System", row => row.Front.System),
        SortColumn<FrontRow>.Plain("Reason"),
        SortColumn<FrontRow>.Figure("Data age", row => row.Front.Updated, SortDirection.Descending),
        SortColumn<FrontRow>.Plain(string.Empty),
    ];

    public static readonly SortOrder FrontOrder = new(0, SortDirection.Ascending);

    /// <summary>The Chronicle columns. Ended, Kind, System and Ticks sort; Outcome and Your work do not.</summary>
    public static IReadOnlyList<SortColumn<ChronicleRow>> ChronicleHeads() =>
    [
        SortColumn<ChronicleRow>.Figure("Ended", row => row.Ended, SortDirection.Descending),
        SortColumn<ChronicleRow>.Text("Kind", row => row.Entry.Kind),
        SortColumn<ChronicleRow>.Text("System", row => row.Entry.System) with { Second = "Opponent" },
        SortColumn<ChronicleRow>.Plain("Outcome"),
        SortColumn<ChronicleRow>.Figure("Ticks", row => (int?)row.Entry.Ticks.Count, SortDirection.Descending),
        SortColumn<ChronicleRow>.Plain("Your work") with { Second = "While it ran" },
    ];

    public static readonly SortOrder ChronicleOrder = new(0, SortDirection.Descending);

    /// <summary>The count under a section's name on the desktop: <c>7 fronts</c>, <c>12 systems</c>, <c>4 resolved</c>.</summary>
    public static IReadOnlyList<string> SectionCounts(int fronts, int systems, int resolved) =>
    [
        fronts == 1 ? "1 front" : $"{fronts} fronts",
        systems == 1 ? "1 system" : $"{systems} systems",
        $"{resolved} resolved",
    ];

    /// <summary>Whether the front was read from a galaxy report older than <see cref="FactionRow.OldAfter"/>; the Commander's own readings never are.</summary>
    public static bool IsOld(Front front, DateTimeOffset now) =>
        !front.OwnReading && front.Updated is { } at && now - at > FactionRow.OldAfter;

    /// <summary>The Data age cell: <c>2 hours ago</c>, or <c>you saw it 2 hours ago</c> for the Commander's own reading.</summary>
    public static string AgeText(Front front, DateTimeOffset now) =>
        front.Updated is not { } at ? Dash : front.OwnReading ? "you saw it " + FactionModel.Ago(now - at) : FactionModel.Ago(now - at);

    /// <summary>The days won in a conflict the Commander has seen, <c>2 – 1</c>; null otherwise.</summary>
    public static string? DaysWonText(Front front) =>
        front.Conflict is { Visited: true } conflict ? $"{conflict.OursWon} – {conflict.TheirsWon}" : null;

    /// <summary>The cyan note beside the days won: <c>days won · you saw it 20 minutes ago</c>.</summary>
    public static string DaysWonNote(Front front, DateTimeOffset now) =>
        front.Updated is not { } at
            ? "days won"
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
    public static IReadOnlyList<string> WorkLines(ChronicleEntry entry) =>
        entry.Work.Count == 0
            ? ["none"]
            : [.. entry.Work.Select(WorkLine)];

    private static string WorkLine(ChronicleWork work) => work.Kind switch
    {
        BgsWorkKind.Mission => (work.Amount < 0 ? "−" : "+") + Math.Abs(work.Amount).ToString(CultureInfo.InvariantCulture) + (Math.Abs(work.Amount) == 1 ? " mark" : " marks"),
        BgsWorkKind.Bounty => BgsWorkPage.Credits(work.Amount) + " in bounties",
        BgsWorkKind.CombatBond => BgsWorkPage.Credits(work.Amount) + " in bonds",
        BgsWorkKind.Trade => BgsWorkPage.Credits(work.Amount) + " of trade",
        _ => BgsWorkPage.Credits(work.Amount) + " in data sales",
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

    /// <summary>The Systems columns. System, Influence and Lead sort; State and Intent do not.</summary>
    public static IReadOnlyList<SortColumn<CampaignSystemRow>> Heads(bool headset) =>
    [
        SortColumn<CampaignSystemRow>.Text("System", row => row.System) with { Second = headset ? null : "Controlled or not" },
        SortColumn<CampaignSystemRow>.Figure(headset ? "Infl." : "Influence", row => row.Influence, SortDirection.Descending),
        SortColumn<CampaignSystemRow>.Figure("Lead", row => row.Lead, SortDirection.Descending) with { Second = headset ? null : "Points" },
        SortColumn<CampaignSystemRow>.Plain("State"),
        SortColumn<CampaignSystemRow>.Plain("Intent"),
    ];

    /// <summary>The order the rows come in, drawn as Influence ▼.</summary>
    public static readonly SortOrder DefaultOrder = new(1, SortDirection.Descending);

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
            Add(current, seen.TryGetValue(current, out var at) ? $"The campaign faction · {SeenIn(at)}" : "The campaign faction");
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
