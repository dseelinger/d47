using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Campaigns;

/// <summary>Hand-built faction reports and journal readings for the campaign tests.</summary>
internal static class CampaignReports
{
    public const string Us = "Kestrel Reach Compact";

    public static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public static FactionSystem Place(
        string name,
        double influence,
        bool controls = true,
        IReadOnlyList<string>? active = null,
        IReadOnlyList<string>? pending = null,
        IReadOnlyList<FactionNeighbour>? others = null,
        DateTimeOffset? reportedAt = null,
        long population = 1_000_000) => new()
    {
        Name = name,
        Influence = influence,
        Controls = controls,
        ActiveStates = active ?? [],
        PendingStates = pending ?? [],
        Others = others ?? [new FactionNeighbour("Lave Purple Gang", 0.10)],
        ReportedAt = reportedAt ?? Now.AddDays(-1),
        Population = population,
    };

    public static FactionReport Report(params FactionSystem[] systems) => new(Us, "Cooperative", "Independent", systems, []);

    /// <summary>A journal FSDJump into <paramref name="system"/> at <paramref name="at"/>, with factions and conflicts as raw JSON.</summary>
    public static JournalEvent Jump(string system, DateTimeOffset at, string factions, string conflicts = "")
    {
        var stamp = at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var line = $$"""{ "timestamp":"{{stamp}}", "event":"FSDJump", "StarSystem":"{{system}}", "SystemAddress":1, "StarPos":[0,0,0], "Factions":[{{factions}}], "Conflicts":[{{conflicts}}] }""";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    /// <summary>One faction entry for <see cref="Jump"/>.</summary>
    public static string Faction(string name, double influence, string active = "", string pending = "") =>
        $$"""{ "Name":"{{name}}", "Influence":{{influence.ToString(CultureInfo.InvariantCulture)}}, "ActiveStates":[{{States(active)}}], "PendingStates":[{{States(pending)}}] }""";

    private static string States(string states) =>
        string.Join(",", states.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(state => $$"""{ "State":"{{state}}" }"""));
}
