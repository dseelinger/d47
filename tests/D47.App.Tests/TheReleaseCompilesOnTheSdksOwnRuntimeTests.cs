using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The runner and tools/verify-release.ps1 compile from tools/install-dotnet.ps1's folder, so both run the compiler on the same runtime.</summary>
[Trait("Category", "Gate")]
public class TheReleaseCompilesOnTheSdksOwnRuntimeTests
{
    static readonly Regex DotnetBuildTestOrPublish = new(@"\bdotnet\s+(build|test|publish)\b");

    [Fact]
    public void TheWorkflowInstallsTheSdkAloneBeforeItsFirstBuild()
    {
        var release = Workflow().Split("\n  docs:")[0];
        var install = release.IndexOf("./tools/install-dotnet.ps1", StringComparison.Ordinal);
        var firstBuild = DotnetBuildTestOrPublish.Match(release).Index;

        Assert.DoesNotContain("actions/setup-dotnet", release, StringComparison.Ordinal);
        Assert.True(install >= 0, "release.yml does not install the SDK with tools/install-dotnet.ps1");
        Assert.True(install < firstBuild, "release.yml installs the SDK after its first dotnet step");
        Assert.Contains("$env:GITHUB_PATH -Value $dotnet", release, StringComparison.Ordinal);
        Assert.Contains("\"DOTNET_ROOT=$dotnet\"", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TheToolBuildsWithTheTagsSdkAloneFirstOnThePath()
    {
        var tool = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "tools", "verify-release.ps1"));
        var install = tool.IndexOf("install-dotnet.ps1') -Parent $env:TEMP -GlobalJson (Join-Path $clone 'global.json')", StringComparison.Ordinal);
        var path = tool.IndexOf("$env:PATH = \"$dotnet;$env:PATH\"", StringComparison.Ordinal);
        var build = Regex.Match(tool, @"(?m)^\s*dotnet\s+build\b").Index;

        Assert.True(install >= 0, "tools/verify-release.ps1 does not install the tag's SDK with tools/install-dotnet.ps1");
        Assert.True(path > install && path < build, "tools/verify-release.ps1 does not put the SDK's folder first on PATH before it builds");
        Assert.Contains("$env:DOTNET_ROOT = $dotnet", tool, StringComparison.Ordinal);
    }

    static string Workflow() =>
        File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), ".github", "workflows", "release.yml"));
}
