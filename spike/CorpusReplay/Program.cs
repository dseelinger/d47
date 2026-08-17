using System.Diagnostics;
using D47.Core.Callouts;
using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace CorpusReplay;

/// <summary>
/// Drives every journal in a corpus through the same fold the tick loop uses — JournalReader.Poll()
/// into GameStateStore.Apply — and then through the full callout set, with each step wrapped so a
/// throw is recorded and replay continues. Core owns no thread and reads no clock, which is the
/// whole reason this is possible at speed.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var directory = args.Length > 0 ? args[0] : JournalFolder.DefaultPath();

        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"No such directory: {directory}");
            return 2;
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"{files.Count} journals in {directory}");

        var capture = new CaptureLoggerFactory();
        var logger = capture.CreateLogger("replay");
        var store = new GameStateStore();

        var scratch = Path.Combine(Path.GetTempPath(), "d47-corpus-replay");
        Directory.CreateDirectory(scratch);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(scratch, "checklist.json"), capture.CreateLogger<ChecklistStore>()),
            new ChecklistProposalStore(Path.Combine(scratch, "proposals.json"), capture.CreateLogger<ChecklistProposalStore>()),
            () => store.Active);

        var engine = new CalloutEngine(capture.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())
            .Add(new AnnouncedAttackCallout())
            .Add(new FuelCallout())
            .Add(new RouteCallout())
            .Add(new LongJumpCallout())
            .Add(new ArrivalCallout())
            .Add(new MaterialMilestoneCallout { Capacity = MaterialGrades.CapacityOf })
            .Add(new CarrierCallout())
            .Add(new SamplingCallout())
            .Add(new ProspectorCallout())
            .Add(new CoreAsteroidCallout())
            .Add(new ChecklistCallout(checklists))
            .Add(new RivalTerritoryCallout())
            .Add(new AmbientCallout())
            .Add(new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true });

        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        var throwsByKind = new Dictionary<string, Throw>(StringComparer.Ordinal);
        var totalEvents = 0L;
        var spoken = 0L;
        var watch = Stopwatch.StartNew();

        foreach (var file in files)
        {
            var reader = new JournalReader(file, logger);

            while (true)
            {
                var events = reader.Poll();
                if (events.Count == 0)
                {
                    break;
                }

                foreach (var journalEvent in events)
                {
                    totalEvents++;
                    kinds[journalEvent.Kind] = kinds.GetValueOrDefault(journalEvent.Kind) + 1;

                    try
                    {
                        store.Apply(journalEvent);
                    }
                    catch (Exception ex)
                    {
                        Record(throwsByKind, journalEvent, file, ex, "Apply");
                    }

                    // One event per tick, so every callout sees every event as "new this tick" —
                    // which is how a live 10 Hz loop sees a Commander who is actually playing.
                    var context = new CalloutContext(
                        journalEvent.Timestamp,
                        IsPriming: false,
                        store.Active,
                        GameStatus.Unknown,
                        NavRoute.None,
                        [journalEvent]);

                    try
                    {
                        engine.Tick(context);
                        spoken += engine.Drain().Count;
                    }
                    catch (Exception ex)
                    {
                        Record(throwsByKind, journalEvent, file, ex, "Tick");
                    }
                }
            }
        }

        watch.Stop();

        Console.WriteLine($"{totalEvents:N0} events, {kinds.Count} distinct kinds, in {watch.Elapsed.TotalSeconds:N1}s");
        Console.WriteLine($"{store.All.Count} commanders, {spoken:N0} announcements");
        Console.WriteLine();

        Console.WriteLine($"=== THROWS (uncaught): {throwsByKind.Count} distinct ===");
        foreach (var t in throwsByKind.Values.OrderBy(t => t.Kind, StringComparer.Ordinal))
        {
            Console.WriteLine($"  [{t.Stage}] {t.Kind}: {t.Exception}: {t.Message}");
            Console.WriteLine($"      at {t.Frame}");
            Console.WriteLine($"      file {t.File}");
        }

        Console.WriteLine();
        Console.WriteLine($"=== LOG WARN/ERROR: {capture.Records.Count} ===");
        foreach (var group in capture.Records.GroupBy(r => r.Template).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  [{group.Count()}] {group.Key}");
            foreach (var sample in group.Take(4))
            {
                Console.WriteLine($"      {Truncate(sample.Message, 400)}");
            }
        }

        Console.WriteLine();

        var outPath = Path.Combine(AppContext.BaseDirectory, "kinds.tsv");
        File.WriteAllLines(outPath, kinds.OrderByDescending(k => k.Value).Select(k => $"{k.Key}\t{k.Value}"));
        Console.WriteLine($"event kind counts -> {outPath}");

        Resolve.Run(files);

        Hulls.Run();

        return throwsByKind.Count == 0 && capture.Records.Count == 0 ? 0 : 1;
    }

    private static void Record(
        Dictionary<string, Throw> into, JournalEvent journalEvent, string file, Exception ex, string stage)
    {
        var key = stage + ":" + journalEvent.Kind;
        if (into.ContainsKey(key))
        {
            return;
        }

        into[key] = new Throw(
            journalEvent.Kind,
            stage,
            Path.GetFileName(file),
            ex.GetType().Name,
            ex.Message,
            (ex.StackTrace ?? string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? "");
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private sealed record Throw(
        string Kind, string Stage, string File, string Exception, string Message, string Frame);
}

internal sealed record LogRecord(string Template, string Message);

internal sealed class CaptureLoggerFactory : ILoggerFactory
{
    public List<LogRecord> Records { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(Records);

    public ILogger<T> CreateLogger<T>() => new CaptureLogger<T>(Records);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

internal class CaptureLogger(List<LogRecord> records) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var template = state is IReadOnlyList<KeyValuePair<string, object?>> pairs
            ? pairs.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value?.ToString() ?? "?"
            : "?";

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += $" || {exception.GetType().Name}: {exception.Message} @ "
                + ((exception.StackTrace ?? string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? "");
        }

        records.Add(new LogRecord(template, message));
    }
}

internal sealed class CaptureLogger<T>(List<LogRecord> records) : CaptureLogger(records), ILogger<T>;
