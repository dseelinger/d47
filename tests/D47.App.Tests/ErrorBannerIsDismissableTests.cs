using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A fault the Commander has read and decided about stops taking a strip off the top of the
/// panel (list.md Phase 12).
/// <para>
/// The banner is the only thing that says a voice companion cannot speak, so it is loud on
/// purpose — and until now it stayed for the rest of the session, above the conversation,
/// whether or not there was anything left to do about it.
/// </para>
/// </summary>
public class ErrorBannerIsDismissableTests
{
    [AvaloniaFact]
    public void DismissingItPutsTheBannerAway()
    {
        var model = new PanelViewModel
        {
            ErrorText = "No ElevenLabs voice has been chosen. Pick one in Settings.",
        };

        var (window, view) = Open(model);

        var banner = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ErrorBanner");
        Assert.True(banner.IsVisible);

        Dismiss(view);

        Assert.False(model.HasError);
        Assert.Null(model.ErrorText);

        window.Close();
    }

    /// <summary>
    /// A dismiss, not a mute. The next fault sets the text again and the banner comes back with
    /// it — a companion that cannot speak still has to say so, however many times the Commander
    /// has waved the message away.
    /// </summary>
    [AvaloniaFact]
    public void TheNextFaultRaisesItAgain()
    {
        var model = new PanelViewModel { ErrorText = "The first thing that went wrong." };
        var (window, view) = Open(model);

        Dismiss(view);
        Assert.False(model.HasError);

        model.ErrorText = "The next thing that went wrong.";

        Assert.True(model.HasError);

        window.Close();
    }

    /// <summary>
    /// Including the same fault twice: eight sentences in a row failing to synthesise is eight
    /// raises of one message, and a Commander who dismissed the first has not asked to stop
    /// being told about the ninth.
    /// </summary>
    [AvaloniaFact]
    public void TheSameFaultRaisesItAgainToo()
    {
        const string Same = "No ElevenLabs voice has been chosen. Pick one in Settings.";

        var model = new PanelViewModel { ErrorText = Same };
        var (window, view) = Open(model);

        Dismiss(view);
        model.ErrorText = Same;

        Assert.True(model.HasError);

        window.Close();
    }

    private static void Dismiss(PanelView view)
    {
        var button = view.GetVisualDescendants().OfType<Button>()
            .Single(control => control.Name == "DismissErrorButton");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static (Window Window, PanelView View) Open(PanelViewModel model)
    {
        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, view);
    }
}
