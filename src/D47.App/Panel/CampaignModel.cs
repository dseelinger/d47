using System.Globalization;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Journal;

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
