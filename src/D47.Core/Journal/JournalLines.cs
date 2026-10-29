using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

internal static class JournalLines
{
    /// <summary>One journal file's lines. An unreadable file logs a warning and yields nothing; a missing one does so only when <paramref name="warnIfMissing"/> is set.</summary>
    public static IEnumerable<string> Read(IFileSystem files, string file, ILogger logger, string purpose, bool warnIfMissing)
    {
        Stream? stream;

        try
        {
            stream = files.OpenRead(file);

            if (stream is null && warnIfMissing)
            {
                throw new FileNotFoundException("The journal file is missing.", file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for {Purpose}", file, purpose);
            yield break;
        }

        if (stream is null)
        {
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}
