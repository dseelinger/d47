using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

[Trait("Category", "Integration")]
public class TheSystemNameTableIsWhatThePatternsMissTests
{

    private static string[] Lines() =>
        File.ReadAllLines(Path.Combine(RepositoryFolder.Root(), "src", "D47.Core", "Knowledge", "SystemNames.tsv"));

    [Trait("Category", "Integration")]
    [Fact]
    public void EveryRowIsANameOnceInOrdinalOrder()
    {
        var rows = Lines().Where(line => !line.StartsWith('#')).ToArray();

        Assert.True(rows.Length > 1000, $"{rows.Length} rows");
        Assert.DoesNotContain(rows, row => string.IsNullOrWhiteSpace(row) || row.Contains('\t'));
        Assert.Equal(rows.Length, rows.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(rows.Order(StringComparer.Ordinal), rows);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheHeaderNamesTheScriptAndTheSource()
    {
        var header = string.Join('\n', Lines().TakeWhile(line => line.StartsWith('#')));

        Assert.Contains("tools/gen-system-names.py", header, StringComparison.Ordinal);
        Assert.Contains("https://www.edsm.net/dump/systemsPopulated.json.gz", header, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheEmbeddedTableIsTheFileOnDisk()
    {
        var rows = Lines().Count(line => line.Length > 0 && !line.StartsWith('#'));

        Assert.Equal(rows, SystemNameTable.Names.Count);
        Assert.True(SystemNameTable.LongestWordCount >= 2);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void NoticeNamesTheTableAndItsGenerator()
    {
        var notice = File.ReadAllText(Path.Combine(RepositoryFolder.Root(), "NOTICE"));

        Assert.Contains("SystemNames.tsv", notice, StringComparison.Ordinal);
        Assert.Contains("tools/gen-system-names.py", notice, StringComparison.Ordinal);
    }
}
