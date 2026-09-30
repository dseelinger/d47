using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Typing into a text row and then opening another settings page saves the text, and does not crash.</summary>
public class LeavingATextBoxForAnotherPageKeepsTheTextTests
{

    [AvaloniaFact]
    public void OpeningAnotherPageWhileATextRowIsFocusedSavesIt()
    {
        using var _ = AppLook.ControlKit();
        (SettingsService settings, var viewState, var paths) = TestSurface.Create();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        var host = SettingsHost.Open(settings, viewState, paths, width: 1180, height: 800);

        Assert.True(host.View.ShowPlaceOf(CalloutCapability.HomeSystemKey));
        Dispatcher.UIThread.RunJobs();

        var here = host.View.ActiveSection;
        var label = host.View.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.IsEffectivelyVisible && string.Equals(text.Text, "Home system", StringComparison.OrdinalIgnoreCase));
        var box = label.GetVisualAncestors().OfType<Control>()
            .Select(ancestor => ancestor.GetVisualDescendants().OfType<TextBox>().FirstOrDefault())
            .First(found => found is not null)!;
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsFocused);
        box.Text = "Diaguandri";

        host.View.ShowPlace(here == 0 ? 1 : 0);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Diaguandri", settings.Read(CalloutCapability.HomeSystemKey));

        host.Close();
    }
}
