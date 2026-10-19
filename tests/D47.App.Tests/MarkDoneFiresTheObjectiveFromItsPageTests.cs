using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>Mark done under NEXT asks first, then fires the objective as the Commander's mark, on a chapter's page and a short story's.</summary>
[Trait("Category", "Integration")]
public sealed class MarkDoneFiresTheObjectiveFromItsPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    private static Adventure Short(string key, DateTimeOffset? accepted) => new()
    {
        Key = key,
        Name = key,
        Source = AdventureSource.Generated,
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Wreck",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
                Line = "Go and look.",
            },
        ],
        AcceptedAt = accepted,
    };

    private static Surface ReadShort(params Adventure[] shorts)
    {
        var surface = Open(TriggerKind.Arrive);

        foreach (var adventure in shorts)
        {
            Assert.Null(surface.Stories!.Book.Write("F1", adventure));
        }

        surface.Panel.Nav.SelectRoot(AdventuresPage.RootKey);
        surface.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + shorts[0].Key, shorts[0].Key));
        Dispatcher.UIThread.RunJobs();
        return surface;
    }

    private static void Press(Surface surface, string label)
    {
        var button = Assert.IsAssignableFrom<Avalonia.Controls.Button>(Pressable(surface.Panel, label));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Choose(Surface surface, string label)
    {
        var option = surface.Panel.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(button =>
            button.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Any(text => text.Text == label));
        option.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AChaptersPageAsksThenFiresTheObjectiveAsTheCommanders()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        ReadChapter(surface);
        Save(surface.Window, "mark-done-chapter");

        Press(surface, "Mark done");

        Assert.NotNull(Starting(surface.Panel, "Mark \"The Signal\" done?"));
        Assert.NotNull(Starting(surface.Panel, "The story moves on as if the journal had shown it."));
        Save(surface.Window, "mark-done-asks");

        Choose(surface, "Mark it done");

        var standing = surface.Stories!.Book.Standing("F1", ChapterKey)!;
        Assert.True(standing.IsDone);
        Assert.Equal([AdventureFold.CommanderSource], standing.FiredBy);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void NotYetLeavesTheObjectiveWaiting()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        ReadChapter(surface);

        Press(surface, "Mark done");
        Choose(surface, "Not yet");

        Assert.Equal(0, surface.Stories!.Book.Standing("F1", ChapterKey)!.Current);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AShortStorysPageMarksOnlyThatStory()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = ReadShort(Short("short-b", Now.AddHours(-1)), Short("short-a", Now.AddHours(-1)));

        Press(surface, "Mark done");
        Assert.NotNull(Starting(surface.Panel, "Mark \"The Wreck\" done?"));
        Choose(surface, "Mark it done");

        Assert.True(surface.Stories!.Book.Standing("F1", "short-b")!.IsDone);
        Assert.Equal(0, surface.Stories.Book.Standing("F1", "short-a")!.Current);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AShortStoryNotBegunHasNoMarkDone()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = ReadShort(Short("not-begun", null));

        Assert.Null(Pressable(surface.Panel, "Mark done"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelFitsMarkDoneBesideTheShipButtonWithPressableTargets()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var ships = Fleet("d47-mark-done-headset");
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        using var vr = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: ships.Checklists,
            ships: ships.Plans,
            gameState: () => null,
            adventures: Build(TriggerKind.Board, ships, "cobramkiii"));

        vr.Nav.Select(PanelTab.Stories);
        vr.Nav.SelectRoot(AdventuresPage.RootKey);
        vr.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + ChapterKey, "The First Light"));
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();

        var row = new[] { "Plan a Cobra MkIII", "Mark done", "Change objective", "Never ask this" }
            .Select(label => vr.Board.View.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(button => button.Content as string == label))
            .ToList();

        Assert.All(row, button => Assert.True(button.Bounds.Height >= 44, $"{button.Content} is {button.Bounds.Height} high"));
        Assert.All(row, button =>
        {
            var right = button.TranslatePoint(new Avalonia.Point(button.Bounds.Width, 0), vr.Board.View)!.Value.X;
            Assert.True(right <= vr.Board.View.Bounds.Width, $"{button.Content} ends at {right} of {vr.Board.View.Bounds.Width}");
        });

        vr.Board.Render().SaveCapture("mark-done-headset");
    }
}
