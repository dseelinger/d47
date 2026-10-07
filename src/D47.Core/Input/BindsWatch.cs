using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>The Commander's bindings, re-read when Elite rewrites them (remediation.md 16, item 2).</summary>
public sealed class BindsWatch
{
    /// <summary>What a tick that could not read the files reports.</summary>
    private const string Unreadable = "unreadable";

    private readonly string _bindingsDirectory;
    private readonly IReadOnlyList<string> _gameDirectories;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    /// <summary>
    /// How many consecutive polls will retry a read that could not be made, before the watcher stops
    /// asking and waits for the files to move again.
    /// </summary>
    private const int RetryPolls = 30;

    private EliteBinds _current;
    private string _stamp;
    private int _retries;

    /// <summary>A resolve has been dispatched and its result not yet adopted. Touched only by <see cref="Poll"/>.</summary>
    private bool _resolving;

    /// <summary>The finished resolve, written by the dispatched work and taken by <see cref="Poll"/>.</summary>
    private Resolved? _resolved;

    /// <summary>Resolves once, immediately, so the first caller sees the same thing it always did.</summary>
    public BindsWatch(string bindingsDirectory, IEnumerable<string> gameDirectories, ILogger logger)
    {
        _bindingsDirectory = bindingsDirectory;
        _gameDirectories = [.. gameDirectories];
        _logger = logger;
        _current = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, logger);
        _stamp = Stamp(_current);
    }

    /// <summary>Runs a resolve off the calling thread; the tick polls and must not walk the game folders.</summary>
    public Action<Action> Dispatch { get; init; } = work => _ = Task.Run(work);

    /// <summary>What is bound right now.</summary>
    public EliteBinds Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Starts a re-read through <see cref="Dispatch"/> if either file moved, and adopts a finished one; true when
    /// the bindings were replaced. One re-read runs at a time, and <see cref="Current"/> holds the previous bindings
    /// until a later poll adopts the new ones.
    /// </summary>
    public bool Poll()
    {
        if (!_resolving)
        {
            var now = Stamp(_current);

            // An unreadable stamp says nothing about whether the files moved; the next poll looks again.
            if (string.Equals(now, Unreadable, StringComparison.Ordinal)
                || string.Equals(now, _stamp, StringComparison.Ordinal))
            {
                return false;
            }

            _resolving = true;
            Dispatch(() => Volatile.Write(ref _resolved, Resolve(now)));
        }

        if (Interlocked.Exchange(ref _resolved, null) is not { } resolved)
        {
            return false;
        }

        _resolving = false;

        return Adopt(resolved);
    }

    /// <summary>Runs off the tick. Never throws: a failure is an unreadable result.</summary>
    private Resolved Resolve(string startedAt)
    {
        try
        {
            var binds = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, _logger, out var unreadable);

            // Taken from the file resolved, which a preset switch changes.
            return new Resolved(binds, unreadable, startedAt, Stamp(binds));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The bindings could not be resolved");

            return new Resolved(EliteBinds.None, Unreadable: true, startedAt, startedAt);
        }
    }

    private bool Adopt(Resolved resolved)
    {
        // **A read that failed never replaces one that worked** (#24).
        if (resolved.Unreadable)
        {
            if (_retries < RetryPolls)
            {
                _retries++;

                // The stamp is not advanced, so the next poll starts another re-read.
                _logger.LogWarning(
                    "The bindings could not be re-read this time; keeping the {Count} already "
                    + "loaded and trying again ({Attempt} of {Limit})",
                    _current.Bindings.Count,
                    _retries,
                    RetryPolls);

                return false;
            }

            // Out of attempts.
            _stamp = resolved.StartedAt;
            _retries = 0;

            _logger.LogWarning(
                "The bindings still could not be read after {Limit} attempts; keeping the "
                + "{Count} already loaded",
                RetryPolls,
                _current.Bindings.Count);

            return false;
        }

        _retries = 0;
        _stamp = resolved.Stamp;

        lock (_gate)
        {
            _current = resolved.Binds;
        }

        // The preset is named because the two changes read very differently in a log: a rebind keeps the name
        // and moves one key, and a preset switch can move all of them.
        _logger.LogInformation(
            "The bindings file changed; re-read {Count} bindings from preset {Preset}",
            resolved.Binds.Bindings.Count,
            resolved.Binds.PresetName ?? "none");

        return true;
    }

    /// <summary>What the watched files look like right now, as one comparable string.</summary>
    private string Stamp(EliteBinds binds)
    {
        var parts = new List<string>();

        try
        {
            if (Directory.Exists(_bindingsDirectory))
            {
                foreach (var start in Directory
                             .EnumerateFiles(_bindingsDirectory, "StartPreset*")
                             .Order(StringComparer.OrdinalIgnoreCase))
                {
                    parts.Add(Of(start));
                }
            }
            else
            {
                // Recorded rather than skipped: an absent folder becoming a present one is Elite being
                // installed or run for the first time, and is worth a re-read.
                parts.Add("no bindings folder");
            }

            if (binds.SourceFile is { } source)
            {
                parts.Add(Of(source));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file being written as it is asked about is the normal case here — Elite saves bindings while
            // d47 is polling.
            return Unreadable;
        }

        return string.Join("|", parts);

        static string Of(string path)
        {
            var info = new FileInfo(path);

            return info.Exists
                ? $"{path}@{info.LastWriteTimeUtc.Ticks}:{info.Length}"
                : $"{path}@gone";
        }
    }

    /// <param name="StartedAt">The stamp that prompted the re-read.</param>
    /// <param name="Stamp">The stamp taken just after it, of the file it landed on.</param>
    private sealed record Resolved(EliteBinds Binds, bool Unreadable, string StartedAt, string Stamp);
}
