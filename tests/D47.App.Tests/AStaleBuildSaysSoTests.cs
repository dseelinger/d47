using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Reported 2026-08-23: a 74 MB single-file bundle left in <c>bin\Debug\…\win-x64\</c> shadowed
/// every build for hours. `dotnet build` rebuilt the DLLs, the bundle ignored them, three fixes in a
/// row appeared not to work, and each was re-diagnosed from scratch. Nothing said anything.
/// <para>
/// The check is pure so the situation can be described here without laying out a 74 MB file.
/// </para>
/// </summary>
public class AStaleBuildSaysSoTests
{
    private static readonly DateTime Tonight = new(2026, 8, 23, 21, 42, 0, DateTimeKind.Utc);

    /// <summary>
    /// The reported shape exactly: a bundle-sized exe with loose assemblies beside it. No build
    /// produces that pair — a bundle ships without them, because the assemblies are inside it.
    /// </summary>
    [Fact]
    public void ABundleWithLooseAssembliesBesideItIsCalledOut()
    {
        var wrong = StaleBuildCheck.Wrong(74_742_715, Tonight, Tonight.AddMinutes(1));

        Assert.NotNull(wrong);
        Assert.Contains("bundle", wrong, StringComparison.Ordinal);
    }

    /// <summary>
    /// The tell that was in plain view the whole morning, without the size: the exe dated 08-16 in
    /// a folder whose DLLs were minutes old.
    /// </summary>
    [Fact]
    public void AnExecutableOlderThanTheAssembliesBesideItIsCalledOut()
    {
        var wrong = StaleBuildCheck.Wrong(206_336, Tonight.AddDays(-7), Tonight);

        Assert.NotNull(wrong);
        Assert.Contains("newer than the executable", wrong, StringComparison.Ordinal);
    }

    /// <summary>An ordinary build: both written together, apphost-sized.</summary>
    [Fact]
    public void AnOrdinaryBuildIsQuiet()
    {
        Assert.Null(StaleBuildCheck.Wrong(206_336, Tonight, Tonight.AddSeconds(3)));
    }

    /// <summary>
    /// A published single-file build is a bundle with <em>no</em> loose assembly beside it, which
    /// is the whole of what makes the pair above diagnostic. It must never be called stale.
    /// </summary>
    [Fact]
    public void APublishedBundleIsNotStale()
    {
        Assert.Null(StaleBuildCheck.Wrong(74_742_715, Tonight, assemblyWritten: null));
    }

    /// <summary>
    /// A build that takes a while still writes both within a moment of each other, so the slack is
    /// there to stop a slow copy or a coarse timestamp from crying wolf.
    /// </summary>
    [Fact]
    public void SecondsOfSlackAreNotAComplaint()
    {
        Assert.Null(StaleBuildCheck.Wrong(206_336, Tonight, Tonight.AddSeconds(45)));
    }
}
