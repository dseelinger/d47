using D47.Core.Storage;
using System.Reflection;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Storage;

/// <summary>
/// The tick reads <see cref="RoutePlanBook"/> and <see cref="MailLedger"/> through their state lock, so a file
/// write on the tool path must not hold it (#910). Each test holds the write lock and reads state meanwhile.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlansAndMailAreWrittenOutsideTheirLocksTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static readonly DateTimeOffset At = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-unlocked-writes-" + Guid.NewGuid().ToString("N"));

    public PlansAndMailAreWrittenOutsideTheirLocksTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Lock WriteGate(object store) =>
        (Lock)store.GetType().GetField("_writeGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;

    /// <summary>Polls <paramref name="seen"/> on another thread until true; false if a call blocks or it never turns true.</summary>
    private static bool Eventually(Func<bool> seen)
    {
        var poll = Task.Run(() =>
        {
            while (!seen())
            {
                Thread.Sleep(5);
            }
        });

        return poll.Wait(Patience, TestContext.Current.CancellationToken);
    }

    private static PlottedRoute Jump() => new("Sol", "Colonia", 22_000, 168, [new RouteWaypoint("Colonia", 158, 0, false)]);

    private static TradeRoute Trade() => new([new TradeStop("Sol", "Abraham Lincoln")]);

    [Fact]
    public async Task APlanIsReadableWhileItsFileIsBeingWritten()
    {
        var path = Path.Combine(_folder, "route-plans.json");
        var book = new RoutePlanBook(path, new DiskFileSystem(), NullLogger<RoutePlanBook>.Instance);
        var gate = WriteGate(book);
        Task keeping;

        gate.Enter();

        try
        {
            keeping = Task.Run(() => book.Record(Jump(), "Sol to Colonia", At), TestContext.Current.CancellationToken);

            Assert.True(Eventually(() => book.Last(RoutePlanKind.Jump) is not null));
            Assert.False(File.Exists(path));
        }
        finally
        {
            gate.Exit();
        }

        await keeping.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task AnOlderPlanWrittenLastDoesNotReplaceANewerOne()
    {
        var path = Path.Combine(_folder, "route-plans.json");
        var book = new RoutePlanBook(path, new DiskFileSystem(), NullLogger<RoutePlanBook>.Instance);
        var gate = WriteGate(book);
        Task first;
        Task second;

        gate.Enter();

        try
        {
            first = Task.Run(() => book.Record(Jump(), "Sol to Colonia", At), TestContext.Current.CancellationToken);
            Assert.True(Eventually(() => book.Last(RoutePlanKind.Jump) is not null));

            second = Task.Run(() => book.Record(Trade(), "A loop", At), TestContext.Current.CancellationToken);
            Assert.True(Eventually(() => book.Last(RoutePlanKind.Trade) is not null));
        }
        finally
        {
            gate.Exit();
        }

        await Task.WhenAll(first, second).WaitAsync(Patience, TestContext.Current.CancellationToken);

        var reloaded = new RoutePlanBook(path, new DiskFileSystem(), NullLogger<RoutePlanBook>.Instance);
        reloaded.Load();

        Assert.NotNull(reloaded.Last(RoutePlanKind.Jump));
        Assert.NotNull(reloaded.Last(RoutePlanKind.Trade));
    }

    [Fact]
    public async Task MailIsReadableWhileItsWatermarkIsBeingWritten()
    {
        var path = Path.Combine(_folder, MailLedger.FileName);
        var ledger = new MailLedger(path, new DiskFileSystem(), NullLogger.Instance);
        var line = """{"timestamp":"2026-10-07T09:00:00Z","event":"MissionCompleted","Name":"Mission_Delivery","MissionID":1,"Reward":1000}""";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        ledger.Fold([parsed!], live: true, "F1");

        var gate = WriteGate(ledger);
        Task reading;

        gate.Enter();

        try
        {
            reading = Task.Run(() => ledger.Read("F1"), TestContext.Current.CancellationToken);

            Assert.True(Eventually(() => ledger.ReadThrough("F1") is not null));
            Assert.False(File.Exists(path));
        }
        finally
        {
            gate.Exit();
        }

        await reading.WaitAsync(Patience, TestContext.Current.CancellationToken);

        var stored = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(path))!;
        Assert.Equal(At, stored["F1"]);
    }
}
