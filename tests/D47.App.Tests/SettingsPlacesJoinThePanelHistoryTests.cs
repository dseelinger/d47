using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Moving between Settings places is a step the Back controls reverse, one place at a time.</summary>
[Trait("Category", "Integration")]
public class SettingsPlacesJoinThePanelHistoryTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths);
    }

    private static void MouseBack(SettingsHost host)
    {
        host.Window.MouseDown(new Avalonia.Point(640, 400), MouseButton.XButton1);
        host.Window.MouseUp(new Avalonia.Point(640, 400), MouseButton.XButton1);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Show(SettingsHost host, int index)
    {
        host.View.ShowPlace(index);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void BackReturnsThroughEachPlaceThenLeavesSettings()
    {
        var host = Open();
        var first = host.View.ActiveSection;

        Show(host, first + 1);
        Show(host, first + 2);
        Assert.Equal(first + 2, host.View.ActiveSection);

        MouseBack(host);
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);
        Assert.Equal(first + 1, host.View.ActiveSection);

        MouseBack(host);
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);
        Assert.Equal(first, host.View.ActiveSection);

        MouseBack(host);
        Assert.Equal(PanelTab.Transcript, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void TypingInTheSearchBoxAddsNoStep()
    {
        var host = Open();
        var first = host.View.ActiveSection;

        Show(host, first + 1);
        host.Search("volume");
        host.Search(string.Empty);

        MouseBack(host);
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);
        Assert.Equal(first, host.View.ActiveSection);

        MouseBack(host);
        Assert.Equal(PanelTab.Transcript, host.Panel.Tab);

        host.Close();
    }

    [AvaloniaFact]
    public void ThePlaceBackReturnsToIsTheOneRememberedForNextStart()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        var first = host.View.ActiveSection;
        var firstId = host.View.OpenPlaceId;

        Show(host, first + 1);
        Show(host, first + 2);
        Assert.NotEqual(firstId, viewState.Load().SettingsSection);

        MouseBack(host);
        MouseBack(host);

        Assert.Equal(first, host.View.ActiveSection);
        Assert.Equal(firstId, viewState.Load().SettingsSection);

        host.Close();
    }
}
