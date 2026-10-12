using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class EachCommanderKeepsTheirOwnRoutePlansTests
{
    private const string Path = "route-plans.json";

    private static readonly DateTimeOffset At = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static RoutePlanBook Book(MemoryFileSystem files, string? shown = null) =>
        new(Path, files, NullLogger<RoutePlanBook>.Instance, () => shown);

    private static PlottedRoute Jump(string to) =>
        new("Sol", to, 100, 5, [new RouteWaypoint("Wolf 359", 8, 50, true), new RouteWaypoint(to, 8, 0, false)]);

    private static JournalEvent Arrival(string system) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{At.AddMinutes(5):O}}","event":"FSDJump","StarSystem":"{{system}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

    [Fact]
    public void TwoCommandersKeepSeparateJumpPlans()
    {
        var book = Book(new MemoryFileSystem());

        book.Record(Jump("Colonia"), "Sol to Colonia", At, "F1");
        book.Record(Jump("Beagle Point"), "Sol to Beagle Point", At, "F2");

        Assert.Equal("Sol to Colonia", book.Last(RoutePlanKind.Jump, "F1")?.Headline);
        Assert.Equal("Sol to Beagle Point", book.Last(RoutePlanKind.Jump, "F2")?.Headline);
    }

    [Fact]
    public void ThePlanShownIsTheShownCommandersPlan()
    {
        var files = new MemoryFileSystem();
        var shown = "F1";
        var book = new RoutePlanBook(Path, files, NullLogger<RoutePlanBook>.Instance, () => shown);

        book.Record(Jump("Colonia"), "Sol to Colonia", At);
        shown = "F2";

        Assert.Null(book.Last(RoutePlanKind.Jump));
        Assert.Equal("Sol to Colonia", book.Last(RoutePlanKind.Jump, "F1")?.Headline);
    }

    [Fact]
    public void AnEventFromOneCommanderDoesNotMoveAnothersPlan()
    {
        var book = Book(new MemoryFileSystem());

        book.Record(Jump("Colonia"), "Sol to Colonia", At, "F1");
        book.Record(Jump("Colonia"), "Sol to Colonia", At, "F2");

        book.Apply([Arrival("Wolf 359")], "F1");

        Assert.Equal(0, book.Last(RoutePlanKind.Jump, "F1")?.Reached);
        Assert.Null(book.Last(RoutePlanKind.Jump, "F2")?.Reached);
    }

    [Fact]
    public void APlanFileWithoutFrontierIdsBelongsToTheFirstCommanderShown()
    {
        var files = new MemoryFileSystem();
        var old = Book(files);

        old.Record(Jump("Colonia"), "Sol to Colonia", At);

        var reloaded = Book(files, "F1");

        reloaded.Load();

        Assert.Equal("Sol to Colonia", reloaded.Last(RoutePlanKind.Jump, "F1")?.Headline);
        Assert.Null(reloaded.Last(RoutePlanKind.Jump, "F2"));

        var again = Book(files, "F2");

        again.Load();

        Assert.Equal("Sol to Colonia", again.Last(RoutePlanKind.Jump, "F1")?.Headline);
        Assert.Null(again.Last(RoutePlanKind.Jump, "F2"));
    }
}
