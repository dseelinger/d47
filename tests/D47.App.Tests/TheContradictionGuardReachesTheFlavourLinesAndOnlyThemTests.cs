using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The guard between composition and speech runs on the five flavour paths and on nothing else. Four
/// are in <c>AppHost.cs</c>; the announcement rewrite is in Core's <c>Rewording.cs</c>. An invented
/// speaker's reply and the Narrator's reply are screened by Core's <c>ChatterLine.cs</c> and
/// <c>NarratorLine.cs</c>, each with a ship reader from the app.
/// </summary>
public class TheContradictionGuardReachesTheFlavourLinesAndOnlyThemTests
{
    /// <summary>
    /// The five flavour paths, exactly: the persona's return-after-a-gap line, its introduction, the
    /// announcement rewrite that carries every ambient remark and carrier line, one line of an invented
    /// exchange, and a stock story's clue.
    /// </summary>
    private const int FlavourCallSites = 5;

    [Fact]
    public void TheGuardWithItsOneRetryIsReachedFromTheFiveFlavourPaths()
    {
        var guarded = CodeLinesContaining("ContradictedClaims.SayableAsync(");

        Assert.Equal(FlavourCallSites, guarded.Count);
    }

    /// <summary>
    /// Three of the five have an authored line behind the model's, and all three of those are checked
    /// as well: an authored line asserting cargo that was not aboard is the incident this guard was
    /// reported for. The announcement path checks its fallback and its as-written line (#214) at one
    /// call site.
    /// </summary>
    [Fact]
    public void EveryAuthoredFallbackBehindAFlavourLineIsCheckedToo()
    {
        var checkedFallbacks = CodeLinesContaining("ContradictedClaims.Sayable(");

        Assert.Equal(FlavourCallSites - 2, checkedFallbacks.Count);
    }

    /// <summary>And nowhere else: outside the app only the four Core files, inside it only three methods.</summary>
    [Fact]
    public void NothingOutsideTheCompositionRootReachesTheGuard()
    {
        var src = Path.Combine(AppSource.RepositoryRoot(), "src");
        var app = Path.Combine(src, "D47.App") + Path.DirectorySeparatorChar;
        var skipped = new[] { $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}" };

        var reaching = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(app, StringComparison.Ordinal))
            .Where(file => !skipped.Any(segment => file.Contains(segment, StringComparison.Ordinal)))
            .Where(file => File.ReadAllText(file).Contains("ContradictedClaims", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order()
            .ToList();

        Assert.Equal(["ChatterLine.cs", "ContradictedClaims.cs", "NarratorLine.cs", "Rewording.cs"], reaching);

        string[] permitted = ["OnPersonaChanged", "ComposeNpcChatterAsync", "ComposeStoryLineAsync"];
        var stray = AppSource.Files
            .SelectMany(file => file.Tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(identifier => identifier.Identifier.ValueText == "ContradictedClaims")
                .Select(identifier => (file, identifier)))
            .Where(pair => !permitted.Contains(
                pair.identifier.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText))
            .Select(pair => $"{pair.file.Name}:{pair.identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
            .ToList();

        Assert.True(stray.Count == 0, "ContradictedClaims is reached outside the three flavour methods:" + Environment.NewLine + string.Join(Environment.NewLine, stray));
    }

    /// <summary>
    /// Read once per line and handed to the guard, rather than each claim asking the game state for
    /// itself: the ship must not appear to change underneath one line and its retry.
    /// </summary>
    [Fact]
    public void TheShipIsSnapshotOncePerFlavourLine()
    {
        var read = CodeLinesContaining("ShipFacts.Of(");

        // Four for the five paths — the two persona paths are branches of one switch and share a
        // snapshot — and the readers ChatterLine and NarratorLine screen each reply against.
        Assert.Equal(FlavourCallSites + 1, read.Count);
        Assert.All(read, line => Assert.Contains(
            line,
            new[]
            {
                "var facts = ShipFacts.Of(GameState.Active);",
                "() => ShipFacts.Of(GameState.Active),",
                "() => ShipFacts.Of(gameState.Active),",
            }));

        // The announcement path hands Rewording a reader; Rewording reads it through one Lazy. That it is
        // read once, and before the model is asked, is tested in Core.
        var rewording = AppSource.CodeLinesIn(Rewording, "Lazy<ShipFacts>", "facts()").Select(line => line.Text).ToList();
        Assert.Contains("var ship = new Lazy<ShipFacts>(facts);", rewording);
        Assert.DoesNotContain(rewording, line => line.Contains("facts()", StringComparison.Ordinal));
    }

    private static readonly string Rewording = Path.Combine(
        AppSource.RepositoryRoot(), "src", "D47.Core", "Callouts", "Rewording.cs");

    /// <summary>
    /// Every code line of the app tree and <c>Rewording.cs</c> containing <paramref name="fragment"/>;
    /// comments are left out, since they discuss the guard by name at length.
    /// </summary>
    private static List<string> CodeLinesContaining(string fragment) =>
        [.. AppSource.CodeLines(fragment).Concat(AppSource.CodeLinesIn(Rewording, fragment)).Select(line => line.Text)];
}
