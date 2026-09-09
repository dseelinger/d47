using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The guard between composition and speech runs on the four flavour paths and on nothing else.
/// </summary>
public class TheContradictionGuardReachesTheFlavourLinesAndOnlyThemTests
{
    /// <summary>
    /// The four flavour paths, exactly: the persona's return-after-a-gap line, its introduction, the
    /// announcement rewrite that carries every ambient remark and carrier line, and one line of an
    /// invented exchange.
    /// </summary>
    private const int FlavourCallSites = 4;

    [Fact]
    public void TheGuardWithItsOneRetryIsReachedFromTheFourFlavourPaths()
    {
        var guarded = CodeLinesContaining("ContradictedClaims.SayableAsync(");

        Assert.Equal(FlavourCallSites, guarded.Count);
    }

    /// <summary>
    /// Three of the four have an authored line behind the model's, and all three of those are checked
    /// as well: an authored line asserting cargo that was not aboard is the incident this guard was
    /// reported for.
    /// </summary>
    [Fact]
    public void EveryAuthoredFallbackBehindAFlavourLineIsCheckedToo()
    {
        var checkedFallbacks = CodeLinesContaining("ContradictedClaims.Sayable(");

        Assert.Equal(FlavourCallSites - 1, checkedFallbacks.Count);
    }

    /// <summary>And nowhere else in the app.</summary>
    [Fact]
    public void NothingOutsideTheCompositionRootReachesTheGuard()
    {
        var source = Path.Combine(RepositoryRoot(), "src");

        var reaching = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("ContradictedClaims", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order()
            .ToList();

        Assert.Equal(["AppHost.cs", "ContradictedClaims.cs"], reaching);
    }

    /// <summary>
    /// Read once per line and handed to the guard, rather than each claim asking the game state for
    /// itself: the ship must not appear to change underneath one line and its retry.
    /// </summary>
    [Fact]
    public void TheShipIsSnapshotOncePerFlavourLine()
    {
        var read = CodeLinesContaining("ShipFacts.Of(");

        // Three, not four: the two persona paths are branches of one switch and share a snapshot.
        Assert.Equal(FlavourCallSites - 1, read.Count);
        Assert.All(read, line => Assert.Equal("var facts = ShipFacts.Of(GameState.Active);", line));
    }

    /// <summary>
    /// Every line of <c>AppHost.cs</c> containing <paramref name="fragment"/>, trimmed, with comments
    /// left out — the comments discuss the guard by name at length, and a gate that counted those would
    /// be counting its own explanation.
    /// </summary>
    private static List<string> CodeLinesContaining(string fragment) =>
        [.. File.ReadAllLines(Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs"))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => line.Contains(fragment, StringComparison.Ordinal))];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
