using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Gate")]
public class TheReleaseCheckRunsTheWorkflowsBuildTests
{
    [Theory]
    [InlineData("Build")]
    [InlineData("Publish")]
    public void TheToolRunsTheWorkflowsCommandLines(string step)
    {
        var tool = Tool().Replace(" --disable-build-servers", "");
        var commands = WorkflowCommands(step);

        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            Assert.True(
                tool.Contains(command, StringComparison.Ordinal),
                $"tools/verify-release.ps1 does not run release.yml's {step} command: {command}");
        }
    }

    [Fact]
    public void TheToolDisablesBuildServersOnEveryDotnetCall()
    {
        var lines = Tool().Split('\n').Where(l => Regex.IsMatch(l, @"^\s*dotnet\s+(build|publish)\b")).ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Contains("--disable-build-servers", l));
    }

    [Fact]
    public void TheToolUsesNoRootButB()
    {
        var drives = Regex.Matches(Tool(), @"(?<![A-Za-z$])([A-Za-z]):\\")
            .Select(m => m.Groups[1].Value.ToUpperInvariant())
            .Distinct();

        Assert.Equal(["B"], drives);
    }

    [Fact]
    public void TheReleaseScriptComparesOnlyAfterTheWatcherReportsSuccess()
    {
        var script = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "tools", "release.ps1"));
        var watch = script.IndexOf("watch-release.ps1", StringComparison.Ordinal);
        var failExit = script.IndexOf("if ($watchCode -ne 0) { exit $watchCode }", StringComparison.Ordinal);
        var verify = script.IndexOf("verify-release.ps1", StringComparison.Ordinal);

        Assert.True(watch >= 0, "release.ps1 never runs the watcher");
        Assert.True(failExit > watch, "release.ps1 does not exit with the watcher's code on a failed run");
        Assert.True(verify > failExit, "release.ps1 compares before it has returned on a failed run");
    }

    static string Tool() =>
        File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "tools", "verify-release.ps1"));

    // The `dotnet` lines of one step's run block, with the version input read as the tool's $Version.
    static List<string> WorkflowCommands(string step)
    {
        var workflow = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), ".github", "workflows", "release.yml"));
        var release = workflow.Split("\n  docs:")[0];
        var chunk = Regex.Split(release, @"(?m)^      - (?=name:|uses:)")
            .Skip(1)
            .First(c => Regex.IsMatch(c, @"^name:\s*" + step + @"\s*$", RegexOptions.Multiline));
        return chunk.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("dotnet ", StringComparison.Ordinal))
            .Select(l => l.Replace("${{ inputs.version }}", "$Version"))
            .ToList();
    }
}
