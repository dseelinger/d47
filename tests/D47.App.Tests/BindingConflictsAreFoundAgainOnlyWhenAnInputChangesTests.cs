using D47.App.Input;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the Transcript notice and the callout read: null until known, then found again only on a change (#2384).</summary>
public class BindingConflictsAreFoundAgainOnlyWhenAnInputChangesTests
{
    private const string BindsPath = @"C:\d47-test\watch.binds";

    /// <summary>Ctrl+Alt+O, D47's Show or hide the overlay key, is Elite's spotlight too.</summary>
    private static EliteBinds Binds()
    {
        var files = new MemoryFileSystem();

        files.WriteText(BindsPath, """
            <Root PresetName="Custom">
              <ShipSpotLightToggle>
                <Primary Device="Keyboard" Key="Key_O">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </ShipSpotLightToggle>
            </Root>
            """);

        return EliteBinds.Parse(files, BindsPath, "Custom", NullLogger.Instance);
    }

    private sealed class Inputs
    {
        public SettingsService Settings { get; } = TestSurface.Settings();

        public EliteBinds Binds { get; set; } = EliteBinds.None;

        public bool Settled { get; set; }

        public int Found { get; private set; }

        public BindingConflictWatch Watch() => new(
            () => Settings.Current,
            () =>
            {
                Found++;
                return [.. Settings.Sections.SelectMany(section => section.Rows)];
            },
            () => [],
            () => Binds,
            () => Settled,
            () => []);
    }

    [Fact]
    public void NothingIsKnownUntilTheBindsAreReadAndTheControllersHaveSettled()
    {
        var inputs = new Inputs();
        var watch = inputs.Watch();

        Assert.False(watch.Poll());
        Assert.Null(watch.Current);

        inputs.Binds = Binds();
        Assert.False(watch.Poll());
        Assert.Null(watch.Current);

        inputs.Settled = true;
        Assert.True(watch.Poll());
        Assert.Single(watch.Current!);
    }

    [Fact]
    public void TheCheckRunsAgainOnlyWhenAnInputChanged()
    {
        var inputs = new Inputs { Binds = Binds(), Settled = true };
        var watch = inputs.Watch();

        watch.Poll();
        watch.Poll();
        watch.Poll();
        Assert.Equal(1, inputs.Found);

        inputs.Settings.Replace("test", current => current with { Hotkeys = current.Hotkeys with { ShowOverlay = "Ctrl+Alt+K" } });

        Assert.True(watch.Poll());
        Assert.Equal(2, inputs.Found);
        Assert.Empty(watch.Current!);
    }

    [Fact]
    public void AControllerSettlingAgainKeepsTheLastList()
    {
        var inputs = new Inputs { Binds = Binds(), Settled = true };
        var watch = inputs.Watch();

        watch.Poll();
        inputs.Settled = false;

        Assert.False(watch.Poll());
        Assert.Single(watch.Current!);
    }

    [Fact]
    public void BindsThatCanNoLongerBeReadLeaveNothingKnown()
    {
        var inputs = new Inputs { Binds = Binds(), Settled = true };
        var watch = inputs.Watch();

        watch.Poll();
        inputs.Binds = EliteBinds.None;

        Assert.True(watch.Poll());
        Assert.Null(watch.Current);
        Assert.False(watch.Poll());
    }

    [Fact]
    public void ANewReadOfTheSameBindsIsNotAChange()
    {
        var inputs = new Inputs { Binds = Binds(), Settled = true };
        var watch = inputs.Watch();

        watch.Poll();
        inputs.Binds = Binds();

        Assert.False(watch.Poll());
        Assert.Equal(2, inputs.Found);
    }
}
