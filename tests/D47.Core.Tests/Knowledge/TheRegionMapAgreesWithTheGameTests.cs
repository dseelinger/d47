using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The region map places each position in <c>tests/fixtures/codex/region-positions.tsv</c> in the region the game
/// wrote in a CodexEntry there, or in the region the fifth column records where the map is known to differ.
/// <c>tools/gen-codex-bodies.py</c> checks its port of the map against the same file.
/// </summary>
[Trait("Category", "Integration")]
public class TheRegionMapAgreesWithTheGameTests
{
    [Fact]
    public void EveryFixturePositionIsInTheRegionItRecords()
    {
        var path = Path.Combine(RepositoryFolder.Root(), "tests", "fixtures", "codex", "region-positions.tsv");
        var rows = File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#').ToList();
        var wrong = new List<string>();

        foreach (var row in rows)
        {
            var cells = row.Split('\t');
            var position = new StarPosition(
                double.Parse(cells[0], CultureInfo.InvariantCulture),
                double.Parse(cells[1], CultureInfo.InvariantCulture),
                double.Parse(cells[2], CultureInfo.InvariantCulture));
            var expected = int.Parse(cells.Length > 4 ? cells[4] : cells[3], CultureInfo.InvariantCulture);
            var found = GalacticRegions.Find(position) is { } name ? GalacticRegions.IdOf(name) : null;

            if (found != expected)
            {
                wrong.Add($"{row} -> {found?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
            }
        }

        Assert.NotEmpty(rows);
        Assert.Empty(wrong);
    }
}
