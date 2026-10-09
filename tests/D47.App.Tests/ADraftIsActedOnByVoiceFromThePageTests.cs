using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The voice commands act on the draft whose page is open, else on the only draft, and otherwise change nothing.</summary>
[Trait("Category", "Integration")]
public class ADraftIsActedOnByVoiceFromThePageTests
{
    private static Adventure Draft(string key) =>
        AdventuresTabTests.Story(null, AdventureSource.Generated) with { Key = key, Name = key };

    private static (PanelView Panel, AdventureBook Book, AdventureCapability.AdventureDesk Desk) Open(params Adventure[] adventures)
    {
        var surface = AdventureFixture.Surface();

        foreach (var adventure in adventures)
        {
            Assert.Null(surface.Book.Write("F1", adventure));
        }

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var desk = new AdventureCapability.AdventureDesk();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface, desk: desk);

        new Window { Content = panel, Width = 900, Height = 700 }.Show();
        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return (panel, surface.Book, desk);
    }

    [AvaloniaFact]
    public void TheOnlyDraftIsAcceptedWithoutItsPageOpen()
    {
        var (_, book, desk) = Open(Draft("one"));

        Assert.Null(desk.Accept());
        Assert.False(book.Standing("F1", "one")!.Adventure.IsDraft);
    }

    [AvaloniaFact]
    public void TheOnlyDraftIsRejectedAndRemoved()
    {
        var (_, book, desk) = Open(Draft("one"));

        Assert.Null(desk.Reject());
        Assert.Null(book.Standing("F1", "one"));
    }

    [AvaloniaFact]
    public void TwoDraftsWithNeitherOpenAreLeftAloneAndTheCommanderIsTold()
    {
        var (_, book, desk) = Open(Draft("one"), Draft("two"));

        foreach (var act in new[] { desk.Accept, desk.Reject, desk.Change })
        {
            Assert.Equal("There is more than one draft adventure. Open the one you mean.", act());
        }

        Assert.True(book.Standing("F1", "one")!.Adventure.IsDraft);
        Assert.True(book.Standing("F1", "two")!.Adventure.IsDraft);
    }

    [AvaloniaFact]
    public void OfTwoDraftsTheOneWhosePageIsOpenIsActedOn()
    {
        var (panel, book, desk) = Open(Draft("one"), Draft("two"));

        panel.Nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + "two", "two"));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(desk.Accept());
        Assert.True(book.Standing("F1", "one")!.Adventure.IsDraft);
        Assert.False(book.Standing("F1", "two")!.Adventure.IsDraft);
    }

    [AvaloniaFact]
    public void ChangingOpensTheVoiceEntryForARemark()
    {
        var (panel, _, desk) = Open(Draft("one"));

        Assert.Null(desk.Change());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("adventure.revise", panel.Nav.Trail[^1].Key);
    }
}
