using System.Diagnostics;
using System.Text.Json;
using D47.Core.Goals;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>tools/seal-stories.py</c> refuses a card's <c>goalArcs</c> without <c>goals</c>, empty, with a repeated key or with a
/// key that is not a Goals arc, as <c>StoryCatalog.Faults</c> does, and knows the same arcs as <c>GoalCatalogue</c> (#1747).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSealCheckRefusesBadGoalArcsTests
{
    private const string Check = """
        import importlib.util, json, sys
        spec = importlib.util.spec_from_file_location("seal", sys.argv[1])
        seal = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(seal)
        card = json.loads(sys.argv[2])
        print(json.dumps(list(seal.GOAL_ARCS) if card is None else seal.goal_arcs_fault(card)))
        """;

    private static IReadOnlyList<string> Run(string card)
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
        start.ArgumentList.Add(card);

        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEnd();
        var error = python.StandardError.ReadToEnd();
        python.WaitForExit();

        Assert.True(python.ExitCode == 0, error);
        return JsonSerializer.Deserialize<List<string>>(output)!;
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void TheCheckKnowsEveryGoalsArc() =>
        Assert.Equal(GoalCatalogue.Every.Select(arc => arc.Key), Run("null"));

    [Theory]
    [InlineData("""{"goals": true}""")]
    [InlineData("""{"goals": true, "goalArcs": ["rank.combat", "rank.trade", "rank.explore", "rank.soldier", "rank.exobiologist"]}""")]
    public void AGoodCardHasNoFault(string card) =>
        Assert.Empty(Run(card));

    [Theory]
    [InlineData("""{"goalArcs": ["rank.combat"]}""", "the card's goalArcs is set on a card that is not a Goals story")]
    [InlineData("""{"goals": true, "goalArcs": []}""", "the card's goalArcs is empty")]
    [InlineData("""{"goals": true, "goalArcs": ["rank.combat", "Rank.Combat"]}""", "the card's goalArcs names rank.combat more than once")]
    [InlineData("""{"goals": true, "goalArcs": ["rank.cqc"]}""", "the card's goalArcs names rank.cqc, which is not a Goals arc")]
    [InlineData("""{"goals": true, "goalArcs": "rank.combat"}""", "the card's goalArcs is 'rank.combat', not a list of arc keys")]
    public void ABadListIsRefused(string card, string fault) =>
        Assert.Contains(fault, Run(card));
}
