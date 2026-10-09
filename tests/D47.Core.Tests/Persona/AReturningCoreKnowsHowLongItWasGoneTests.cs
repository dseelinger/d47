using System.Text.Json;
using D47.Core.Storage;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

[Trait("Category", "Integration")]
public sealed class AReturningCoreKnowsHowLongItWasGoneTests : IDisposable
{
    private static readonly DateTimeOffset Day = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();
    private DateTimeOffset _now = Day.AddHours(10);

    public void Dispose() => _install.Dispose();

    private ViewStateStore Store() => new(_install.Paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);

    private CoreAbsences Open() => new(Store(), () => _now, NullLogger.Instance);

    private static JournalEvent Event(string kind, int hour) =>
        new(Day.AddHours(hour), kind, JsonDocument.Parse("{}").RootElement.Clone());

    private DateTimeOffset? Recorded(string core) =>
        Store().Load().CoresLastAboard.TryGetValue(core, out var at) ? at : null;

    [Fact]
    public void ACoreNeverAboardReturnsNoGap()
    {
        var (away, delta) = Open().Returning("covas", null);

        Assert.Null(away);
        Assert.Null(delta);
    }

    [Fact]
    public void ACoreSwitchedOutAtFourteenAndBackAtFifteenWasAwayOneHour()
    {
        var absences = Open();

        _now = Day.AddHours(14);
        absences.Leaving("covas", SessionSummary.Empty);

        _now = Day.AddHours(15);

        Assert.Equal(TimeSpan.FromHours(1), absences.Returning("covas", null).Away);
    }

    [Fact]
    public void AShutdownBeforeExitingIsWhenTheCoreLeft()
    {
        var absences = Open();

        absences.Observe([Event("Shutdown", 14)], priming: false, "covas");
        _now = Day.AddHours(22);
        absences.Exiting("covas");

        Assert.Equal(Day.AddHours(14), Recorded("covas"));
    }

    [Fact]
    public void ALoadGameAfterAShutdownLetsExitingRecordTheExit()
    {
        var absences = Open();

        absences.Observe([Event("Shutdown", 14), Event("LoadGame", 16)], priming: false, "covas");
        _now = Day.AddHours(22);
        absences.Exiting("covas");

        Assert.Equal(Day.AddHours(22), Recorded("covas"));
    }

    [Fact]
    public void AShutdownSeenWhilePrimingRecordsNothing()
    {
        var absences = Open();

        absences.Observe([Event("Shutdown", 14)], priming: true, "covas");

        Assert.Null(Recorded("covas"));
        Assert.Null(absences.Returning("covas", null).Away);
    }

    [Fact]
    public void AShutdownSeenWhilePrimingStopsExitingFromRecordingTheExit()
    {
        var absences = Open();

        absences.Observe([Event("Shutdown", 14)], priming: true, "covas");
        _now = Day.AddHours(22);
        absences.Exiting("covas");

        Assert.Null(Recorded("covas"));
    }

    [Fact]
    public void ACoreSwitchedOutAfterTheGameClosedLeftWhenTheGameClosed()
    {
        var absences = Open();

        absences.Observe([Event("Shutdown", 14)], priming: false, "covas");
        _now = Day.AddHours(16);
        absences.Leaving("covas", SessionSummary.Empty);
        _now = Day.AddHours(17);

        Assert.Equal(TimeSpan.FromHours(3), absences.Returning("covas", null).Away);
    }

    [Fact]
    public void AnEntryFromAnEarlierRunIsReadAndSwitchedWritesTheOutgoingCore()
    {
        var earlier = Day.AddHours(2);
        var store = Store();
        store.Save(store.Load() with
        {
            CoresLastAboard = new Dictionary<string, DateTimeOffset> { ["analyst"] = earlier },
        });

        var absences = Open();

        Assert.Equal(TimeSpan.FromHours(8), absences.Returning("analyst", null).Away);

        _now = Day.AddHours(12);
        absences.Leaving("covas", SessionSummary.Empty);
        absences.Switched("covas");

        Assert.Equal(Day.AddHours(12), Recorded("covas"));
        Assert.Equal(earlier, Recorded("analyst"));
    }
}
