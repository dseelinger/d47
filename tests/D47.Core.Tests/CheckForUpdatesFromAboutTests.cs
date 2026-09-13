using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The Version row's own way to check for an update, and the Install row that appears once one is
/// found (#193).
/// </summary>
public class CheckForUpdatesFromAboutTests
{
    private static IReadOnlyList<SettingRow> Rows(
        LongPress? checkForUpdate = null,
        LongPress? installUpdate = null,
        Func<string?>? pendingUpdateVersion = null) =>
        AboutCapability.Create(
            new AppPaths(Path.Combine(Path.GetTempPath(), "d47-about-rows")),
            "0.78.0",
            "0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205",
            checkForUpdate: checkForUpdate,
            installUpdate: installUpdate,
            pendingUpdateVersion: pendingUpdateVersion).Settings;

    [Fact]
    public void WithNoCheckSuppliedTheVersionRowOffersNoButton()
    {
        var version = Rows().Single(row => row.Key == AboutCapability.VersionKey);

        Assert.Null(version.PressAsync);
        Assert.Null(version.PressLabel);
    }

    [Fact]
    public void ChecKingForUpdatesIsTheVersionRowsOwnButton()
    {
        var version = Rows(checkForUpdate: (_, _) => Task.FromResult<string?>("checked"))
            .Single(row => row.Key == AboutCapability.VersionKey);

        Assert.NotNull(version.PressAsync);
        Assert.Equal("Check for updates", version.PressLabel);
    }

    [Fact]
    public void WithNoInstallSuppliedThereIsNoInstallRow()
    {
        Assert.DoesNotContain(Rows(), row => row.Key == AboutCapability.InstallUpdateKey);
    }

    [Fact]
    public void TheInstallRowIsAbsentWithNothingPending()
    {
        var install = Rows(
                installUpdate: (_, _) => Task.FromResult<string?>(null),
                pendingUpdateVersion: () => null)
            .Single(row => row.Key == AboutCapability.InstallUpdateKey);

        Assert.False(install.Applies(D47Settings.Defaults));
    }

    [Fact]
    public void TheInstallRowAppearsAndNamesTheVersionOncePending()
    {
        var install = Rows(
                installUpdate: (_, _) => Task.FromResult<string?>(null),
                pendingUpdateVersion: () => "0.111.0")
            .Single(row => row.Key == AboutCapability.InstallUpdateKey);

        Assert.True(install.Applies(D47Settings.Defaults));
        Assert.Equal("Install 0.111.0", install.PressLabelFor!());
    }
}
