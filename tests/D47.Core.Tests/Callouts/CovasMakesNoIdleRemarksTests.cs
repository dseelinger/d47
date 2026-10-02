using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>With a stock core aboard, the ambient remark, the opening line and the session lines are not said.</summary>
public class CovasMakesNoIdleRemarksTests
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(5);

    private const StatusFlags Docked = StatusFlags.Docked | StatusFlags.InMainShip;

    private static CalloutContext At(DateTimeOffset now, params JournalEvent[] events) =>
        new(now, false, null, GameStatus.Unknown with { Flags = Docked }, NavRoute.None, events);

    private static JournalEvent Event(string kind, string? fid = null)
    {
        var json = fid is null
            ? $$"""{"timestamp":"3312-05-01T12:00:00Z","event":"{{kind}}"}"""
            : $$"""{"timestamp":"3312-05-01T12:00:00Z","event":"{{kind}}","FID":"{{fid}}"}""";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Announcement[] Remarks(bool stock)
    {
        var ambient = new AmbientCallout { Interval = Gap, Longest = Gap, StockCoreAboard = () => stock };
        _ = ambient.Examine(At(T0)).ToArray();

        return [.. ambient.Examine(At(T0 + Gap * 3))];
    }

    [Fact]
    public void TheAmbientRemarkIsNotSaidWithCovasAboard()
    {
        Assert.Empty(Remarks(stock: true));
        Assert.NotEmpty(Remarks(stock: false));
    }

    [Fact]
    public void TheOpeningLineIsNotSaidWithCovasAboard()
    {
        var continuity = new ContinuityCallout { StockCoreAboard = () => true };
        _ = continuity.Examine(At(T0)).ToArray();

        Assert.Empty(continuity.Examine(At(T0 + TimeSpan.FromMinutes(1))));
    }

    [Fact]
    public void ACoreSwitchedInLaterDoesNotGreetMidSession()
    {
        var stock = true;
        var continuity = new ContinuityCallout { StockCoreAboard = () => stock };
        _ = continuity.Examine(At(T0)).ToArray();
        _ = continuity.Examine(At(T0 + TimeSpan.FromMinutes(1))).ToArray();

        stock = false;

        Assert.Empty(continuity.Examine(At(T0 + TimeSpan.FromMinutes(20))));
    }

    [Fact]
    public void TheOpeningLineIsSaidWithAnyOtherCoreAboard()
    {
        var continuity = new ContinuityCallout { StockCoreAboard = () => false };
        _ = continuity.Examine(At(T0)).ToArray();

        Assert.Single(continuity.Examine(At(T0 + TimeSpan.FromMinutes(1))));
    }

    [Fact]
    public void NeitherSessionLineIsSaidWithCovasAboard()
    {
        var session = new SessionCallout { StockCoreAboard = () => true };

        Assert.Empty(session.Examine(At(T0, Event("LoadGame", "F1"))));
        Assert.Empty(session.Examine(At(T0 + TimeSpan.FromHours(2), Event("Shutdown"))));
    }

    [Fact]
    public void BothSessionLinesAreSaidWithAnyOtherCoreAboard()
    {
        var session = new SessionCallout { StockCoreAboard = () => false };

        Assert.Single(session.Examine(At(T0, Event("LoadGame", "F1"))));
        Assert.Single(session.Examine(At(T0 + TimeSpan.FromHours(2), Event("Shutdown"))));
    }
}
