using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Timekeeping;
using D47.App.Windowing;
using D47.Core;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Timers and alarms exist only for a run started with the switch (#90).</summary>
public class TimersAndAlarmsAreOffUnlessARunAsksTests : IDisposable
{
    /// <summary>The switch and the variable are process-wide state, so both are put back.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        TimersAndAlarms.ReadCommandLine([]);
        Environment.SetEnvironmentVariable(TimersAndAlarms.EnvironmentVariable, null);
    }

    [Fact]
    public void AnOrdinaryLaunchLeavesThemOff()
    {
        TimersAndAlarms.ReadCommandLine(["--selftest"]);

        Assert.False(TimersAndAlarms.Enabled);
    }

    [Fact]
    public void TheSwitchTurnsThemOn()
    {
        TimersAndAlarms.ReadCommandLine(["--utilities"]);

        Assert.True(TimersAndAlarms.Enabled);
    }

    [Fact]
    public void TheVariableTurnsThemOn()
    {
        TimersAndAlarms.ReadCommandLine([]);
        Environment.SetEnvironmentVariable(TimersAndAlarms.EnvironmentVariable, "1");

        Assert.True(TimersAndAlarms.Enabled);
    }

    [Theory]
    [InlineData("--utility")]
    [InlineData("--Utilities")]
    [InlineData("utilities")]
    public void ANearMissIsNotTheSwitch(string argument)
    {
        TimersAndAlarms.ReadCommandLine([argument]);

        Assert.False(TimersAndAlarms.Enabled);
    }

    /// <summary>Per run: nothing is written to settings, and the next launch without the switch is off again.</summary>
    [Fact]
    public void TheSwitchLastsOneRunAndIsNotSaved()
    {
        var paths = Paths();

        TimersAndAlarms.ReadCommandLine([TimersAndAlarms.Flag]);
        Assert.NotNull(TimersAndAlarms.Create(paths, NullLoggerFactory.Instance));

        Assert.False(File.Exists(paths.SettingsFile));

        TimersAndAlarms.ReadCommandLine([]);
        Assert.Null(TimersAndAlarms.Create(paths, NullLoggerFactory.Instance));
    }

    /// <summary>Off: no stores, and no date or reminder list for the game-state block.</summary>
    [Fact]
    public void OffNothingIsComposedAndTheGameStateCarriesNoDate()
    {
        TimersAndAlarms.ReadCommandLine([]);

        var clocks = TimersAndAlarms.Create(Paths(), NullLoggerFactory.Instance);

        Assert.Null(clocks);
        Assert.Null(TimersAndAlarms.Live(clocks, DateTimeOffset.UnixEpoch.AddYears(56), TimeZoneInfo.Utc));
    }

    /// <summary>On: the game-state block carries both dates and what is running, as it always has.</summary>
    [Fact]
    public void OnTheGameStateCarriesBothDatesAndWhatIsRunning()
    {
        TimersAndAlarms.ReadCommandLine([TimersAndAlarms.Flag]);

        var now = new DateTimeOffset(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);
        var clocks = TimersAndAlarms.Create(Paths(), NullLoggerFactory.Instance);

        Assert.NotNull(clocks);
        clocks.Timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), now);

        var live = TimersAndAlarms.Live(clocks, now, TimeZoneInfo.Utc);

        Assert.NotNull(live);
        Assert.Contains("3312", live, StringComparison.Ordinal);
        Assert.Contains("2026", live, StringComparison.Ordinal);
        Assert.Contains("Running: mining run", live, StringComparison.Ordinal);
    }

    /// <summary>
    /// Off, the desktop panel, the headset and the strip are handed nothing for the tab, and each refuses
    /// it and stays on the transcript.
    /// </summary>
    [AvaloniaFact]
    public void OffNoSurfaceCarriesTheTab()
    {
        TimersAndAlarms.ReadCommandLine([]);

        var (desktop, headset, overlay) = Surfaces();

        foreach (var nav in new[] { desktop.Nav, headset.Nav, overlay.Nav })
        {
            Assert.False(nav.Has(PanelTab.Utilities));

            nav.Select(PanelTab.Utilities);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(PanelTab.Transcript, nav.Tab);
        }

        overlay.Close();
    }

    [AvaloniaFact]
    public void OnEverySurfaceCarriesTheTab()
    {
        TimersAndAlarms.ReadCommandLine([TimersAndAlarms.Flag]);

        var (desktop, headset, overlay) = Surfaces();

        Assert.True(desktop.Nav.Has(PanelTab.Utilities));
        Assert.True(headset.Nav.Has(PanelTab.Utilities));
        Assert.True(overlay.Nav.Has(PanelTab.Utilities));

        overlay.Close();
    }

    /// <summary>The three surfaces, wired the way the host wires them from whatever the switch composed.</summary>
    private static (PanelView Desktop, VrPanelSurface Headset, OverlayPanel Overlay) Surfaces()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var (settings, viewState, paths) = TestSurface.Create();
        var clocks = TimersAndAlarms.Create(paths, NullLoggerFactory.Instance);
        var adventures = AdventureFixture.Surface(paths);

        var desktop = new PanelView { DataContext = new PanelViewModel() };

        if (clocks is not null)
        {
            desktop.EnableUtilities(
                clocks.Timekeeper, clocks.Alarms, () => DateTimeOffset.UnixEpoch, () => TimeZoneInfo.Utc);
        }

        var headset = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            settingsPage: () => new Avalonia.Controls.TextBlock { Text = "settings" },
            timekeeper: clocks?.Timekeeper,
            alarmStore: clocks?.Alarms,
            adventures: adventures);

        var overlay = new OverlayPanel(
            new PanelViewModel(),
            settings,
            viewState,
            NullLogger<OverlayPanel>.Instance,
            avatars: null,
            adventures: adventures,
            tabs: new OverlayTabs
            {
                Timekeeper = clocks?.Timekeeper,
                Alarms = clocks?.Alarms,
            });

        Dispatcher.UIThread.RunJobs();

        return (desktop, headset, overlay);
    }

    private static AppPaths Paths()
    {
        var paths = new AppPaths(TempFolders.Create("d47-timers-switch"));
        paths.EnsureCreated();

        return paths;
    }
}
