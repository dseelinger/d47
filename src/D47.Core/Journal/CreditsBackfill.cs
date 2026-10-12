using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The credits each Commander's newest LoadGame reported, recovered from earlier journals.</summary>
public static class CreditsBackfill
{
    /// <summary>Each Commander's credits at their newest LoadGame, keyed by Frontier id, over a list of journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, long> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var credits = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            Stream stream;

            try
            {
                stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {File} for LoadGame events", file);
                continue;
            }

            using (stream)
            {
                using var reader = new StreamReader(stream);

                while (reader.ReadLine() is { } line)
                {
                    if (!line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
                        || !JournalEvent.TryParse(line, logger, out var parsed)
                        || parsed is null)
                    {
                        continue;
                    }

                    if (parsed.String("FID") is { Length: > 0 } fid && parsed.Long("Credits") is { } held)
                    {
                        credits[fid] = held;
                    }
                }
            }
        }

        return credits;
    }
}
