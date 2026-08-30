using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The trust rule has exactly one definition, and everything that needs it asks that one
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// <para>
/// <b>Because a control copied is two controls that will disagree.</b> <c>Resolve-Trust</c> is the
/// whole of "may this text be shown" — it is what keeps a stranger's issue prose out of an agent's
/// context, and since #207 it is also what decides whose issue titles a local build may stamp into
/// itself. Those are different risks and the same text, so they get the same door. A second copy
/// would be a door somebody fixes once.
/// </para>
/// <para>
/// Asserted over the scripts rather than reasoned about, because a copy is exactly what a hurried
/// change makes: the function is forty lines, it is self-contained, and pasting it is easier than
/// finding out where it lives.
/// </para>
/// </summary>
public class OneAnswerToWhoseTextMayBeShownTests
{
    /// <summary>Every PowerShell script this repository ships, by path.</summary>
    private static IEnumerable<string> Scripts() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "tools"), "*.ps1", SearchOption.AllDirectories);

    [Fact]
    public void TheTrustRuleIsDefinedOnce()
    {
        var defining = Scripts()
            .Where(path => File.ReadAllText(path).Contains("function Resolve-Trust", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["issues.lib.ps1"], defining);
    }

    /// <summary>
    /// And so is the answer to "which issues did this build work". <c>prerelease.ps1</c> decides a
    /// version number from it and a version number never moves; <c>get-local.ps1</c> lists it on a
    /// badge. Two extractions would be two definitions of the same window, free to differ on the
    /// run that mattered.
    /// </summary>
    [Fact]
    public void TheClosedIssueExtractionIsDefinedOnceToo()
    {
        var defining = Scripts()
            .Where(path => File.ReadAllText(path).Contains("function Get-ClosedIssueNumbers", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["issues.lib.ps1"], defining);

        // And nobody has quietly gone back to writing the regex out by hand.
        var open = Scripts()
            .Where(path => !string.Equals(Path.GetFileName(path), "issues.lib.ps1", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("fixes|closes|resolves", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(open);
    }

    /// <summary>
    /// The three callers reach it the same way. A script that defined its own copy of
    /// <c>Invoke-Gh</c> beside a dot-source of this file would be half-using the shared answer,
    /// which is the shape that rots quietest.
    /// </summary>
    [Theory]
    [InlineData("issues.ps1")]
    [InlineData("prerelease.ps1")]
    [InlineData("get-local.ps1")]
    public void EveryCallerDotSourcesItRatherThanRepeatingIt(string script)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", script));

        Assert.Contains("issues.lib.ps1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("function Invoke-Gh", text, StringComparison.Ordinal);
        Assert.DoesNotContain("function Invoke-Native", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And no publish step prints a title.</b> The whole point of <c>issues.ps1</c> is that
    /// untrusted issue prose does not enter a terminal an agent is reading; a <c>get-local</c> that
    /// echoed what it was baking would walk it straight back in through the one channel this
    /// repository trusts. Numbers and a count are what it says out loud.
    /// </summary>
    [Fact]
    public void GetLocalNeverPrintsAnIssueTitle()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "get-local.ps1"));

        var printing = text
            .Split('\n')
            .Where(line => line.Contains("Write-Host", StringComparison.Ordinal)
                           || line.Contains("Write-Note", StringComparison.Ordinal)
                           || line.Contains("Write-Step", StringComparison.Ordinal))
            .Where(line => line.Contains(".title", StringComparison.OrdinalIgnoreCase)
                           || line.Contains("$title", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(printing);
    }

    /// <summary>
    /// The stamp is passed as one inert token. A JSON value would need quoting through PowerShell,
    /// cmd and MSBuild in turn, and part of it is text somebody else wrote — the encoding is what
    /// makes that a question nobody has to answer.
    /// </summary>
    [Fact]
    public void TheStampCrossesTheCommandLineEncoded()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "get-local.ps1"));

        Assert.Contains("ToBase64String", text, StringComparison.Ordinal);

        // Release, never Debug, and the property rides on that same publish rather than on a
        // second one somebody could run in the wrong configuration.
        //
        // **A variable, never a parenthesised call.** PowerShell does not evaluate an expression
        // inside a native-command argument: `-p:Key=(Get-Thing)` passes `-p:Key=` and then the
        // result as a *separate* argument, which MSBuild read as a second project and refused.
        // Found by driving it rather than by the suite (#207).
        Assert.Contains(
            "dotnet publish $project -c Release -p:Version=$version -p:LocalBuildIssues=$stamp",
            text,
            StringComparison.Ordinal);

        Assert.DoesNotContain("-p:LocalBuildIssues=(", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>No caller shadows the library's repository name</b>, and this guard exists because one
    /// did (#207).
    /// <para>
    /// PowerShell variable names are case-insensitive, so a script that sets <c>$repo</c> to its
    /// checkout path is setting the library's <c>$Repo</c>. <c>get-local.ps1</c> does exactly that
    /// on its first line of work, and every <c>gh</c> call then went out with
    /// <c>--repo C:\dev\d47</c>. It failed the way being offline fails, was caught by the
    /// fail-soft path that exists for being offline, and stamped ten issues as unknown — with no
    /// symptom but a warning that reads like a network problem.
    /// </para>
    /// <para>
    /// The name is <c>$IssueRepo</c> now, which no path variable is going to collide with. This
    /// asserts the collision cannot come back under either spelling.
    /// </para>
    /// </summary>
    [Fact]
    public void NoCallerCanShadowTheLibrarysOwnNames()
    {
        var shared = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "issues.lib.ps1"));

        // Unindented only, which is exactly the set that lands in the caller's own scope. A local
        // inside one of the library's functions is scoped to it and collides with nothing.
        var names = shared
            .Split('\n')
            .Where(line => line.StartsWith('$') && line.Contains('='))
            .Select(line => line[1..line.IndexOf('=', StringComparison.Ordinal)].Trim())
            .ToList();

        Assert.NotEmpty(names);

        foreach (var script in new[] { "issues.ps1", "prerelease.ps1", "get-local.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", script));

            foreach (var name in names)
            {
                Assert.DoesNotContain(
                    text.Split('\n').Select(line => line.TrimStart()),
                    line => line.StartsWith($"${name} =", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>
    /// And the csproj only writes the attribute when the property is set, so a published release
    /// carries none — which is what makes the feature absent from a real build by construction
    /// rather than by a run-time check.
    /// </summary>
    [Fact]
    public void APublishedBuildCarriesNoStampAtAll()
    {
        var csproj = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "D47.App", "D47.App.csproj"));

        Assert.Contains(
            "<ItemGroup Condition=\"'$(LocalBuildIssues)' != ''\">",
            csproj,
            StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
