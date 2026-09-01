using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Listening;

/// <summary>
/// The Commander's own names, read out of the journals they already have
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>Why a walk rather than only the live fold.</b> A name is worth having in the catalogue the
/// first time it is <em>said</em>, which may be years after it was visited — so a Commander who
/// installs d47 today should still be able to be understood about a system they flew to last
/// summer. The live fold keeps it current; this is what makes it deep on the first run.
/// </para>
/// <para>
/// <b>Not a whole replay</b>, for the reason <see cref="LoadoutBackfill"/> gives: driving old
/// journals through <see cref="GameStateStore"/> would restore month-old positions and missions as
/// though they were current. This reads the same files for one subject.
/// </para>
/// <para>
/// <b>Only grows, so a seed and a catch-up are the same operation.</b> There is nothing to forget
/// and nothing to reconcile — running it twice over the same files costs time and changes nothing,
/// which is what makes the watermark an optimisation rather than a correctness device.
/// </para>
/// </summary>
public static class SpokenNameMiner
{
    /// <param name="stored">What the file already held, to add to rather than replace.</param>
    /// <param name="since">
    /// How far the file has been read, or null to read everything. Null is the first run, and it
    /// is the expensive one on purpose: measured on a 943-journal, 382 MB corpus it yields
    /// <b>15,216</b> names, and every one of them is a name the Commander might say tomorrow.
    /// </param>
    public static IReadOnlyDictionary<string, SpokenNames> FromHistory(
        string directory,
        ILogger logger,
        IReadOnlyDictionary<string, SpokenNames>? stored = null,
        DateTimeOffset? since = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return stored ?? new Dictionary<string, SpokenNames>(StringComparer.Ordinal);
        }

        var all = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        // Everything on the first run — there is no watermark to measure a gap against, and a
        // shallow catalogue is one that cannot recover the name the Commander is about to say.
        var walking = since is null ? all : LoadoutBackfill.Window(all, since);

        return FromHistory(walking, logger, stored, progress);
    }

    /// <summary>The same, over an explicit list oldest-first. What a test drives.</summary>
    public static IReadOnlyDictionary<string, SpokenNames> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        IReadOnlyDictionary<string, SpokenNames>? stored = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var found = stored is null
            ? new Dictionary<string, SpokenNames>(StringComparer.Ordinal)
            : new Dictionary<string, SpokenNames>(stored, StringComparer.Ordinal);

        var commander = string.Empty;

        for (var i = 0; i < files.Count; i++)
        {
            progress?.Report((double)i / Math.Max(1, files.Count));

            var reader = new JournalReader(files[i], logger);

            while (reader.Poll() is { Count: > 0 } batch)
            {
                foreach (var journalEvent in batch)
                {
                    // Carried across files, because a continuation journal re-emits Fileheader and
                    // not Commander — the same rule LoadoutBackfill follows.
                    if (journalEvent.Kind is "Commander" or "LoadGame"
                        && journalEvent.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var known = found.TryGetValue(commander, out var existing) ? existing : SpokenNames.Empty;

                    found[commander] = known.Apply(journalEvent);
                }
            }
        }

        progress?.Report(1);

        foreach (var (fid, names) in found)
        {
            logger.LogInformation(
                "Names this Commander has met, for hearing them right: {Count} for {Commander}",
                names.Names.Count,
                fid);
        }

        return found;
    }
}
