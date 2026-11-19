using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <see cref="CodexKinds.OfSpansh(BodyProfile, CodexTable)"/> gives each body in <c>tests/fixtures/codex/kinds.tsv</c> the
/// key the fixture records. <c>tools/gen-codex-bodies.py</c> checks its classifier against the same file.
/// </summary>
[Trait("Category", "Integration")]
public class ASpanshBodyHasTheKindTheFixtureGivesTests
{
    private static string? Cell(string cell) => cell.Length == 0 ? null : cell;

    [Fact]
    public void EveryFixtureBodyHasTheKeyItRecords()
    {
        var path = Path.Combine(RepositoryFolder.Root(), "tests", "fixtures", "codex", "kinds.tsv");
        var rows = File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#').Select(line => line.Split('\t')).ToList();
        var wrong = new List<string>();

        foreach (var cells in rows)
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
            var table = CodexTable.Parse(cells[6].Length == 0
                ? string.Empty
                : KindRows.Row(cells[7][..cells[7].LastIndexOf('.')], cells[1], "Stars", subType: cells[1], usualBand: cells[6]));
            var found = CodexKinds.OfSpansh(body, table);

            if (found != cells[7])
            {
                wrong.Add($"{string.Join('|', cells)} -> {found ?? "none"}");
            }
        }

        Assert.NotEmpty(rows);
        Assert.Empty(wrong);
    }
}
