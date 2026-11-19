using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <see cref="CodexKinds.OfScan(JournalEvent, CodexTable)"/> gives each scan in <c>tests/fixtures/codex/journal-kinds.tsv</c>
/// the key the fixture records, and a key that fixture shares with <c>kinds.tsv</c> is the one
/// <see cref="CodexKinds.OfSpansh(BodyProfile, CodexTable)"/> gives the Spansh body.
/// </summary>
[Trait("Category", "Integration")]
public class AScanHasTheKindTheFixtureGivesTests
{
    private static string? Cell(string cell) => cell.Length == 0 ? null : cell;

    private static List<string[]> Rows(string file) =>
        [.. File.ReadAllLines(Path.Combine(RepositoryFolder.Root(), "tests", "fixtures", "codex", file))
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split('\t'))];

    private static CodexTable UsualBand(string subType, string band, string key) =>
        CodexTable.Parse(band.Length == 0 ? string.Empty : KindRows.Row(key[..key.LastIndexOf('.')], subType, "Stars", subType: subType, usualBand: band));

    private static JournalEvent Scan(string[] cells)
    {
        var fields = new Dictionary<string, object>
        {
            ["timestamp"] = "2026-11-19T10:00:00Z",
            ["event"] = "Scan",
            ["ScanType"] = "Detailed",
            ["BodyName"] = cells[0],
        };
        string[] names = ["StarType", "Luminosity", "PlanetClass", "TerraformState", "Atmosphere", "AtmosphereType"];

        for (var i = 0; i < names.Length; i++)
        {
            if (cells[i + 1].Length > 0)
            {
                fields[names[i]] = cells[i + 1] == "\"\"" ? string.Empty : cells[i + 1];
            }
        }

        var rings = cells[7].Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (rings.Length > 0)
        {
            fields["Rings"] = rings.Select((ring, i) => new Dictionary<string, string> { ["Name"] = $"{cells[0]} {(char)('A' + i)} Ring", ["RingClass"] = ring }).ToList();
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(fields), NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static string? OfSpansh(string[] cells)
    {
        var body = new BodyProfile
        {
            BodyId = 1,
            Name = "Fixture A 1",
            Type = cells[0],
            SubType = Cell(cells[1]),
            Luminosity = Cell(cells[2]),
            TerraformingState = Cell(cells[3]),
            Atmosphere = Cell(cells[4]),
            Rings = [.. cells[5].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select((type, i) => new RingProfile($"Fixture A 1 {(char)('A' + i)} Ring", type, new Dictionary<string, int>()))],
        };

        return CodexKinds.OfSpansh(body, UsualBand(cells[1], cells[6], cells[7]));
    }

    private static string? OfScan(string[] cells) =>
        CodexKinds.OfScan(Scan(cells), cells[8].Length == 0 ? CodexTable.Parse(string.Empty) : UsualBand(CodexKinds.StarTypes[cells[1]], cells[8], cells[9]));

    [Fact]
    public void EveryFixtureScanHasTheKeyItRecords()
    {
        var rows = Rows("journal-kinds.tsv");
        var wrong = rows
            .Where(cells => OfScan(cells) != Cell(cells[9]))
            .Select(cells => $"{string.Join('|', cells)} -> {OfScan(cells) ?? "none"}")
            .ToList();

        Assert.NotEmpty(rows);
        Assert.Empty(wrong);
    }

    [Fact]
    public void AKeyBothFixturesHoldComesFromTheScanAndTheSpanshBodyAlike()
    {
        var spansh = Rows("kinds.tsv").GroupBy(cells => cells[7]).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var shared = Rows("journal-kinds.tsv").Where(cells => cells[9].Length > 0 && spansh.ContainsKey(cells[9])).ToList();
        var wrong = shared
            .SelectMany(scan => spansh[scan[9]].Select(body => (Scan: OfScan(scan), Spansh: OfSpansh(body), Name: scan[0])))
            .Where(pair => pair.Scan != pair.Spansh)
            .Select(pair => $"{pair.Name}: scan {pair.Scan ?? "none"}, Spansh {pair.Spansh ?? "none"}")
            .ToList();

        Assert.True(shared.Count >= 15, $"The fixtures share only {shared.Count} keys.");
        Assert.Empty(wrong);
    }
}
