using System.Collections.Concurrent;

namespace D47.App.Tests;

/// <summary>
/// Temporary folders that go away again.
/// <para>
/// Tests here create real folders because the code under test does real file I/O — an installer
/// swap, a shortcut, a coverage file. None of them removed what they made, and one machine
/// accumulated <b>9,900 folders and 422 MB</b> across a few weeks of runs, which is slow to
/// enumerate, noisy to search and actively misleading: hunting a Commander's real install
/// through a temp folder holding eight hundred copies of <c>d47.exe</c> is a genuine cost.
/// </para>
/// <para>
/// Two mechanisms, because either alone leaves a gap. Folders this run created are deleted when
/// the run ends, which handles the common case. Anything left by an earlier run — killed test
/// host, crash, or a version of this file that did not exist yet — is swept on the way in,
/// <b>but only when it is over a day old</b>, so a concurrent run's live folders are never
/// touched. Both are best-effort: failing to delete a temporary folder must never fail a test.
/// </para>
/// </summary>
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
