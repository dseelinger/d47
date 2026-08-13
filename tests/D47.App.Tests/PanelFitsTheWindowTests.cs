using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The panel lays out inside the window it is given.
/// <para>
/// Reported from a running build at 0.5.7: "at 100% zoom the main window doesn't fit in HD".
/// The window was the symptom. The zoom host wraps the content in a <c>ScrollViewer</c> that
/// may scroll sideways, and a scroll viewer that may scroll sideways measures its child with
/// <em>infinite</em> available width — so the transcript's <c>TextWrapping="Wrap"</c> had
/// nothing to wrap against, one long sentence became one long line, and the content was as
/// wide as the longest thing d47 had ever said. Dragging the window wider never reached the
/// end of it.
/// </para>
/// </summary>
public class PanelFitsTheWindowTests
{
    private const double Width = 900;
    private const double Height = 600;

    private const string LongEnoughToOverflow =
        "We're holding in normal space at HIP 12099 1 b. *Expedition* reports full health — "
        + "eighty-one point seven seven light years to the jump, tank at one twenty-eight. A "
        + "sound frame, Commander, and nothing out here is going to trouble it.";

    [AvaloniaFact]
    public void ALineLongerThanTheWindowWrapsInsteadOfRunningOffTheEdge()
    {
        var panel = LaidOut(LongEnoughToOverflow);

        Assert.True(
            panel.Bounds.Width <= Width,
            $"The panel laid out {panel.Bounds.Width} wide inside a {Width} window, so the end "
            + "of the line is somewhere off the right edge.");
    }

    /// <summary>
    /// And it wrapped rather than being clipped or truncated: a single line of this text at
    /// this size is far wider than the window, so fitting it means it is on several lines.
    /// </summary>
    [AvaloniaFact]
    public void TheTextIsOnSeveralLinesRatherThanOneThatFitsByBeingCutOff()
    {
        var wrapped = Transcript(LaidOut(LongEnoughToOverflow)).TextLayout;
        var single = Transcript(LaidOut("Holding.")).TextLayout;

        Assert.Single(single.TextLines);

        Assert.True(
            wrapped.TextLines.Count > 1,
            "The transcript laid the sentence out on one line, so what fits the window is the "
            + "left-hand end of it and the rest is off the edge.");

        // Every line inside the window, which is the half that says it wrapped rather than
        // simply being clipped somewhere further down the tree.
        Assert.All(wrapped.TextLines, line => Assert.True(line.Width <= Width));
    }

    /// <summary>
    /// Zoomed in, the layout is narrower and drawn larger — a browser's text zoom rather than a
    /// picture scaled until it hangs off the side. The sideways scroll survives for content
    /// with a real minimum width; it is no longer what ordinary text does.
    /// </summary>
    [AvaloniaFact]
    public void ZoomingInRewrapsRatherThanPushingThePanelOffTheEdge()
    {
        var panel = LaidOut(LongEnoughToOverflow, zoom: 150);

        Assert.True(
            panel.Bounds.Width <= Width,
            $"At 150% the panel laid out {panel.Bounds.Width} wide inside a {Width} window.");
    }


    private static TextBlock Transcript(PanelView panel) =>
        panel.GetControl<Avalonia.Controls.SelectableTextBlock>("Transcript");

    private static PanelView LaidOut(string said, int zoom = 100)
    {
        var (settings, _, _) = TestSurface.Create();

        if (zoom != 100)
        {
            settings.Apply(
                D47.Core.Capabilities.Builtin.InterfaceCapability.ZoomKey,
                zoom.ToString(System.Globalization.CultureInfo.InvariantCulture),
                D47.Core.Configuration.SettingsCaller.Panel);
        }

        var model = new PanelViewModel();
        model.Append(said);

        var panel = new PanelView { DataContext = model };
        var window = new Window { Width = Width, Height = Height, Content = panel };

        ZoomHost.Attach(window, settings);
        window.Show();

        var bounds = new Rect(0, 0, Width, Height);

        // Twice: the first pass is what gives the scroll viewer a viewport, and the constraint
        // that viewport implies is only available to the second.
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Measure(bounds.Size);
        window.Arrange(bounds);

        return panel;
    }
}
