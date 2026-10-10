using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// "the" is optional in every declared phrase, on every model-free route, and dropping or adding it
/// is an exact match rather than a near miss (#525).
/// </summary>
public class TheIsOptionalInEveryPhraseTests
{
    /// <summary>The same fold the router applies internally, reimplemented here so the test does not
    /// need internal access.</summary>
    private static string WithoutThe(string phrase) =>
        string.Join(
            ' ',
            phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !string.Equals(word, "the", StringComparison.OrdinalIgnoreCase)));

    /// <summary>What a phrase entry reaches, close enough to <c>PhraseBook.Target</c> to detect a collision.</summary>
    private static string Target(PhraseEntry entry) =>
        entry.Row is { } row
            ? $"setting {row.Key}={entry.Value}"
            : $"tool {entry.CapabilityId}/{entry.ToolName}?" +
              string.Join('&', entry.Arguments.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"{a.Key}={a.Value}"));

    [Theory]
    [InlineData("open galaxy map", "open the galaxy map")]
    [InlineData("open system map", "open the system map")]
    [InlineData("open left panel", "open the left panel")]
    [InlineData("open cargo scoop", "open the cargo scoop")]
    [InlineData("target next system", "target the next system")]
    public void ADeclaredPhraseWithoutTheReachesTheSameActionAsWithIt(string withoutThe, string withThe)
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var bare = router.MatchToolCommand(withoutThe);
        var declared = router.MatchToolCommand(withThe);

        Assert.NotNull(bare);
        Assert.NotNull(declared);
        Assert.Equal(declared.CapabilityId, bare.CapabilityId);
        Assert.Equal(declared.ToolName, bare.ToolName);
        Assert.Equal(declared.Arguments.Values, bare.Arguments.Values);
    }

    [Theory]
    [InlineData("engage hyperspace")]
    [InlineData("hyperspace")]
    public void HyperspaceIsReachableByBothDeclaredWords(string said)
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        Assert.NotNull(router.MatchToolCommand(said));
    }

    [Fact]
    public void AddingTheWhereNoneWasDeclaredStillMatches()
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var withThe = router.MatchToolCommand("open the galaxy map");
        var withoutThe = router.MatchToolCommand("open galaxy map");

        Assert.NotNull(withThe);
        Assert.NotNull(withoutThe);
        Assert.Equal(withThe.ToolName, withoutThe.ToolName);
        Assert.Equal(withThe.Arguments.Values, withoutThe.Arguments.Values);
    }

    [Fact]
    public void ASettingCommandPhraseMatchesWithoutIts()
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var withThe = (
            from capability in registry.All
            from row in capability.Descriptor.Settings
            from command in row.Commands
            where command.Phrase.Contains("the ", StringComparison.OrdinalIgnoreCase)
            select command.Phrase).FirstOrDefault();

        Assert.NotNull(withThe);

        Assert.NotNull(router.MatchSetting(withThe!));
        Assert.NotNull(router.MatchSetting(WithoutThe(withThe!)));
    }

    [Fact]
    public void ExactMatchBeatsANearMissOfferOnceTheIsDropped()
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var book = PhraseBook.From(registry, []);

        var candidates = book.Candidates("open galaxy map", InputSource.Typed);

        var galaxyMap = candidates.FirstOrDefault(c => c.Phrase.Contains("galaxy map", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(galaxyMap);
        Assert.Equal(PhraseMatch.Equivalent, galaxyMap!.Match);
    }

    /// <summary>
    /// Removing "the" from every declared phrase must not merge two entries that reach different
    /// targets, so a name like "The Oracle" keeps its own identity against a name that only differs
    /// from it by "the".
    /// </summary>
    [Fact]
    public void NoTwoPhrasesReachingDifferentTargetsCollideOnceTheIsRemoved()
    {
        var install = new MemoryInstall();
        var registry = TestSurface.For(install).Registry;
        var book = PhraseBook.From(registry, []);

        // Grouped by phrase text first, since the same wording legitimately reaches more than one
        // target from different declaration sources (a keyword with no single answering tool, say).
        // Only a fold that merges two *different* wordings into one, and in doing so loses the
        // distinction between their targets, is the collision this issue asks to catch.
        var collisions = book.Entries
            .GroupBy(entry => entry.Phrase.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => (Phrase: group.Key, Targets: group.Select(Target).ToHashSet(StringComparer.Ordinal)))
            .GroupBy(item => WithoutThe(item.Phrase), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1 && group.Select(item => item.Targets).Any(
                targets => group.Any(other => !ReferenceEquals(other.Targets, targets) && !other.Targets.Overlaps(targets))))
            .ToArray();

        Assert.True(
            collisions.Length == 0,
            "These phrases become the same phrase once \"the\" is removed, but reach different targets: " +
            string.Join(
                "; ",
                collisions.Select(g => $"'{g.Key}': {string.Join(", ", g.Select(e => e.Phrase).Distinct())}")));
    }
}
