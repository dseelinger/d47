using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Ships;
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

    internal sealed record Surface(Window Window, PanelView Panel, AdventureSurface? Stories = null);

    /// <summary>The ships a story's objective can show or plan.</summary>
    internal sealed record Ships(
        ShipPlanService Plans, ChecklistService Checklists, D47.Core.Engineers.EngineerPlanService Unlocks, D47.Core.Loadout.OnFootPlanService OnFoot);

    internal static Ships Fleet(string folder)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder(folder));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var builds = new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance);
        var kit = new D47.Core.Loadout.OnFootBuildStore(
            Path.Combine(paths.Data, "on-foot.json"), new MemoryFileSystem(), NullLogger<D47.Core.Loadout.OnFootBuildStore>.Instance);

        return new Ships(
            new ShipPlanService(builds, checklists, () => null),
            checklists,
            new D47.Core.Engineers.EngineerPlanService(builds, kit, checklists, () => null),
            new D47.Core.Loadout.OnFootPlanService(kit, checklists, () => null));
    }

    /// <summary>The Stories surface for a one-chapter story; a ship objective reads <paramref name="fleet"/> or else the plans.</summary>
    internal static AdventureSurface Build(
        TriggerKind kind, Ships? ships = null, string? shipType = null, IReadOnlyList<FleetEntry>? fleet = null, string? engineer = null,
        Func<AdventureAsk, DateTimeOffset, CancellationToken, Task<AdventureOutcome>>? write = null,
        Func<string?>? commander = null)
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
                    Trigger = new AdventureTrigger { Kind = kind, ShipType = shipType, Engineer = engineer, Stage = engineer is null ? null : "Unlocked", SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
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
            stories, book, () => StoryFixture.Catalog, write ?? generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        return new AdventureSurface(
            book, generator, () => null, commander ?? (() => "F1"), () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director,
            Fleet: fleet is not null ? () => fleet : ships is null ? null : ships.Plans.Fleet,
            Intend: ships is null ? null : hull => ships.Plans.Intend(hull));
    }

    internal static Surface Open(
        TriggerKind kind, Ships? ships = null, string? shipType = null, IReadOnlyList<FleetEntry>? fleet = null, string? engineer = null, bool engineers = false,
        Func<AdventureAsk, DateTimeOffset, CancellationToken, Task<AdventureOutcome>>? write = null,
        Func<string?>? commander = null, D47.Core.Capabilities.Builtin.AdventureCapability.AdventureDesk? desk = null)
    {
        var surface = Build(kind, ships, shipType, fleet, engineer, write, commander);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface, desk: desk);

        if (ships is not null)
        {
            panel.EnableLoadout(ships.Plans, ships.Checklists, () => null, null);

            if (engineers)
            {
                panel.EnableEngineers(ships.Unlocks, ships.Plans, () => null, ships.OnFoot);
            }
        }

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, surface);
    }

    internal static void ReadChapter(Surface surface)
    {
        surface.Panel.Nav.SelectRoot(AdventuresPage.RootKey);
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

    /// <summary>The first text on the page starting with <paramref name="text"/>; the next-objective strip above every tab is not searched.</summary>
    internal static TextBlock? Starting(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block =>
            !block.GetVisualAncestors().OfType<ObjectiveStrip>().Any() &&
            (block.Text ?? string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? [])).StartsWith(text, StringComparison.OrdinalIgnoreCase));
}
