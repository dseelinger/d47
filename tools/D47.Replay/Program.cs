using System.Diagnostics;
using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;

namespace D47.Replay;

/// <summary>
/// Folds every journal in each folder through <see cref="GameStateStore"/>, the way the app's journal spine does,
/// and writes a Markdown report of the exceptions, logged warnings and unknown event kinds.
/// Usage: d47-replay --out report.md [--previous last-report.md] folder...
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? output = null;
        string? previous = null;
        var folders = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--previous" when i + 1 < args.Length:
                    previous = args[++i];
                    break;
                default:
                    folders.Add(args[i]);
                    break;
            }
        }

        if (output is null || folders.Count == 0)
        {
            Console.Error.WriteLine("Usage: d47-replay --out report.md [--previous last-report.md] folder...");
            return 2;
        }

        var earlier = previous is not null && File.Exists(previous) ? File.ReadAllText(previous) : null;
        var files = new ReadOnlyFileSystem(new DiskFileSystem());
        var results = folders.Select(folder => FolderReplay.Run(folder, files)).ToList();

        File.WriteAllText(output, Report.Write(results, earlier));

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Replayed {results.Sum(r => r.Events)} events from {results.Sum(r => r.Files)} files: {results.Sum(r => r.Exceptions.Count)} exceptions, {results.Sum(r => r.Problems.Count)} warnings."));

        return 0;
    }
}

/// <summary>One exception thrown while folding one event.</summary>
internal sealed record FoldFailure(string File, DateTimeOffset Timestamp, string Kind, Exception Exception);

/// <summary>What replaying one folder found.</summary>
internal sealed record FolderResult(
    int Files,
    long Events,
    TimeSpan Took,
    List<FoldFailure> Exceptions,
    List<LoggedProblem> Problems,
    Dictionary<string, int> UnknownKinds);

internal static class FolderReplay
{
    /// <summary>Replays the folder's journals oldest first into one fresh store, carrying a held LoadGame across files as the spine does.</summary>
    public static FolderResult Run(string folder, IFileSystem files)
    {
        var clock = Stopwatch.StartNew();
        var loggers = new CollectingLoggerFactory();
        var logger = loggers.CreateLogger("Replay");
        var store = new GameStateStore();
        var failures = new List<FoldFailure>();
        var unknown = new Dictionary<string, int>(StringComparer.Ordinal);
        var ordered = JournalFolder.Ordered(files, folder);
        long events = 0;
        JournalEvent? carried = null;

        foreach (var file in ordered)
        {
            var name = Path.GetFileName(file);
            var marker = new OperationMarker();
            List<JournalEvent> batch;

            try
            {
                batch = [.. marker.Next(new JournalReader(file, files, logger).Poll())];
            }
            catch (Exception ex)
            {
                failures.Add(new FoldFailure(name, DateTimeOffset.MinValue, "(reading the file)", ex));
                continue;
            }

            if (carried is not null)
            {
                batch.Insert(0, carried);
            }

            carried = marker.Release();

            foreach (var journalEvent in batch)
            {
                events++;

                if (!HandledEvents.All.Contains(journalEvent.Kind))
                {
                    unknown[journalEvent.Kind] = unknown.GetValueOrDefault(journalEvent.Kind) + 1;
                }

                try
                {
                    store.Apply(journalEvent, null, priming: false);
                }
                catch (Exception ex)
                {
                    failures.Add(new FoldFailure(name, journalEvent.Timestamp, journalEvent.Kind, ex));
                }
            }
        }

        return new FolderResult(ordered.Count, events, clock.Elapsed, failures, loggers.Problems, unknown);
    }
}

internal static class Report
{
    private const string NewMark = " **(new)**";

    /// <summary>The report; a heading line absent from <paramref name="earlier"/> is marked new.</summary>
    public static string Write(IReadOnlyList<FolderResult> results, string? earlier)
    {
        var text = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        text.AppendLine(inv, $"# Journal replay, {DateTime.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        text.AppendLine("Folders are numbered in the order they were given. Reports name no Commander and quote no journal line.");
        text.AppendLine();
        text.AppendLine("| Folder | Files | Events | Exceptions | Warnings | Unknown kinds | Took |");
        text.AppendLine("|---|---|---|---|---|---|---|");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            text.AppendLine(inv, $"| {i + 1} | {r.Files} | {r.Events} | {r.Exceptions.Count} | {r.Problems.Count} | {r.UnknownKinds.Count} | {r.Took.TotalSeconds:0.0}s |");
        }

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            text.AppendLine();
            text.AppendLine(inv, $"## Folder {i + 1}");

            text.AppendLine();
            text.AppendLine("### Exceptions");
            text.AppendLine();
            var groups = r.Exceptions.GroupBy(f => Key(f.Exception)).OrderByDescending(g => g.Count()).ToList();
            if (groups.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach (var group in groups)
            {
                var first = group.First();
                var line = $"- {group.Key}";
                text.AppendLine(inv, $"{line}{Mark(line, earlier)}");
                text.AppendLine(inv, $"  {group.Count()} times; first in {first.File} at {first.Timestamp:yyyy-MM-dd HH:mm:ss} ({first.Kind}). Kinds: {string.Join(", ", group.Select(f => f.Kind).Distinct().Take(8))}.");
            }

            text.AppendLine();
            text.AppendLine("### Logged warnings and errors");
            text.AppendLine();
            var problems = r.Problems.GroupBy(p => $"{p.Level} {p.Category}: {p.Template}{(p.Exception is null ? "" : $" ({p.Exception})")}")
                .OrderByDescending(g => g.Count()).ToList();
            if (problems.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach (var group in problems)
            {
                var line = $"- {group.Key}";
                text.AppendLine(inv, $"{line}{Mark(line, earlier)} ({group.Count()} times)");
            }

            text.AppendLine();
            text.AppendLine("### Event kinds d47 does not list in HandledEvents");
            text.AppendLine();
            if (r.UnknownKinds.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach (var (kind, count) in r.UnknownKinds.OrderByDescending(pair => pair.Value))
            {
                var line = $"- `{kind}`";
                text.AppendLine(inv, $"{line}{Mark(line, earlier)} ({count} times)");
            }
        }

        return text.ToString();
    }

    private static string Mark(string line, string? earlier) =>
        earlier is not null && !earlier.Contains(line, StringComparison.Ordinal) ? NewMark : "";

    /// <summary>Type, message and the first d47 frame: the same defect gives the same key on every night.</summary>
    private static string Key(Exception exception)
    {
        var message = exception.Message.ReplaceLineEndings(" ");
        if (message.Length > 200)
        {
            message = message[..200] + "…";
        }

        var frame = exception.StackTrace?
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("at D47.", StringComparison.Ordinal));

        if (frame is not null)
        {
            var at = frame.IndexOf(" in ", StringComparison.Ordinal);
            frame = at < 0 ? frame : frame[..at];
        }

        return $"`{exception.GetType().Name}`: {message} ({frame ?? "no d47 frame"})";
    }
}
