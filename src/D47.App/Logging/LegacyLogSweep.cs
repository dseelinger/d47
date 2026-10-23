using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Logging;

/// <summary>Deletes the <c>d47-*.jsonl</c> files earlier versions wrote beside the log.</summary>
public static class LegacyLogSweep
{
    /// <summary>Returns how many files were removed. A file that cannot be deleted is logged and skipped.</summary>
    public static int Run(IFileSystem files, string logsFolder, ILogger logger)
    {
        var removed = 0;

        foreach (var path in files.Enumerate(logsFolder, "d47-*.jsonl"))
        {
            try
            {
                files.Delete(path);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete the old log file {Path}", path);
            }
        }

        if (removed > 0)
        {
            logger.LogInformation("Deleted {Count} old .jsonl log files from {Folder}", removed, logsFolder);
        }

        return removed;
    }
}
