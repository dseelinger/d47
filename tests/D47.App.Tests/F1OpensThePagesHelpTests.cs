using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>F1 opens the help the HELP button opens, and closes it again (#1495).</summary>
public class F1OpensThePagesHelpTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths);
    }

    private static void PressF1(SettingsHost host)
    {
        host.Window.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None);
        Jobs();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void OnSettingsItOpensTheGuideForThePlaceThatIsOpen()
    {
        var host = Open();

        SettingsPageReading.Open(host.View, "voice");
        PressF1(host);

        Assert.True(host.Panel.Nav.Modal, "help took the panel");
        Assert.StartsWith(HelpLevel.Prefix, host.Panel.Nav.Trail[^1].Key, StringComparison.Ordinal);

        host.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void OnTheTranscriptItOpensTheHelpIndex()
    {
        var host = Open();

        host.Panel.Tab = PanelTab.Transcript;
        Jobs();
        PressF1(host);

        Assert.True(host.Panel.Nav.Modal, "help took the panel");
        Assert.Equal("Help", host.Panel.Nav.Trail[^1].Word);

        host.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void PressedAgainItPutsBackThePageThatWasThere()
    {
        var host = Open();

        SettingsPageReading.Open(host.View, "voice");
        PressF1(host);
        PressF1(host);

        Assert.False(host.Panel.Nav.Modal, "help was dismissed");
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);

        host.Close();
    }
}
