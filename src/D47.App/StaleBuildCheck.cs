using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>
/// What a build output directory looks like when the executable in it is not the one the build
/// just produced (reported 2026-08-23).
/// <para>
/// <b>The trap.</b> A single-file publish had been written into <c>bin\Debug\…\win-x64\</c> at some
/// point in the past, leaving a 74 MB <c>d47.exe</c> with every assembly inside it. That exe never
/// reads the <c>d47.dll</c> beside it — so <c>dotnet build</c> and <c>dotnet run</c> faithfully
/// rebuilt the DLLs and the running app ignored them completely. Three fixes in a row appeared not
/// to work, each was re-diagnosed from scratch, and it cost about two hours and a wiped data folder
/// before anybody looked at a file size.
/// </para>
/// <para>
/// <b>The shape, and why it cannot happen legitimately.</b> A normal apphost is a couple of hundred
/// kilobytes and sits beside the loose assemblies it loads. A single-file bundle is tens of
/// megabytes and ships <em>without</em> a loose <c>d47.dll</c>, because the assembly is inside it.
/// A large executable <em>and</em> a loose managed assembly beside it is therefore not a layout any
/// build produces: it is one layout left lying in another's way.
/// </para>
/// <para>
/// <b>Reported rather than enforced.</b> This says so on the way past — it does not refuse to
/// start, because a heuristic that can be wrong must not be the thing that stops d47 running on
/// somebody's machine. The whole cost of that morning was that nothing said anything at all.
/// </para>
/// </summary>
internal static class StaleBuildCheck
{
    /// <summary>
    /// Bigger than any apphost and far smaller than any bundle. The measured pair either side of it
    /// are 206 KB and 74,742,715 bytes, so the threshold is a judgement with two orders of
    /// magnitude of room rather than a number to tune.
    /// </summary>
    internal const long BundleBytes = 5L * 1024 * 1024;

    /// <summary>
    /// What is wrong with this pair of files, or null where nothing is. Pure, so the situation can
    /// be described in a test without laying out a 74 MB file to do it.
    /// </summary>
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

        // A build writes both, so the assembly cannot legitimately be the newer of the two by more
        // than the moments a build takes. A minute of slack keeps a slow copy or a coarse
        // filesystem timestamp from crying wolf.
        return assembly - executableWritten > TimeSpan.FromMinutes(1)
            ? "the assemblies beside the executable are newer than the executable itself, so a "
              + "build has landed that this exe will not load"
            : null;
    }

    /// <summary>
    /// Looks at what is actually on disk beside the running executable and says so if it is the
    /// shape above. Never throws: a check that can stop d47 starting is worse than the trap.
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
