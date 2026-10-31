using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every system name the Navigation tab draws has a copy glyph beside it or at the end of its line.</summary>
[Trait("Category", "Integration")]
public class TheNavigationTabCopiesEverySystemNameTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 19, 0, 0, TimeSpan.Zero);

    private static List<string> Copied(Avalonia.Visual root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Select(CopyGlyph.GetCopies).OfType<string>()];

    private static void Press(Avalonia.Visual root, string copies, RecordingClipboard clipboard)
    {
        root.GetVisualDescendants().OfType<Button>()
            .First(button => CopyGlyph.GetCopies(button) == copies)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(copies, clipboard.Last);
    }

    private static T Shown<T>(T control)
        where T : Control
    {
        new Window { Content = control, Width = 900, Height = 700 }.Show();
        Dispatcher.UIThread.RunJobs();

        return control;
    }

    private static Func<string, Task<bool>> CopyTo(RecordingClipboard clipboard) =>
        text => clipboard.SetTextAsync(text);

    private static RoutePlanBook Book() =>
        new("plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

    private static NavRoute Route(params string[] systems) =>
        new() { Hops = [.. systems.Select((name, index) => new RouteHop(name, "G") { Position = (index * 10, 0, 0) })] };

    private static void RecordJump(RoutePlanBook book) =>
        book.Record(
            new PlottedRoute("Sol", "Colonia", 22_000, 168, [new RouteWaypoint("Waypoint 0", 1, 700, IsNeutron: false)]),
            "Sol to Colonia",
            Start);

    [AvaloniaFact]
    public void ThePlanHeadingCopiesItsOriginAndItsDestination()
    {
        var book = Book();
        RecordJump(book);

        var clipboard = new RecordingClipboard();
        var page = Shown(new RoutePlanResultPage(book.Last(RoutePlanKind.Jump)!, CopyTo(clipboard), book));

        Press(page, "Sol", clipboard);
        Press(page, "Colonia", clipboard);
    }

    [AvaloniaFact]
    public void ThePlanHeadingWithNoClipboardDrawsNoGlyph()
    {
        var book = Book();
        RecordJump(book);

        var page = Shown(new RoutePlanResultPage(book.Last(RoutePlanKind.Jump)!, plans: book));

        Assert.Empty(Copied(page));
    }

    [AvaloniaFact]
    public void TheNoStationLineEndsInAGlyphForItsReferenceSystem()
    {
        var book = Book();
        book.Record(
            new OutfittingRoute([], ["1H Guardian FSD Booster"]) { Reference = "LHS 3447", MaxDistance = 50 },
            "no stops",
            Start);

        var clipboard = new RecordingClipboard();
        var page = Shown(new RoutePlanResultPage(book.Last(RoutePlanKind.Outfitting)!, CopyTo(clipboard), book));

        Press(page, "LHS 3447", clipboard);
    }

    [AvaloniaFact]
    public void TheNoStationLineDrawsNoGlyphForTheStart()
    {
        var book = Book();
        book.Record(new OutfittingRoute([], ["1H Guardian FSD Booster"]), "no stops", Start);

        var page = Shown(new RoutePlanResultPage(book.Last(RoutePlanKind.Outfitting)!, CopyTo(new RecordingClipboard()), book));

        Assert.Empty(Copied(page));
    }

    [AvaloniaFact]
    public void TheProgressHeadlineCopiesItsTwoNamesAsStored()
    {
        var clipboard = new RecordingClipboard();
        var page = Shown(new RouteProgressPage(() => Route("Sol", "Alpha Centauri", "Colonia"), () => "Sol", CopyTo(clipboard)));

        Press(page, "Sol", clipboard);
        Press(page, "Colonia", clipboard);
    }

    [AvaloniaFact]
    public void TheProgressAsideEndsInAGlyphForTheSystemTheCommanderIsIn()
    {
        var clipboard = new RecordingClipboard();
        var page = Shown(new RouteProgressPage(() => Route("Sol", "Colonia"), () => "Sagittarius A*", CopyTo(clipboard)));

        Press(page, "Sagittarius A*", clipboard);
    }

    [AvaloniaFact]
    public void TheProgressAsideHasNoGlyphWhenNoSystemIsKnown()
    {
        var page = Shown(new RouteProgressPage(() => Route("Sol", "Colonia"), () => null, CopyTo(new RecordingClipboard())));

        Assert.DoesNotContain(Copied(page), name => name is not ("Sol" or "Colonia"));
    }

    [AvaloniaFact]
    public void TheMiniRouteStopsCopyTheirSystems()
    {
        var book = Book();
        RecordJump(book);

        var clipboard = new RecordingClipboard();
        var mini = Shown(new RouteMini(book, () => NavRoute.None, () => "Sol", copy: CopyTo(clipboard)));

        Press(mini, "Waypoint 0", clipboard);
    }

    [AvaloniaFact]
    public void TheMiniRouteWithNoClipboardDrawsNoGlyph()
    {
        var book = Book();
        RecordJump(book);

        Assert.Empty(Copied(Shown(new RouteMini(book, () => NavRoute.None, () => "Sol"))));
    }
}
