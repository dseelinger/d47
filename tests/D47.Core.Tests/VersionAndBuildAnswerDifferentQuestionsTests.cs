using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// About's Version and Build rows answer different questions and must not print the same string.
/// </summary>
public class VersionAndBuildAnswerDifferentQuestionsTests
{
    private const string Semantic = "0.78.0";
    private const string Full = "0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205";

    /// <summary>Handed only the full stamp, which is all the composition root has.</summary>
    private static IReadOnlyList<SettingRow> Rows(string build) =>
        AboutCapability.Create(
            new AppPaths(Path.Combine(Path.GetTempPath(), "d47-about-rows")),
            ReleaseVersion.Semantic(build),
            build,
            showChangelog: () => { },
            showChangelogOnline: () => { },
            addToStartMenu: () => { },
            startMenuWanted: () => true,
            setUpKeys: () => { },
            showCommunity: () => { }).Settings;

    private static string Value(IReadOnlyList<SettingRow> rows, string key) =>
        rows.Single(row => row.Key == key).Binding!.Read(D47Settings.Defaults) ?? string.Empty;

    [Fact]
    public void TheVersionRowShowsTheReleaseAndTheBuildRowShowsTheCommit()
    {
        var rows = Rows(Full);

        Assert.Equal(Semantic, Value(rows, AboutCapability.VersionKey));
        Assert.Equal(Full, Value(rows, AboutCapability.BuildKey));
    }

    /// <summary>
    /// The property that matters, stated as itself: whatever the two are, they are not the same answer.
    /// </summary>
    [Fact]
    public void AndTheyAreNeverTheSameString()
    {
        var rows = Rows(Full);

        Assert.NotEqual(
            Value(rows, AboutCapability.VersionKey),
            Value(rows, AboutCapability.BuildKey));
    }

    [Fact]
    public void AndTheVersionRowCarriesNoCommitStamp()
    {
        var version = Value(Rows(Full), AboutCapability.VersionKey);

        Assert.DoesNotContain("+", version, StringComparison.Ordinal);
        Assert.True(
            version.Length <= 20,
            $"the Version row reads \"{version}\", which is long enough to be carrying a commit.");
    }
}
