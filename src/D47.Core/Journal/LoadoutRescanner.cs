using D47.Core.Persona;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Describes the stored loadouts and rebuilds them from every journal on disk.</summary>
public sealed class LoadoutRescanner
{
    private readonly GameStateStore _state;
    private readonly LoadoutStore? _store;
    private readonly IFileSystem _files;
    private readonly string? _directory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Func<DateTimeOffset> _now;
    private int _rescanning;

    public LoadoutRescanner(
        GameStateStore state,
        LoadoutStore? store,
        IFileSystem files,
        string? journalDirectory,
        ILoggerFactory loggerFactory,
        Func<DateTimeOffset> now)
    {
        _state = state;
        _store = store;
        _files = files;
        _directory = journalDirectory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LoadoutRescanner>();
        _now = now;
    }

    public string Remembered()
    {
        if (_state.Active?.Loadouts is not { IsKnown: true } ships)
        {
            return "No ship has been seen inside yet. Board one and D47 will remember it.";
        }

        var oldest = ships.Ships.Values.Min(ship => ship.SeenAt);
        var count = ships.Ships.Count;

        return count == 1
            ? $"One ship, last seen {TelemetryDelta.Spoken(_now() - oldest)} ago."
            : $"{count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} ships, the "
              + $"oldest last seen {TelemetryDelta.Spoken(_now() - oldest)} ago.";
    }

    /// <summary>Reads every journal on disk again and rebuilds what each ship was last seen holding (#128).</summary>
    public async Task<string?> RescanAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_directory is not { Length: > 0 } directory || _store is not { } store)
        {
            return "There is no journal folder to read.";
        }

        if (Interlocked.Exchange(ref _rescanning, 1) == 1)
        {
            return "A rescan is already running.";
        }

        try
        {
            var found = await Task.Run(
                () => LoadoutBackfill.Rescan(
                    _files,
                    directory,
                    _loggerFactory.CreateLogger(nameof(LoadoutBackfill)),
                    progress),
                cancellationToken).ConfigureAwait(false);

            if (found.Files == 0)
            {
                _logger.LogWarning("A rescan read no journals from {Directory}; nothing was changed", directory);

                return $"I could not read any journals in {directory}, so nothing was changed. "
                       + "Check the journal folder and try again.";
            }

            _state.ReplaceLoadouts(found.ByCommander);

            // Dated off the newest thing the walk saw rather than off the clock, so the next start's catch-up
            // begins where this left off.
            var through = found.ByCommander.Values
                .SelectMany(ships => ships.Ships.Values)
                .Select(ship => ship.SeenAt)
                .DefaultIfEmpty(_now())
                .Max();

            store.Save(_state.All, through);

            _logger.LogInformation(
                "A rescan read {Files} journals and remembered {Ships} ship(s)", found.Files, found.Ships);

            return found.Ships == 0
                ? $"Read {found.Files} journals and found no ships in them. Nothing is remembered now."
                : Sentence(found, _state.Active?.Identity.FrontierId);
        }
        catch (OperationCanceledException)
        {
            return "The rescan was stopped. Nothing was changed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "A rescan of {Directory} could not be completed", directory);
            return "I could not read the journals through. Nothing was changed.";
        }
        finally
        {
            _ = Interlocked.Exchange(ref _rescanning, 0);
        }
    }

    // Splits a rescan's total by commander, so the sentence matches "What is fitted, remembered"
    // above it rather than summing every FID in the journal folder (#246).
    public static string Sentence(LoadoutRescan found, string? activeFid)
    {
        if (activeFid is not { Length: > 0 })
        {
            return $"Read {found.Files} journals. {found.Ships} {Noun("ship", found.Ships)} remembered.";
        }

        var mine = found.ByCommander.TryGetValue(activeFid, out var loadouts) ? loadouts.Ships.Count : 0;
        var others = found.ByCommander.Where(pair => pair.Key != activeFid).ToList();

        if (others.Count == 0)
        {
            return $"Read {found.Files} journals. {mine} {Noun("ship", mine)} remembered.";
        }

        var otherShips = others.Sum(pair => pair.Value.Ships.Count);

        return $"Read {found.Files} journals. {mine} {Noun("ship", mine)} remembered for this commander, "
               + $"and {otherShips} for {others.Count} {Noun("other commander", others.Count)} on this machine.";

        static string Noun(string singular, int count) => count == 1 ? singular : singular + "s";
    }
}
