using System.Diagnostics;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>How far the walk over older journals has got (#148).</summary>
public enum HistoryState
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Walking.</summary>
    Running,

    /// <summary>Finished; the four dictionaries are there.</summary>
    Done,

    /// <summary>Threw; the four dictionaries are not there and never will be.</summary>
    Failed,
}

/// <summary>
/// The four folds over journals d47 was not running for, run together and off the startup path. Owns no
/// thread: a caller runs <see cref="Run"/> on whichever one it wants the walk on (#148).
/// </summary>
public sealed class HistoryBackfill
{
    private readonly Stopwatch _elapsed = new();

    private int _saidAtSecond = -1;

    /// <summary>The journal folder to walk.</summary>
    public required string Directory { get; init; }

    public required ILoggerFactory Loggers { get; init; }

    /// <summary>What the loadout file already holds, and how far it has been folded.</summary>
    public LoadoutStore? LoadoutFile { get; init; }

    /// <summary>The same for the names, and where this walk's result is written back.</summary>
    public HeardNamesStore? NameFile { get; init; }

    /// <summary>Times one fold, where the caller measures the steps of startup.</summary>
    public Func<string, IDisposable>? Step { get; init; }

    /// <summary>Stamps the watermark the names write back.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.Now;

    /// <summary>Raised on the thread <see cref="Run"/> is on, at most once a second while it walks.</summary>
    public event Action? Changed;

    public HistoryState State { get; private set; } = HistoryState.Pending;

    /// <summary>How long the walk has been going, or took.</summary>
    public TimeSpan Elapsed => _elapsed.Elapsed;

    /// <summary>Why the walk failed, or null.</summary>
    public string? Failure { get; private set; }

    /// <summary>Whether the walk has yet to produce an answer, one way or the other.</summary>
    public bool Pending => State is HistoryState.Pending or HistoryState.Running;

    public IReadOnlyDictionary<string, FleetRegistry>? Fleets { get; private set; }

    public IReadOnlyDictionary<string, ShipLoadouts>? Loadouts { get; private set; }

    public IReadOnlyDictionary<string, CarrierState>? Carriers { get; private set; }

    public IReadOnlyDictionary<string, SpokenNames>? Names { get; private set; }

    /// <summary>
    /// Walks the four, in order, on the calling thread. A second call does nothing: the answer is wanted
    /// once.
    /// </summary>
    public void Run()
    {
        if (State is not HistoryState.Pending)
        {
            return;
        }

        State = HistoryState.Running;
        _elapsed.Start();
        Changed?.Invoke();

        try
        {
            // The order the restore hooks read them in.
            Fleets = Timed(
                "fleet backfill",
                () => FleetBackfill.FromHistory(Directory, Loggers.CreateLogger(nameof(FleetBackfill))));

            Loadouts = Timed(
                "loadout backfill",
                () => LoadoutBackfill.FromHistory(
                    Directory,
                    Loggers.CreateLogger(nameof(LoadoutBackfill)),
                    LoadoutFile?.All,
                    LoadoutFile?.FoldedThrough));

            Carriers = Timed(
                "carrier backfill",
                () => CarrierBackfill.FromHistory(Directory, Loggers.CreateLogger(nameof(CarrierBackfill))));

            Names = Timed("spoken names", MineNames);

            State = HistoryState.Done;
        }
        catch (Exception ex)
        {
            Failure = ex.Message;
            State = HistoryState.Failed;

            Loggers.CreateLogger<HistoryBackfill>()
                .LogError(ex, "Reading the journal history failed; nothing older than this session is restored");
        }
        finally
        {
            _elapsed.Stop();
            Changed?.Invoke();
        }
    }

    private IReadOnlyDictionary<string, SpokenNames> MineNames()
    {
        var found = SpokenNameMiner.FromHistory(
            Directory,
            Loggers.CreateLogger(nameof(SpokenNameMiner)),
            NameFile?.All.ToDictionary(entry => entry.Key, entry => entry.Value.Names, StringComparer.Ordinal),
            NameFile?.FoldedThrough,
            new Ticking(this));

        // Written straight back, so the expensive first walk happens once rather than at every start until
        // something else prompts a save.
        NameFile?.RememberNames(found, Now());

        return found;
    }

    private T Timed<T>(string name, Func<T> fold)
    {
        if (Step is null)
        {
            return fold();
        }

        using (Step(name))
        {
            return fold();
        }
    }

    /// <summary>
    /// Raises <see cref="Changed"/> when the second a reader would display changes, which is as often as
    /// the panel's line needs rewriting.
    /// </summary>
    private sealed class Ticking(HistoryBackfill backfill) : IProgress<double>
    {
        public void Report(double value)
        {
            var second = (int)backfill._elapsed.Elapsed.TotalSeconds;

            if (second == backfill._saidAtSecond)
            {
                return;
            }

            backfill._saidAtSecond = second;
            backfill.Changed?.Invoke();
        }
    }
}
