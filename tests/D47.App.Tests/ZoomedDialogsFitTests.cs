using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A dialog opened over a zoomed panel still fits inside itself (#145).
/// <para>
/// <b>The trap is written down twice in <see cref="ZoomHost"/> and was only defended once.</b> A
/// <see cref="ScrollViewer"/> that may scroll sideways measures its child with <em>infinite</em>
/// available width — that is what "may scroll sideways" means to a measure pass — so
/// <c>TextWrapping="Wrap"</c> underneath it has nothing to wrap against and never wraps, and
/// <c>TextTrimming</c> has nothing to trim against and never trims. <c>Attach</c> undoes that on
/// every viewport change, and its comment explains why at length. <c>Match</c> builds the same
/// scrolling host for every dialog and undid nothing, so the fault the main window was fixed for
/// in 0.57.0 was reintroduced for every dialog in the app the moment zoom left 100%.
/// </para>
/// <para>
/// <b>Invisible to every existing test, and this is why:</b> <c>Match</c> returns immediately at
/// 100% zoom, and 100% is what a headless test gets unless it says otherwise. So the picker tests
/// all drew a 520-wide window and passed while the Commander's own — at 150% — was three times
/// that, with the voice help on one unwrapped line and a horizontal scrollbar under it.
/// </para>
/// </summary>
public class ZoomedDialogsFitTests
{
    /// <summary>The Commander's own level, and the one the report came from.</summary>
    private const int Zoomed = 150;

    /// <summary>
    /// A window carrying a zoom host, which is what <see cref="ZoomHost.Match"/> looks the level
    /// up in — a dialog over an owner that has none is left at 100% and is not this test's case.
    /// </summary>
    private static Window Owner(int percent)
    {
        var settings = TestSurface.Settings();

        settings.Apply(
            InterfaceCapability.ZoomKey,
            percent.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SettingsCaller.Panel);

        var owner = new Window { Width = 1200, Height = 800, Content = new Border() };

        ZoomHost.Attach(owner, settings);

        return owner;
    }

    /// <summary>
    /// The voice picker as the report shows it: a long help paragraph, and rows whose labels run
    /// past the width of the window they are in.
    /// </summary>
    private static PickerRequest Voices() => new()
    {
        Prompt = "Voice",
        Help =
            "Which voice the core aboard speaks in. Kept per core, so switching persona switches "
            + "voice. Play a voice to hear it. This provider costs nothing. It must be a voice "
            + "from the selected provider, and clearing the row has d47 choose for this core again.",
        Choices = ["af_jessica", "af_kore", "af_nicole", "af_nova", "af_river"],
        Describe = id => $"{char.ToUpperInvariant(id[3])}{id[4..]} — female, American — Female, en-US",
        Current = "af_kore",
        DefaultDisplay = "the voice d47 picks for this core",
    };

    /// <summary>
    /// Nothing sticks out sideways, which is the whole report: the content is no wider than the
    /// window drawn around it, so there is nothing to scroll to.
    /// </summary>
    [AvaloniaFact]
    public void AZoomedPickerHasNothingToScrollSidewaysTo()
    {
        var owner = Owner(Zoomed);
        var picker = PickerWindow.For(Voices());

        ZoomHost.Match(picker, owner);

        picker.Show();
        Dispatcher.UIThread.RunJobs();

        var host = Assert.IsType<ScrollViewer>(picker.Content);

        Assert.True(
            host.Extent.Width <= host.Viewport.Width + 1,
            $"the picker is {host.Extent.Width} wide inside a {host.Viewport.Width} window.");

        picker.Close();
        owner.Close();
    }

    /// <summary>
    /// And the reason it fits: the help paragraph has a width to wrap against. Asserted on the
    /// text block rather than on the window, because "one line as long as the sentence" is the
    /// symptom a Commander actually reported seeing.
    /// </summary>
    [AvaloniaFact]
    public void TheHelpParagraphStillWraps()
    {
        var owner = Owner(Zoomed);
        var picker = PickerWindow.For(Voices());

        ZoomHost.Match(picker, owner);

        picker.Show();
        Dispatcher.UIThread.RunJobs();

        var help = picker.GetControl<TextBlock>("HelpText");

        Assert.True(
            help.Bounds.Width <= picker.Width,
            $"the help line is {help.Bounds.Width} wide in a {picker.Width} window.");

        picker.Close();
        owner.Close();
    }

    /// <summary>
    /// The rows trim to the list instead of setting the width of the window, which is the same
    /// fault seen from the other end — a voice label is long and is meant to end in an ellipsis.
    /// </summary>
    [AvaloniaFact]
    public void TheRowsTrimToTheListRatherThanWideningIt()
    {
        var owner = Owner(Zoomed);
        var picker = PickerWindow.For(Voices());

        ZoomHost.Match(picker, owner);

        picker.Show();
        Dispatcher.UIThread.RunJobs();

        var list = picker.GetControl<ListBox>("Choices");

        Assert.All(
            list.GetVisualDescendants().OfType<ListBoxItem>(),
            item => Assert.True(
                item.Bounds.Width <= picker.Width,
                $"a row is {item.Bounds.Width} wide in a {picker.Width} window."));

        picker.Close();
        owner.Close();
    }

    /// <summary>
    /// At 100% nothing is wrapped at all — <see cref="ZoomHost.Match"/> leaves the dialog alone,
    /// and the window is its own declared size. Here so the fix cannot be a constraint that
    /// quietly applies to every dialog whether or not it was zoomed.
    /// </summary>
    [AvaloniaFact]
    public void AnUnzoomedPickerIsLeftExactlyAsItWas()
    {
        var owner = Owner(100);
        var picker = PickerWindow.For(Voices());

        ZoomHost.Match(picker, owner);

        Assert.IsNotType<ScrollViewer>(picker.Content);
        Assert.Equal(520, picker.Width);

        owner.Close();
    }
}
