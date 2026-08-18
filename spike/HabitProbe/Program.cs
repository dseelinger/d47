using System.Diagnostics;
using D47.Core.Habits;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace HabitProbe;

/// <summary>
/// Runs the Phase 32 miner over a real journal folder and prints what it concluded, per Commander.
/// <para>
/// The counterpart to spike/CorpusReplay: that one asks whether the fold throws on real data, and
/// this one asks whether the claims it produces are the ones the corpus was measured to support
/// before the detectors were written. Every figure in docs/plans/phase-32-it-learns-your-mistakes.md
/// came out of here.
/// </para>
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

        var watch = Stopwatch.StartNew();
        var reports = new HabitMiner(new ConsoleLogger<HabitMiner>()).Mine(directory, DateTimeOffset.UtcNow);
        watch.Stop();

        Console.WriteLine($"{reports.Count} Commander(s) in {watch.Elapsed.TotalSeconds:0.0}s from {directory}");

        foreach (var report in reports)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {(report.FrontierId.Length == 0 ? "(no id)" : report.FrontierId)} "
                + $"— {report.Journals} journals, {report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd}");

            if (report.Refusal is { Length: > 0 } refusal)
            {
                Console.WriteLine($"  refused: {refusal}");
                continue;
            }

            foreach (var claim in report.Claims)
            {
                Console.WriteLine($"  CLAIM  {claim.Key,-20} {claim.Evidence.Occurrences}/{claim.Evidence.Opportunities}"
                    + $" recent={claim.Evidence.Recent} occasion={claim.Occasion}");
                Console.WriteLine($"         {claim.Spoken()}");
            }

            foreach (var silence in report.Quiet)
            {
                Console.WriteLine($"  quiet  {silence.Key,-20} {silence.Why}");
            }
        }

        return 0;
    }
}

/// <summary>Warnings and above to stderr. Core only brings the abstractions, so this is the whole of it.</summary>
internal sealed class ConsoleLogger<T> : ILogger<T>
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
        if (IsEnabled(logLevel))
        {
            Console.Error.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
