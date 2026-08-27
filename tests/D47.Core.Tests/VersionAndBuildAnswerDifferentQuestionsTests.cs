using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// About's <b>Version</b> and <b>Build</b> rows answer different questions and must not print the
/// same string (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
/// <para>
/// <b>They did.</b> Both read <c>0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205</c>, because the
/// composition root passed <c>BuildInfo.Full</c> as the version as well as the build. So the row
/// whose help says <em>"Which release this is"</em> answered with forty characters of commit hash,
/// and the row beside it — whose help says a version alone <em>"cannot tell two builds of the same
/// release apart"</em> — proved its own point by being identical.
/// </para>
/// <para>
/// <b>The values were never missing.</b> <c>BuildInfo</c> has always carried both, the title bar
/// has always used the short one, and <c>AboutCapability.Create</c>'s own parameter documents the
/// contract: <em>"The version a Commander would quote — BuildInfo.Semantic"</em>. One call site
/// ignored it, and nothing asserted the rows, so it drifted quietly from the day About stopped
/// being a window.
/// </para>
/// <para>
/// Asserted here rather than in the App because it is a property of the rows rather than of the
/// window: the two rows must differ whatever a host hands them, which is a statement about the
/// contract and not about one composition.
/// </para>
/// </summary>
public class VersionAndBuildAnswerDifferentQuestionsTests
{
    private const string Semantic = "0.78.0";
    private const string Full = "0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205";

    /// <summary>
    /// <b>Handed only the full stamp</b>, which is all the composition root has. The defect was
    /// one string used for both rows; if that is still expressible, it shows up here.
    /// </summary>
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
    /// The property that matters, stated as itself: whatever the two are, they are not the same
    /// answer. This is what fires when a composition root passes one value twice.
    /// </summary>
    [Fact]
    public void AndTheyAreNeverTheSameString()
    {
        var rows = Rows(Full);

        Assert.NotEqual(
            Value(rows, AboutCapability.VersionKey),
            Value(rows, AboutCapability.BuildKey));
    }

    /// <summary>
    /// The version row carries no commit stamp at all. Equality alone would pass a Version row
    /// showing the full string beside a Build row that had been changed to something else — the
    /// rows would differ and the Version row would still be answering the wrong question.
    /// </summary>
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
