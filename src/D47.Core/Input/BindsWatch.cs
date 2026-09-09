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

    /// <summary>Resolves once, immediately, so the first caller sees the same thing it always did.</summary>
    public BindsWatch(string bindingsDirectory, IEnumerable<string> gameDirectories, ILogger logger)
    {
        _bindingsDirectory = bindingsDirectory;
        _gameDirectories = [.. gameDirectories];
        _logger = logger;
        _current = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, logger);
        _stamp = Stamp(_current);
    }

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

    /// <summary>Re-reads if either file moved.</summary>
    public bool Poll()
    {
        var now = Stamp(_current);

        if (string.Equals(now, _stamp, StringComparison.Ordinal))
        {
            return false;
        }

        var reloaded = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, _logger, out var unreadable);

        // **A read that failed never replaces one that worked** (#24).
        if (unreadable)
        {
            if (_retries < RetryPolls)
            {
                _retries++;

                // The stamp is deliberately *not* advanced, which is what makes the next tick try again.
                _logger.LogWarning(
                    "The bindings could not be re-read this time; keeping the {Count} already "
                    + "loaded and trying again ({Attempt} of {Limit})",
                    _current.Bindings.Count,
                    _retries,
                    RetryPolls);

                return false;
            }

            // Out of attempts.
            _stamp = now;
            _retries = 0;

            _logger.LogWarning(
                "The bindings still could not be read after {Limit} attempts; keeping the "
                + "{Count} already loaded",
                RetryPolls,
                _current.Bindings.Count);

            return false;
        }

        _retries = 0;

        // Advanced after the read rather than before it.
        _stamp = now;

        lock (_gate)
        {
            _current = reloaded;
        }

        // The preset is named because the two changes read very differently in a log: a rebind keeps the name
        // and moves one key, and a preset switch can move all of them.
        _logger.LogInformation(
            "The bindings file changed; re-read {Count} bindings from preset {Preset}",
            reloaded.Bindings.Count,
            reloaded.PresetName ?? "none");

        // The stamp is taken again because resolving may have landed on a different file — a preset switch
        // does exactly that — and the one just recorded describes the old one.
        _stamp = Stamp(reloaded);

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
}
