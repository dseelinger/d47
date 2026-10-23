using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Ctrl+1 to Ctrl+7, Ctrl+Tab and Alt+Left move between tabs and back (#1494).</summary>
[Trait("Category", "Integration")]
public class TheKeyboardSwitchesTabsAndGoesBackTests
{
    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 1280, height: 800);

        EveryTabDrawsOneHeaderTests.FurnishEveryTabButSettings(host.Panel);
        host.Panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("Sol", null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { }));
        Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static void Press(SettingsHost host, Key key, KeyModifiers modifiers, PhysicalKey physical = PhysicalKey.None)
    {
        host.Window.KeyPress(key, (RawInputModifiers)(int)modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void CtrlAndANumberShowsThatTabInBarOrder()
    {
        var host = Open();

        Press(host, Key.D3, KeyModifiers.Control, PhysicalKey.Digit3);
        Assert.Equal(PanelTab.Stories, host.Panel.Tab);

        Press(host, Key.D7, KeyModifiers.Control, PhysicalKey.Digit7);
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void CtrlAndTheNumberOfTheTabYouAreOnGoesToItsTop()
    {
        var host = Open();

        Press(host, Key.D5, KeyModifiers.Control, PhysicalKey.Digit5);
        Assert.Equal(PanelTab.Assets, host.Panel.Tab);

        Assert.True(host.Panel.Nav.Drill(new NavCrumb("ship:test", "Test")));
        Dispatcher.UIThread.RunJobs();
        Assert.False(host.Panel.Nav.AtRoot);

        Press(host, Key.D5, KeyModifiers.Control, PhysicalKey.Digit5);

        Assert.Equal(PanelTab.Assets, host.Panel.Tab);
        Assert.True(host.Panel.Nav.AtRoot);

        host.Close();
    }

    [AvaloniaFact]
    public void CtrlTabStepsAlongTheBarAndWraps()
    {
        var host = Open();

        Press(host, Key.D7, KeyModifiers.Control, PhysicalKey.Digit7);
        Press(host, Key.Tab, KeyModifiers.Control, PhysicalKey.Tab);
        Assert.Equal(PanelTab.Transcript, host.Panel.Tab);

        Press(host, Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, PhysicalKey.Tab);
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void ExtraModifiersAndOtherDigitsDoNothingNew()
    {
        var host = Open();
        var start = host.Panel.Tab;

        Press(host, Key.D3, KeyModifiers.Control | KeyModifiers.Shift, PhysicalKey.Digit3);
        Press(host, Key.D8, KeyModifiers.Control, PhysicalKey.Digit8);
        Press(host, Key.D9, KeyModifiers.Control, PhysicalKey.Digit9);
        Press(host, Key.D0, KeyModifiers.Control, PhysicalKey.Digit0);

        Assert.Equal(start, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void AltLeftGoesUpOneLevelLikeEscape()
    {
        var host = Open();

        Press(host, Key.D5, KeyModifiers.Control, PhysicalKey.Digit5);
        Assert.True(host.Panel.Nav.Drill(new NavCrumb("ship:test", "Test")));
        Dispatcher.UIThread.RunJobs();
        Assert.False(host.Panel.Nav.AtRoot);

        Press(host, Key.Left, KeyModifiers.Alt, PhysicalKey.ArrowLeft);

        Assert.True(host.Panel.Nav.AtRoot);
        Assert.Equal(PanelTab.Assets, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void AltLeftOnASegmentGoesBackAndLeavesTheSegment()
    {
        var host = Open();

        host.View.Reveal(D47.Core.Capabilities.Builtin.ConversationCapability.Id);
        Dispatcher.UIThread.RunJobs();

        var segment = host.View.GetVisualDescendants().OfType<Segment>().First(s => s.Bounds.Height > 0);
        segment.Focus();
        var before = segment.SelectedIndex;

        Press(host, Key.Left, KeyModifiers.Alt, PhysicalKey.ArrowLeft);

        Assert.Equal(before, segment.SelectedIndex);

        host.Close();
    }
}
