using System.Net;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class AFactionReportJoinsEveryPageAndIsHeldForAnHourTests
{
    private const string Faction = FactionAnswer.Faction;

    private static readonly DateTimeOffset Start = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Start;

    private SpanshGalaxyService Service(FactionAnswer answer) =>
        new(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(answer), () => _now);

    [Fact]
    public async Task TwoPagesAreAskedForAndJoinedUntilTheCountIsReached()
    {
        var answer = new FactionAnswer(FactionAnswer.TwoPages());
        using var service = Service(answer);

        var report = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        Assert.Equal(["Eurybia", "Hyades Sector RE-G b11-3", "LPM 173"], report.Systems.Select(system => system.Name));
        Assert.Equal(22, report.Stations.Count);
        Assert.Equal(2, answer.Bodies.Count);
        Assert.Contains("\"page\":0", answer.Bodies[0]);
        Assert.Contains("\"page\":1", answer.Bodies[1]);
    }

    [Fact]
    public async Task AnEmptyPageEndsTheSearchEvenWhenTheCountSaysMore()
    {
        var pages = FactionAnswer.TwoPages();
        var answer = new FactionAnswer(pages[0]);
        using var service = Service(answer);

        var report = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Systems.Count);
        Assert.Equal(2, answer.Bodies.Count);
    }

    [Fact]
    public async Task AFactionPresentNowhereHasAnEmptyReport()
    {
        var answer = new FactionAnswer("""{"count":0,"results":[]}""");
        using var service = Service(answer);

        var report = await service.FactionAsync("No Such Faction", TestContext.Current.CancellationToken);

        Assert.Equal("No Such Faction", report.Name);
        Assert.Null(report.Government);
        Assert.Empty(report.Systems);
        Assert.Empty(report.Stations);
        Assert.Single(answer.Bodies);
    }

    [Fact]
    public async Task AFailedSecondPageRaisesGalaxyUnavailableAndIsNotHeld()
    {
        var answer = new FactionAnswer(FactionAnswer.TwoPages()) { Failing = (1, HttpStatusCode.ServiceUnavailable) };
        using var service = Service(answer);

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.FactionAsync(Faction, TestContext.Current.CancellationToken));

        answer.Failing = null;
        var report = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        Assert.Equal(3, report.Systems.Count);
        Assert.Equal(4, answer.Bodies.Count);
    }

    [Fact]
    public async Task ASecondCallWithinTheHourIsAnsweredWithoutARequest()
    {
        var answer = new FactionAnswer(FactionAnswer.Recorded());
        using var service = Service(answer);

        var first = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        _now = Start.AddMinutes(59);
        var second = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Single(answer.Bodies);
    }

    [Fact]
    public async Task ACallAfterTheHourAsksAgain()
    {
        var answer = new FactionAnswer(FactionAnswer.Recorded());
        using var service = Service(answer);

        await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        _now = Start.AddMinutes(61);
        await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        Assert.Equal(2, answer.Bodies.Count);
    }

    [Fact]
    public async Task EachFactionIsHeldUnderItsOwnName()
    {
        var answer = new FactionAnswer(FactionAnswer.Recorded());
        using var service = Service(answer);

        await service.FactionAsync(Faction, TestContext.Current.CancellationToken);
        await service.FactionAsync("eurybia blue mafia", TestContext.Current.CancellationToken);

        Assert.Equal(2, answer.Bodies.Count);
    }

    [Fact]
    public async Task ARefreshAsksAgainWithinTheHourAndTheReportSaysWhenItWasBuilt()
    {
        var answer = new FactionAnswer(FactionAnswer.Recorded());
        using var service = Service(answer);

        var first = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);

        _now = Start.AddMinutes(10);
        var held = await service.FactionAsync(Faction, TestContext.Current.CancellationToken);
        var fresh = await service.FactionAsync(Faction, TestContext.Current.CancellationToken, refresh: true);

        Assert.Equal(Start, first.FetchedAt);
        Assert.Equal(Start, held.FetchedAt);
        Assert.Equal(Start.AddMinutes(10), fresh.FetchedAt);
        Assert.Equal(2, answer.Bodies.Count);
    }

    [Fact]
    public async Task EachPageReadReportsHowManySystemsAreInSoFar()
    {
        var answer = new FactionAnswer(FactionAnswer.TwoPages());
        using var service = Service(answer);
        var heard = new List<FactionProgress>();

        await service.FactionAsync(Faction, TestContext.Current.CancellationToken, progress: new Immediate(heard));

        Assert.Equal(2, heard.Count);
        Assert.True(heard[0].Read < heard[1].Read);
        Assert.Equal(heard[1].Total, heard[1].Read);
    }

    private sealed class Immediate(List<FactionProgress> heard) : IProgress<FactionProgress>
    {
        public void Report(FactionProgress value) => heard.Add(value);
    }
}
