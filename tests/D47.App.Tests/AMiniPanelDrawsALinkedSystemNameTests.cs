using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>On an output-only surface a linked system name is drawn as plain text and the copy glyph is hidden.</summary>
[Trait("Category", "Integration")]
public class AMiniPanelDrawsALinkedSystemNameTests
{
    private const string Far = "Dyson's Hollow";

    private static PanelView Open(string mode)
    {
        var (panel, book, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(new DateTimeOffset(2026, 8, 22, 19, 0, 0, TimeSpan.Zero)));

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-22T19:30:00Z", "event":"FSDJump", "StarSystem":"Ossen's Lantern", "SystemAddress":1 }""",
            NullLogger.Instance,
            out var jump));
        book.Observe(jump!, "F1");

        panel.EnableCopy(new RecordingClipboard());

        if (mode == "mini")
        {
            panel.Classes.Add("output-only");
            panel.Mode = PanelMode.Mini;
        }
        else
        {
            panel.Mode = PanelMode.Mini;
        }

        Dispatcher.UIThread.RunJobs();
        return panel;
    }

    private static TextBlock? Name(PanelView panel) =>
        panel.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Text == Far && block.IsEffectivelyVisible);

    [AvaloniaFact]
    public void TheMiniPanelDrawsTheSystemNameNotPressable()
    {
        var panel = Open("mini");

        var name = Name(panel);

        Assert.NotNull(name);
        Assert.Equal(Far, name.Text);
        Assert.Null(name.TextDecorations);
        Assert.DoesNotContain(
            name.GetSelfAndVisualAncestors().OfType<Control>().TakeWhile(c => c is not PanelView),
            c => c is Button && c.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void TheMiniPanelHidesTheCopyGlyph()
    {
        var panel = Open("mini");

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Content is not Border);
    }

    [AvaloniaFact]
    public void TheNameIsALinkAgainWhenTheSurfaceStopsBeingOutputOnly()
    {
        var panel = Open("mini");

        Assert.NotNull(Name(panel));

        panel.Classes.Remove("output-only");
        Dispatcher.UIThread.RunJobs();

        var name = Name(panel);

        Assert.NotNull(name);
        Assert.NotNull(name.TextDecorations);
        Assert.Contains(
            name.GetVisualAncestors().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.IsHitTestVisible);
    }
}
