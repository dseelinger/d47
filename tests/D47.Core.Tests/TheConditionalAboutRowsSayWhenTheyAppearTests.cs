using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The two About rows that are absent most of the time say, in their exercise steps, what brings them on.</summary>
public class TheConditionalAboutRowsSayWhenTheyAppearTests
{
    private static IReadOnlyList<SettingRow> Rows() =>
        AboutCapability.Create(
            new AppPaths(Path.Combine(MemoryInstall.FakeRoot, "d47-about-exercise")),
            "0.78.0",
            "0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205",
            addToStartMenu: () => { },
            installUpdate: (_, _) => Task.FromResult<string?>(null)).Settings;

    [Fact]
    public void InstallSaysItAppearsOnlyAfterCheckForUpdatesFindsANewerRelease()
    {
        var steps = Rows().Single(row => row.Key == AboutCapability.InstallUpdateKey).Exercise;

        Assert.NotNull(steps);
        Assert.Contains("appears only after Check for updates", steps, StringComparison.Ordinal);
        Assert.Contains("newer release", steps, StringComparison.Ordinal);
    }

    [Fact]
    public void AddToStartMenuSaysItAppearsOnlyWithoutAShortcutAndDeletingItBringsItBack()
    {
        var steps = Rows().Single(row => row.Key == AboutCapability.StartMenuKey).Exercise;

        Assert.NotNull(steps);
        Assert.Contains("appears only when there is no Directive 47 shortcut", steps, StringComparison.Ordinal);
        Assert.Contains("Delete the shortcut", steps, StringComparison.Ordinal);
        Assert.Contains("comes back", steps, StringComparison.Ordinal);
    }
}
