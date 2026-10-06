using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Adventures tab offers no way to write or edit an adventure: a Commander asks D47 for one.</summary>
public class AnAdventureIsAskedForNotWrittenTests
{
    private static IReadOnlyList<string> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content as string ?? string.Empty)];

    [AvaloniaFact]
    public void TheRootHasNoWriteButton()
    {
        var (panel, _, _) = AdventuresTabTests.Open();

        Assert.Contains("Ask for one", Buttons(panel));
        Assert.DoesNotContain("Write an adventure", Buttons(panel));
    }

    [AvaloniaFact]
    public void AnAbandonedAdventureD47WroteHasNoEdit()
    {
        var now = new DateTimeOffset(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);
        var (panel, book, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(now, AdventureSource.Generated));

        book.Abandon("F1", "the-lantern-route", now.AddMinutes(5));

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var buttons = Buttons(panel);

        Assert.Contains("Begin again", buttons);
        Assert.DoesNotContain("Edit", buttons);
    }

    [AvaloniaFact]
    public void ADraftD47WroteHasNoEdit()
    {
        var (panel, _, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(null, AdventureSource.Generated));

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var buttons = Buttons(panel);

        Assert.Contains("Accept", buttons);
        Assert.DoesNotContain("Edit", buttons);
    }
}
