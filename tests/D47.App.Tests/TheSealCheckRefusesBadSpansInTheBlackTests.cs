using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>tools/seal-stories.py</c> refuses a span in the black that names a beat off the length's sheet, ends at or before it
/// starts, or overlaps another, as <c>StoryCatalog.Faults</c> does (#1737).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSealCheckRefusesBadSpansInTheBlackTests
{
    private const string Check = """
        import importlib.util, json, sys
        spec = importlib.util.spec_from_file_location("seal", sys.argv[1])
        seal = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(seal)
        black = json.loads(sys.argv[2])
        print(json.dumps(seal.black_fault({"black": black}, seal.LENGTHS["1-year"][2], "1-year")))
        """;

    private static IReadOnlyList<string> Faults(string black)
    {
        var start = new ProcessStartInfo("python")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Check);
        start.ArgumentList.Add(Path.Combine(AppSource.RepositoryRoot(), "tools", "seal-stories.py"));
        start.ArgumentList.Add(black);

        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEnd();
        var error = python.StandardError.ReadToEnd();
        python.WaitForExit();

        Assert.True(python.ExitCode == 0, error);
        return JsonSerializer.Deserialize<List<string>>(output)!;
    }

    [Theory]
    [InlineData("""[{"from": null, "to": null}]""")]
    [InlineData("""[{"from": "catalyst", "to": "midpoint"}, {"from": "midpoint", "to": null}]""")]
    [InlineData("[]")]
    public void SpansOnTheSheetInOrderHaveNoFault(string black) =>
        Assert.Empty(Faults(black));

    [Theory]
    [InlineData("""[{"from": "climax", "to": "midpoint"}]""", "black span 1 has from 'climax', not a beat of a 1-year story")]
    [InlineData("""[{"from": "midpoint", "to": "catalyst"}]""", "black span 1 ends at or before the beat it starts at")]
    [InlineData("""[{"from": "midpoint", "to": "midpoint"}]""", "black span 1 ends at or before the beat it starts at")]
    [InlineData("""[{"from": "catalyst", "to": "midpoint"}, {"from": "bStory", "to": "allIsLost"}]""", "black span 2 overlaps black span 1")]
    [InlineData("""[{"from": null, "to": null}, {"from": "midpoint", "to": "allIsLost"}]""", "black span 2 overlaps black span 1")]
    public void ABadSpanIsRefused(string black, string fault) =>
        Assert.Contains(fault, Faults(black));
}
