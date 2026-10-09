using Xunit;

namespace D47.App.Tests;

/// <summary>About's way back into the setup wizard.</summary>
public class AboutReopensTheSetupTests
{
    /// <summary>
    /// Reachable again after first run, because keys get rotated and revoked.
    /// </summary>
    [Fact]
    public void AboutOffersTheKeySetupAgain()
    {
        var (_, _, paths) = TestSurface.Create();
        var opened = 0;

        var about = D47.Core.Capabilities.Builtin.AboutCapability.Create(
            paths,
            "1.2.3",
            "1.2.3+abcdef0",
            setUpKeys: () => opened++);

        var row = about.Settings.Single(
            candidate => candidate.Key == D47.Core.Capabilities.Builtin.AboutCapability.SetUpKeysKey);

        Assert.NotNull(row.Press);
        row.Press!();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AboutHidesTheButtonWhenThereIsNothingToOpen()
    {
        var (_, _, paths) = TestSurface.Create();

        var about = D47.Core.Capabilities.Builtin.AboutCapability.Create(
            paths,
            "1.2.3",
            "1.2.3+abcdef0");

        Assert.DoesNotContain(
            about.Settings,
            row => row.Key == D47.Core.Capabilities.Builtin.AboutCapability.SetUpKeysKey);
    }
}
