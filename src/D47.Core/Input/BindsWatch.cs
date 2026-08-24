using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>
/// The Commander's bindings, re-read when Elite rewrites them (remediation.md 16, item 2).
/// <para>
/// <b>They used to be read once, at startup, and the reason given for that was wrong.</b> The
/// composition root said the file changes only when the Commander edits their controls, "which
/// they cannot do while d47 is the foreground window" — true, and beside the point. Controls are
/// edited in <em>Elite's</em> options menu, where Elite is the foreground window and d47 is not;
/// and the moment a Commander is most likely to go there is straight after d47 has told them an
/// action is not bound. From then until a restart, every injection sent the old scancode and
/// every answer about what is reachable was about a preset that is no longer in use.
/// </para>
/// <para>
/// <b>Two files, not one.</b> A rebind rewrites the <c>.binds</c> file; switching preset rewrites
/// <c>StartPreset</c> and changes which <c>.binds</c> file is the answer. Watching only the file
/// currently resolved would miss the second entirely — and a preset switch is the larger change
/// of the two, since it can move every binding at once.
/// </para>
/// <para>
/// <b>Pull-based and clock-free</b>, like every other reader in Core: the tick loop calls
/// <see cref="Poll"/>, exactly as it does for <see cref="Actions.MacroStore"/>, so no thread and
/// no watcher lives down here. What is compared is the files' own write stamps, which is I/O
/// about a file rather than a reading of the clock.
/// </para>
/// <para>
/// <b>Safe for the prompt cache</b>, which is worth stating because it is the obvious objection.
/// Bindings never reach a tool schema — capability descriptors are registered once and never
/// mutated. What they reach is the reachability sentence in the game-state block, which sits
/// <em>below</em> the cache breakpoint and is expected to change every turn. So a reload changes
/// what d47 says is possible and costs nothing in cached tokens.
/// </para>
/// </summary>
public sealed class BindsWatch
{
    /// <summary>What a tick that could not read the files reports. See <see cref="Stamp"/>.</summary>
    private const string Unreadable = "unreadable";

    private readonly string _bindingsDirectory;
    private readonly IReadOnlyList<string> _gameDirectories;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    /// <summary>
    /// How many consecutive polls will retry a read that could not be made, before the watcher
    /// stops asking and waits for the files to move again. At the tick's 10 Hz this is three
    /// seconds, and the thing being waited out is Elite finishing with a file of a few dozen
    /// bytes — so it is generous by a wide margin rather than finely judged.
    /// </summary>
    private const int RetryPolls = 30;

    private EliteBinds _current;
    private string _stamp;
    private int _retries;

    /// <summary>
    /// Resolves once, immediately, so the first caller sees the same thing it always did. The
    /// startup read was never the defect — never reading again was.
    /// </summary>
    public BindsWatch(string bindingsDirectory, IEnumerable<string> gameDirectories, ILogger logger)
    {
        _bindingsDirectory = bindingsDirectory;
        _gameDirectories = [.. gameDirectories];
        _logger = logger;
        _current = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, logger);
        _stamp = Stamp(_current);
    }

    /// <summary>What is bound right now. Never null; <see cref="EliteBinds.None"/> when unresolved.</summary>
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
    /// Re-reads if either file moved. Returns whether anything was reloaded.
    /// <para>
    /// The stamp is compared before anything is parsed, so the ordinary tick costs two or three
    /// calls to ask a file when it was last written and no XML at all.
    /// </para>
    /// </summary>
    public bool Poll()
    {
        var now = Stamp(_current);

        if (string.Equals(now, _stamp, StringComparison.Ordinal))
        {
            return false;
        }

        var reloaded = BindsResolver.Resolve(_bindingsDirectory, _gameDirectories, _logger, out var unreadable);

        // **A read that failed never replaces one that worked** (#24). Elite held StartPreset open
        // for a moment on 2026-08-24, the resolve came back empty, 347 bindings became none, and
        // nothing asked again for two hours and forty-one minutes — every key d47 can press dead,
        // and only the arrival honk talkative enough to say so. There is no state in which zero
        // bindings is a better answer than the ones that were working a moment ago.
        if (unreadable)
        {
            if (_retries < RetryPolls)
            {
                _retries++;

                // The stamp is deliberately *not* advanced, which is what makes the next tick try
                // again. A lock is a moment rather than a state, so waiting for the file to move
                // a second time is waiting for something that may never happen.
                _logger.LogWarning(
                    "The bindings could not be re-read this time; keeping the {Count} already "
                    + "loaded and trying again ({Attempt} of {Limit})",
                    _current.Bindings.Count,
                    _retries,
                    RetryPolls);

                return false;
            }

            // Out of attempts. The stamp advances so this stops parsing every tick, and the
            // bindings still stand — giving up on the read is not a reason to give up on them.
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

        // Advanced after the read rather than before it. It used to go first, so that a file which
        // could not be read was not re-parsed ten times a second for the rest of the session —
        // the retry budget above is what buys that now, and it buys it without also making the
        // failure permanent.
        _stamp = now;

        lock (_gate)
        {
            _current = reloaded;
        }

        // The preset is named because the two changes read very differently in a log: a rebind
        // keeps the name and moves one key, and a preset switch can move all of them.
        _logger.LogInformation(
            "The bindings file changed; re-read {Count} bindings from preset {Preset}",
            reloaded.Bindings.Count,
            reloaded.PresetName ?? "none");

        // The stamp is taken again because resolving may have landed on a different file — a
        // preset switch does exactly that — and the one just recorded describes the old one.
        _stamp = Stamp(reloaded);

        return true;
    }

    /// <summary>
    /// What the watched files look like right now, as one comparable string.
    /// <para>
    /// Length as well as write time, because a write stamp has a coarser resolution than the time
    /// it takes to save a bindings file twice — and because the pair costs one <c>FileInfo</c>
    /// either way.
    /// </para>
    /// <para>
    /// Every <c>StartPreset*</c>, not the one <see cref="BindsResolver"/> would pick: which of them
    /// wins is version-ordered, so a Commander whose game writes a new highest version has changed
    /// their preset by adding a file rather than by editing one.
    /// </para>
    /// </summary>
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
                // Recorded rather than skipped: an absent folder becoming a present one is
                // Elite being installed or run for the first time, and is worth a re-read.
                parts.Add("no bindings folder");
            }

            if (binds.SourceFile is { } source)
            {
                parts.Add(Of(source));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file being written as it is asked about is the normal case here — Elite saves
            // bindings while d47 is polling. One constant rather than the previous stamp, so a
            // run of unreadable ticks compares equal to itself and re-reads nothing, and the
            // first readable tick after it compares different and re-reads once.
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
