using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>
/// What a build output directory looks like when the executable in it is not the one the build just
/// produced (reported 2026-08-23).
/// </summary>
internal static class StaleBuildCheck
{
    /// <summary>Bigger than any apphost and far smaller than any bundle.</summary>
    internal const long BundleBytes = 5L * 1024 * 1024;

    /// <summary>What is wrong with this pair of files, or null where nothing is.</summary>
    /// <param name="executableBytes">The size of the running executable.</param>
    /// <param name="executableWritten">When it was written.</param>
    /// <param name="assemblyWritten">
    /// When the managed assembly beside it was written, or null where there is none — which is the
    /// ordinary published layout and never a problem.
    /// </param>
    internal static string? Wrong(
        long executableBytes,
        DateTime executableWritten,
        DateTime? assemblyWritten)
    {
        if (assemblyWritten is not { } assembly)
        {
            return null;
        }

        if (executableBytes >= BundleBytes)
        {
            return "the executable is a self-contained bundle and there are loose assemblies beside "
                   + "it, so those assemblies are not what is running";
        }

        // A build writes both, so the assembly cannot legitimately be the newer of the two by more than the
        // moments a build takes.
        return assembly - executableWritten > TimeSpan.FromMinutes(1)
            ? "the assemblies beside the executable are newer than the executable itself, so a "
              + "build has landed that this exe will not load"
            : null;
    }

    /// <summary>
    /// Looks at what is actually on disk beside the running executable and says so if it is the shape
    /// above.
    /// </summary>
    public static void Report(ILogger logger, string executablePath)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var executable = new FileInfo(executablePath);

            if (!executable.Exists)
            {
                return;
            }

            var assembly = new FileInfo(
                Path.Combine(
                    executable.DirectoryName ?? string.Empty,
                    Path.GetFileNameWithoutExtension(executable.Name) + ".dll"));

            var wrong = Wrong(
                executable.Length,
                executable.LastWriteTimeUtc,
                assembly.Exists ? assembly.LastWriteTimeUtc : null);

            if (wrong is null)
            {
                return;
            }

            logger.LogWarning(
                "The build in {Folder} looks stale: {What}. {Executable} is {Bytes:N0} bytes, "
                + "written {Written:u}. Delete it and build again.",
                executable.DirectoryName,
                wrong,
                executable.Name,
                executable.Length,
                executable.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogDebug(ex, "Could not check whether the running build is stale");
        }
    }
}
