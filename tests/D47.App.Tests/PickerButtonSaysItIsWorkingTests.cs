using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A row that opens a picker says it is working while the picker is being built.</summary>
[Trait("Category", "Integration")]
public class PickerButtonSaysItIsWorkingTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        host.ShowPlaceOf(SpeechCapability.OutputDeviceKey);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>The mark is built with the row and starts hidden: nothing moves on a surface that is merely open.</summary>
    [AvaloniaFact]
    public void ThePickerRowCarriesAWaitMarkThatStartsHidden()
    {
        var host = Open();

        var marks = host.View.GetVisualDescendants().OfType<WaitingState>().ToList();

        Assert.NotEmpty(marks);
        Assert.All(marks, mark => Assert.False(mark.IsVisible));

        host.Close();
    }

    /// <summary>
    /// And it is the microphone row's own, next to the button that opens its picker — the row the
    /// Commander reported this against.
    /// </summary>
    [AvaloniaFact]
    public void TheMicrophoneRowHasOneBesideItsButton()
    {
        var host = Open();

        // Any panel, not a StackPanel specifically: this was one, until a horizontal StackPanel turned out to
        // measure its children with no width limit and let the button run out past the column.
        var row = host.View
            .GetVisualDescendants()
            .OfType<Avalonia.Controls.Panel>()
            .FirstOrDefault(panel =>
                panel.Children.OfType<WaitingState>().Any() && panel.Children.OfType<Button>().Any());

        Assert.NotNull(row);

        // Before the button, so it appears where the eye already is rather than off past the end of a control
        // whose width follows its contents.
        Assert.IsType<WaitingState>(row.Children[0]);
        Assert.IsType<Button>(row.Children[1]);
        Assert.Equal(Dock.Left, DockPanel.GetDock(row.Children[0]));

        host.Close();
    }
}
