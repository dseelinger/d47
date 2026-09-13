using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The last plan each planner produced.
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

    private static PlottedRoute JumpWithTwoWaypoints() => new(
        "Sol",
        "Colonia",
        22_000,
        168,
        [
            new RouteWaypoint("PSR J1752-2806", 10, 15_000, true),
            new RouteWaypoint("Colonia", 158, 0, false),
        ]);

    private static TradeRoute TradeLoop() => new(
    [
        new TradeStop("Sol", "Abraham Lincoln"),
        new TradeStop("Wolf 359", "Whitehead Vision"),
        new TradeStop("Sol", "Abraham Lincoln"),
    ])
    {
        Capital = 50_000_000,
        TotalProfit = 412_800,
        Loop = true,
    };

    private static JournalEvent Arrival(string kind, string system, DateTimeOffset at) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{at:O}}","event":"{{kind}}","StarSystem":"{{system}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

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

        // The service returns every waypoint and the capability speaks five of them, so the truncation is a
        // property of speech rather than of the answer.
        Assert.Equal(168, kept.Jump!.TotalJumps);
        Assert.Equal("PSR J1752-2806", kept.Jump.Waypoints[0].System);
        Assert.True(kept.Jump.Waypoints[0].IsNeutron);
    }

    /// <summary>The whole reason the book exists: whoever plotted, both paths read the same plan back.</summary>
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
    /// Nothing here is the Commander's work, so an unreadable file costs one plot rather than being a
    /// problem they have to go and fix — which is what separates this from the stores that hold
    /// preferences.
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

        // Capital is never inferred, and a stored plan has to say what it was worked out against or the
        // profit on it means nothing.
        Assert.Equal(50_000_000, read.Last(RoutePlanKind.Trade)?.Trade?.Capital);
        Assert.Equal(412_800, read.Last(RoutePlanKind.Trade)?.Trade?.TotalProfit);
    }

    [Fact]
    public void ANewPlanStartsWithNothingReached()
    {
        var book = Book();

        book.Record(Jump(), "Sol to Colonia", At);

        Assert.Null(book.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void ArrivingAtEachWaypointInOrderMovesTheReachedStopForward()
    {
        var book = Book();

        book.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);

        book.Apply([Arrival("FSDJump", "PSR J1752-2806", At.AddMinutes(10))]);
        Assert.Equal(0, book.Last(RoutePlanKind.Jump)?.Reached);

        book.Apply([Arrival("FSDJump", "Colonia", At.AddMinutes(20))]);
        Assert.Equal(1, book.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void JumpingPastAWaypointCountsItAsReachedToo()
    {
        var book = Book();

        book.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);

        // The Commander's own jump range cleared the first waypoint in one hop.
        book.Apply([Arrival("FSDJump", "Colonia", At.AddMinutes(10))]);

        Assert.Equal(1, book.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void AnArrivalBeforeThePlanWasMadeMovesNothing()
    {
        var book = Book();

        book.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);

        book.Apply([Arrival("FSDJump", "PSR J1752-2806", At.AddMinutes(-5))]);

        Assert.Null(book.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void AnArrivalNotOnThePlanMovesNothing()
    {
        var book = Book();

        book.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);

        book.Apply([Arrival("FSDJump", "Deciat", At.AddMinutes(10))]);

        Assert.Null(book.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void ATradeLoopEndingWhereItStartedReachesTheLastStopNotTheFirst()
    {
        var book = Book();

        book.Record(TradeLoop(), "2 stops from Abraham Lincoln", At);

        book.Apply([Arrival("Location", "Wolf 359", At.AddMinutes(10))]);
        Assert.Equal(1, book.Last(RoutePlanKind.Trade)?.Reached);

        book.Apply([Arrival("Location", "Sol", At.AddMinutes(20))]);
        Assert.Equal(2, book.Last(RoutePlanKind.Trade)?.Reached);
    }

    [Fact]
    public void ATradePlanMadeStandingOnTheFirstStopStartsThereReached()
    {
        var book = Book();

        book.Record(Trade(), "1 stop from Abraham Lincoln", At, currentSystem: "Sol");

        Assert.Equal(0, book.Last(RoutePlanKind.Trade)?.Reached);
    }

    [Fact]
    public void ChangedIsRaisedWhenAReachedStopMovesAndNotOtherwise()
    {
        var book = Book();
        book.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);

        var raised = 0;
        book.Changed += () => raised++;

        book.Apply([Arrival("FSDJump", "Deciat", At.AddMinutes(10))]);
        Assert.Equal(0, raised);

        book.Apply([Arrival("FSDJump", "PSR J1752-2806", At.AddMinutes(10))]);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void AReachedStopSurvivesLoad()
    {
        var written = Book();
        written.Record(JumpWithTwoWaypoints(), "Sol to Colonia", At);
        written.Apply([Arrival("FSDJump", "PSR J1752-2806", At.AddMinutes(10))]);

        var read = Book();
        read.Load();

        Assert.Equal(0, read.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public void AFileWithNoReachedFieldStillLoadsWithNothingReached()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(
            Path_,
            """
            [{"kind":"jump","plottedAt":"2026-08-20T09:00:00Z","headline":"Sol to Colonia",
              "jump":{"origin":"Sol","destination":"Colonia","totalDistance":22000,"totalJumps":168,
                "waypoints":[{"system":"PSR J1752-2806","jumps":10,"distanceLeft":21629,"isNeutron":true}]}}]
            """);

        var book = Book();
        book.Load();

        Assert.NotNull(book.Last(RoutePlanKind.Jump));
        Assert.Null(book.Last(RoutePlanKind.Jump)?.Reached);
    }
}
