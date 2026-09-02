using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Switching core puts a marked line in the conversation, because a transcript that omits the
/// switch reads as one companion changing character mid-page. The panel is saying this, not
/// whoever is aboard, so it has to be told apart from the lines around it without being read —
/// which is a colour, not a form of words.
/// </summary>
public class PersonaSwitchMarkTests
{
    [Fact]
    public void AMarkIsItsOwnSegmentAndStaysOutOfTheLinesAroundIt()
    {
        var model = new PanelViewModel();

        model.Append("Standing by, Commander.");
        model.Mark("Switched to Cora");
        model.Append("Cora online.");

        Assert.Contains("[Switched to Cora]", model.TranscriptText, StringComparison.Ordinal);

        var segments = model.Segments(TranscriptPage.Conversation);

        // Three, in order, and only the middle one is marked. Two would mean the mark had been
        // merged into a neighbour and the whole reply would be drawn as the panel's own voice.
        Assert.Equal(3, segments.Count);
        Assert.Equal([false, true, false], segments.Select(segment => segment.Marker));
        Assert.Equal("\n[Switched to Cora]\n", segments[1].Text);
    }

    /// <summary>
    /// A mark survives lines written either side of it, which is the case it exists for: the core
    /// changing is a thing that happened <em>to</em> the conversation, mid-conversation.
    /// </summary>
    /// <remarks>
    /// This asserted the mark reached the Technical reading as well until #260 deleted it. There
    /// is one reading of the runs now, so "both pages agree" is not a question that can be asked
    /// — what is left is the half that was always the point.
    /// </remarks>
    [Fact]
    public void TheMarkSurvivesTheLinesAroundIt()
    {
        var model = new PanelViewModel();

        model.Append("Standing by.");
        model.Mark("Switched to Kex");
        model.Append("Kex online.");

        Assert.Contains("[Switched to Kex]", model.TranscriptText, StringComparison.Ordinal);
        Assert.Contains(model.Segments(TranscriptPage.Conversation), segment => segment.Marker);
    }

    /// <summary>
    /// And the panel draws it in the accent while the conversation around it keeps the colour it
    /// had. This is the half a view model cannot assert: the mark exists to be seen.
    /// </summary>
    [AvaloniaFact]
    public void ThePanelDrawsTheMarkInTheAccentAndNothingElse()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(themeId: null);

        var model = new PanelViewModel();
        model.Append("Standing by, Commander.");
        model.Mark("Switched to Cora");

        var panel = new PanelView { DataContext = model };
        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();
        window.Measure(new Size(900, 560));
        window.Arrange(new Rect(0, 0, 900, 560));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var runs = panel.TranscriptRuns.ToArray();

        var marked = Assert.Single(runs, run => run.Text!.Contains("[Switched to Cora]", StringComparison.Ordinal));
        var spoken = Assert.Single(runs, run => run.Text!.Contains("Standing by", StringComparison.Ordinal));

        var accent = (IBrush?)window.FindResource(ThemeManager.AccentKey);

        Assert.Equal(accent?.ToString(), marked.Foreground?.ToString());
        Assert.NotEqual(accent?.ToString(), spoken.Foreground?.ToString());
        Assert.Equal(FontWeight.SemiBold, marked.FontWeight);

        window.Close();
    }
}
