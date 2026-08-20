using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The last plan each planner produced (list.md Phase 37, "One last plan, shared by the voice
/// and the panel").
/// </summary>
public class RoutePlanBookTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "d47-plans-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_folder, "route-plans.json");

    private RoutePlanBook Book() => new(Path_, NullLogger<RoutePlanBook>.Instance);

    private static readonly DateTimeOffset At = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static PlottedRoute Jump() => new(
        "Sol",
        "Colonia",
        22_000,
        168,
        [new RouteWaypoint("PSR J1752-2806", 10, 21_629, true)]);

    private static TradeRoute Trade() => new([new TradeStop("Sol", "Abraham Lincoln")])
    {
        Capital = 50_000_000,
        TotalProfit = 412_800,
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NothingPlottedYetIsNullRatherThanAnEmptyPlan()
    {
        var book = Book();

        book.Load();

        Assert.Null(book.Last(RoutePlanKind.Jump));
        Assert.Null(book.Last(RoutePlanKind.Riches));
        Assert.Null(book.Last(RoutePlanKind.Trade));
    }

    [Fact]
    public void APlanIsKeptWholeRatherThanAsTheSentenceItWasReadAsOut()
    {
        var book = Book();

        book.Record(Jump(), "Sol to Colonia", At);

        var kept = book.Last(RoutePlanKind.Jump);

        Assert.NotNull(kept);
        Assert.Equal("Sol to Colonia", kept.Headline);
        Assert.Equal(At, kept.PlottedAt);

        // The service returns every waypoint and the capability speaks five of them, so the
        // truncation is a property of speech rather than of the answer.
        Assert.Equal(168, kept.Jump!.TotalJumps);
        Assert.Equal("PSR J1752-2806", kept.Jump.Waypoints[0].System);
        Assert.True(kept.Jump.Waypoints[0].IsNeutron);
    }

    /// <summary>
    /// The whole reason the book exists: whoever plotted, both paths read the same plan back.
    /// A panel holding its own would be the settings-row-and-speaking-path disagreement in a
    /// new place.
    /// </summary>
    [Fact]
    public void OnePlanIsReadBackByWhoeverAsks()
    {
        var written = Book();
        written.Record(Jump(), "Sol to Colonia", At);

        var read = Book();
        read.Load();

        Assert.Equal("Sol to Colonia", read.Last(RoutePlanKind.Jump)?.Headline);
        Assert.Equal("Colonia", read.Last(RoutePlanKind.Jump)?.Jump?.Destination);
    }

    [Fact]
    public void EachKindHasItsOwnSlotAndDoesNotEvictTheOthers()
    {
        var book = Book();

        book.Record(Jump(), "Sol to Colonia", At);
        book.Record(Trade(), "1 stop from Abraham Lincoln", At);

        Assert.NotNull(book.Last(RoutePlanKind.Jump));
        Assert.NotNull(book.Last(RoutePlanKind.Trade));
        Assert.Null(book.Last(RoutePlanKind.Riches));
    }

    [Fact]
    public void PlottingAgainReplacesTheOneBefore()
    {
        var book = Book();

        book.Record(Jump(), "Sol to Colonia", At);
        book.Record(Jump(), "Sol to Sagittarius A*", At.AddHours(1));

        Assert.Equal("Sol to Sagittarius A*", book.Last(RoutePlanKind.Jump)?.Headline);
    }

    [Fact]
    public void RecordingRaisesChangedSoASurfaceCanRedraw()
    {
        var book = Book();
        var raised = 0;

        book.Changed += () => raised++;
        book.Record(Jump(), "Sol to Colonia", At);

        Assert.Equal(1, raised);
    }

    /// <summary>
    /// Nothing here is the Commander's work, so an unreadable file costs one plot rather than
    /// being a problem they have to go and fix — which is what separates this from the stores
    /// that hold preferences.
    /// </summary>
    [Fact]
    public void AFileThatWillNotParseIsDroppedRatherThanReported()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, "{ this is not a plan");

        var book = Book();
        book.Load();

        Assert.Null(book.Last(RoutePlanKind.Jump));

        // And the next plot rewrites it rather than failing on it.
        book.Record(Jump(), "Sol to Colonia", At);
        Assert.NotNull(book.Last(RoutePlanKind.Jump));
    }

    [Fact]
    public void TheTradePlanKeepsTheFigureTheCommanderSaidOutLoud()
    {
        var book = Book();

        book.Record(Trade(), "1 stop from Abraham Lincoln", At);

        var read = Book();
        read.Load();

        // Capital is never inferred, and a stored plan has to say what it was worked out against
        // or the profit on it means nothing.
        Assert.Equal(50_000_000, read.Last(RoutePlanKind.Trade)?.Trade?.Capital);
        Assert.Equal(412_800, read.Last(RoutePlanKind.Trade)?.Trade?.TotalProfit);
    }
}
