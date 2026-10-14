using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A stock story with one chapter waiting on one objective, drawn in a panel.</summary>
[Trait("Category", "Integration")]
internal static class NotForMeGroupFixture
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    internal const string ChapterKey = "the-first-light";

    internal sealed record Surface(Window Window, PanelView Panel);

    internal static Surface Open(TriggerKind kind)
    {
        var paths = new AppPaths(TempFolders.Create("d47-not-for-me-group"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var card = StoryFixture.Story;

        Assert.Null(book.Write("F1", new Adventure
        {
            Key = ChapterKey,
            Name = "The First Light",
            Source = AdventureSource.Generated,
            StoryId = card.Id,
            Beats =
            [
                new AdventureBeat
                {
                    Title = "The Signal",
                    Trigger = new AdventureTrigger { Kind = kind, SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
                    Line = "Go and look.",
                },
            ],
            AcceptedAt = Now.AddHours(-1),
        }));

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        stories.Save("F1", new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(),
            Chapters = [ChapterKey],
            PickedAt = Now.AddHours(-1),
        });

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(StoriesView.Crumb);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }

    internal static void ReadChapter(Surface surface)
    {
        surface.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + ChapterKey, "The First Light"));
        Dispatcher.UIThread.RunJobs();
    }

    internal static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    internal static double Top(Control control, Visual root) => control.TranslatePoint(default, root)!.Value.Y;

    internal static double Bottom(Control control, Visual root) => Top(control, root) + control.Bounds.Height;

    internal static Button? Pressable(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Content as string == label);

    internal static TextBlock? Starting(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block =>
            (block.Text ?? string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? [])).StartsWith(text, StringComparison.OrdinalIgnoreCase));
}
