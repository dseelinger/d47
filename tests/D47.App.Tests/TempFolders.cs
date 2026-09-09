using System.Collections.Concurrent;

namespace D47.App.Tests;

/// <summary>Temporary folders that go away again.</summary>
internal static class TempFolders
{
    /// <summary>Old enough that nothing running now can still want it.</summary>
    private static readonly TimeSpan Stale = TimeSpan.FromDays(1);

    private static readonly ConcurrentBag<string> Created = [];

    static TempFolders()
    {
        SweepStale();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveCreated();
    }

    /// <summary>A folder under the system temp directory, removed when this run ends.</summary>
    public static string Create(string prefix)
    {
        var folder = Directory.CreateTempSubdirectory(prefix).FullName;

        Created.Add(folder);

        return folder;
    }

    private static void RemoveCreated()
    {
        foreach (var folder in Created)
        {
            TryDelete(folder);
        }
    }

    private static void SweepStale()
    {
        try
        {
            var cutoff = DateTime.UtcNow - Stale;

            foreach (var folder in Directory.EnumerateDirectories(Path.GetTempPath(), "d47-*"))
            {
                if (Directory.GetLastWriteTimeUtc(folder) < cutoff)
                {
                    TryDelete(folder);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        // Tidiness is never worth failing a run over.
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
