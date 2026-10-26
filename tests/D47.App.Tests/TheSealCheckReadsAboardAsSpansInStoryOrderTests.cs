using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>tools/seal-stories.py</c> takes a cast member's <c>aboard</c> as one span object or a list of two or more in story
/// order, and refuses a list out of order (#1759).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSealCheckReadsAboardAsSpansInStoryOrderTests
{
    private const string Check = """
        import importlib.util, json, sys
        spec = importlib.util.spec_from_file_location("seal", sys.argv[1])
        seal = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(seal)
        aboard = json.loads(sys.argv[2])
        print(json.dumps(seal.aboard_fault("cast 1 (rider)", {"aboard": aboard}, seal.LENGTHS["1-year"][2], "1-year")))
        """;

    private static IReadOnlyList<string> Faults(string aboard)
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
        start.ArgumentList.Add(aboard);

        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEnd();
        var error = python.StandardError.ReadToEnd();
        python.WaitForExit();

        Assert.True(python.ExitCode == 0, error);
        return JsonSerializer.Deserialize<List<string>>(output)!;
    }

    [Theory]
    [InlineData("""{"boards": "midpoint", "leaves": "allIsLost"}""")]
    [InlineData("""[{"boards": null, "leaves": "catalyst"}, {"boards": "midpoint", "leaves": null}]""")]
    [InlineData("""[{"boards": "setUp", "leaves": "catalyst"}, {"boards": "midpoint", "leaves": "allIsLost"}, {"boards": "finale", "leaves": null}]""")]
    [InlineData("""{"boards": "midpoint", "leaves": "allIsLost", "place": "carrier"}""")]
    [InlineData("""[{"boards": null, "leaves": "catalyst", "place": "ship"}, {"boards": "midpoint", "leaves": null}]""")]
    public void AnObjectOrAListInStoryOrderHasNoFault(string aboard) =>
        Assert.Empty(Faults(aboard));

    [Theory]
    [InlineData("""[{"boards": "midpoint", "leaves": null}]""", "has an aboard list of one span, which is written as an object")]
    [InlineData("[]", "has an empty aboard list")]
    [InlineData("""[{"boards": "setUp", "leaves": "catalyst"}, {"boards": null, "leaves": null}]""", "span 2 boards at null; only the first span boards at the start")]
    [InlineData("""[{"boards": "setUp", "leaves": null}, {"boards": "midpoint", "leaves": null}]""", "span 1 leaves at null; only the last span leaves at the end")]
    [InlineData("""[{"boards": "setUp", "leaves": "midpoint"}, {"boards": "midpoint", "leaves": null}]""", "span 2 boards at or before the beat the span before it leaves at")]
    [InlineData("""[{"boards": "setUp", "leaves": "midpoint"}, {"boards": "catalyst", "leaves": null}]""", "span 2 boards at or before the beat the span before it leaves at")]
    [InlineData("""[{"boards": "setUp", "leaves": "catalyst"}, {"boards": "midpoint", "leaves": "climax"}]""", "span 2 has aboard leaves 'climax'")]
    [InlineData("""{"boards": "midpoint", "leaves": null, "place": "station"}""", "has aboard place 'station', not 'ship' or 'carrier'")]
    [InlineData("""[{"boards": null, "leaves": "catalyst", "place": "carrier"}, {"boards": "midpoint", "leaves": null}]""", "span 2 has aboard place 'ship', not the first span's")]
    public void AListOutOfOrderIsRefused(string aboard, string fault) =>
        Assert.Contains(Faults(aboard), found => found.Contains(fault, StringComparison.Ordinal));
}
