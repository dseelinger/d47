using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Gate")]
public class TheReleaseIsBuiltFromAFixedRootTests
{
    static readonly Regex DotnetBuildTestOrPublish = new(@"\bdotnet\s+(build|test|publish)\b");

    [Fact]
    public void EveryStepThatBuildsTestsOrPublishesRunsInTheFixedRoot()
    {
        var steps = Steps();
        var building = steps.Where(s => DotnetBuildTestOrPublish.IsMatch(s.Text)).ToList();

        Assert.NotEmpty(building);
        foreach (var step in building)
        {
            Assert.True(
                Regex.IsMatch(step.Text, @"^\s+working-directory:\s*B:\\\s*$", RegexOptions.Multiline),
                $"release.yml step '{step.Name}' runs dotnet but not in B:\\");
            Assert.True(
                step.Text.Contains(@"Test-Path 'B:\d47.slnx'", StringComparison.Ordinal),
                $"release.yml step '{step.Name}' does not check for B:\\d47.slnx first");
        }
    }

    [Fact]
    public void TheRootIsMappedBeforeTheFirstStepThatBuilds()
    {
        var steps = Steps();
        var map = steps.FindIndex(s => s.Text.Contains("subst B: $env:GITHUB_WORKSPACE", StringComparison.Ordinal));
        var firstBuild = steps.FindIndex(s => DotnetBuildTestOrPublish.IsMatch(s.Text));

        Assert.True(map >= 0, "release.yml never maps B: to the workspace");
        Assert.True(map < firstBuild, "release.yml maps B: after the first dotnet step");
        Assert.Contains("$LASTEXITCODE -ne 0", steps[map].Text);
    }

    static List<(string Name, string Text)> Steps()
    {
        var workflow = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), ".github", "workflows", "release.yml"));
        var release = workflow.Split("\n  docs:")[0];
        return Regex.Split(release, @"(?m)^      - (?=name:|uses:)")
            .Skip(1)
            .Select(chunk => (Regex.Match(chunk, @"name:\s*(.+)").Groups[1].Value.Trim(), chunk))
            .ToList();
    }
}
