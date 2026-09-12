using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheSystemNameTableIsWhatThePatternsMissTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("d47.slnx not found above the test output.");
    }

    private static string[] Lines() =>
        File.ReadAllLines(Path.Combine(Root(), "src", "D47.Core", "Knowledge", "SystemNames.tsv"));

    [Fact]
    public void EveryRowIsANameOnceInOrdinalOrder()
    {
        var rows = Lines().Where(line => !line.StartsWith('#')).ToArray();

        Assert.True(rows.Length > 1000, $"{rows.Length} rows");
        Assert.DoesNotContain(rows, row => string.IsNullOrWhiteSpace(row) || row.Contains('\t'));
        Assert.Equal(rows.Length, rows.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(rows.Order(StringComparer.Ordinal), rows);
    }

    [Fact]
    public void NoRowIsSomethingAPatternAlreadyFinds()
    {
        var caught = Lines()
            .Where(line => !line.StartsWith('#'))
            .Where(row => SystemNameFinder.IsProcedural(row) || SystemNameFinder.IsCatalogue(row))
            .ToArray();

        Assert.Empty(caught);
    }

    [Fact]
    public void TheHeaderNamesTheScriptAndTheSource()
    {
        var header = string.Join('\n', Lines().TakeWhile(line => line.StartsWith('#')));

        Assert.Contains("tools/gen-system-names.py", header, StringComparison.Ordinal);
        Assert.Contains("https://www.edsm.net/dump/systemsPopulated.json.gz", header, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEmbeddedTableIsTheFileOnDisk()
    {
        var rows = Lines().Count(line => line.Length > 0 && !line.StartsWith('#'));

        Assert.Equal(rows, SystemNameTable.Names.Count);
        Assert.True(SystemNameTable.LongestWordCount >= 2);
    }

    [Fact]
    public void NoticeNamesTheTableAndItsGenerator()
    {
        var notice = File.ReadAllText(Path.Combine(Root(), "NOTICE"));

        Assert.Contains("SystemNames.tsv", notice, StringComparison.Ordinal);
        Assert.Contains("tools/gen-system-names.py", notice, StringComparison.Ordinal);
    }
}
