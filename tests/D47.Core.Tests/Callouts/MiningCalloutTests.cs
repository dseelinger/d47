using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Prospector and core callouts (list.md Phase 18). Every shape and every threshold decision here
/// was measured against 1,633 real <c>ProspectedAsteroid</c> events — see
/// <c>docs/spikes/mining-callouts.md</c> — and the one that would have shipped a wrong answer is
/// the game's own <c>Content</c> grade.
/// </summary>
public class MiningCalloutTests
{
    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    /// <summary>A real rock: one material, and Elite grading it Low while it holds 58% Platinum.</summary>
    private const string Platinum =
        """
        {"timestamp":"2026-08-16T10:00:00Z","event":"ProspectedAsteroid",
         "Materials":[{"Name":"Platinum","Proportion":58.255268}],
         "Content":"$AsteroidMaterialContent_Low;","Content_Localised":"Material Content: Low",
         "Remaining":100.0}
        """;

    /// <summary>A real core, with the localised motherlode name absent — 2 of the 3 in the corpus.</summary>
    private const string Core =
        """
        {"timestamp":"2026-08-16T10:05:00Z","event":"ProspectedAsteroid",
         "Materials":[{"Name":"tritium","Proportion":23.450556},
                      {"Name":"liquidoxygen","Name_Localised":"Liquid oxygen","Proportion":5.142477}],
         "MotherlodeMaterial":"Alexandrite",
         "Content":"$AsteroidMaterialContent_Low;","Content_Localised":"Material Content: Low",
         "Remaining":100.0}
        """;

    // --------------------------------------------------------------- prospector

    [Fact]
    public void TheRichestMaterialIsSaidFirstWithItsPercentage()
    {
        var said = Assert.Single(new ProspectorCallout().Examine(Context(false, Platinum)));

        Assert.Equal("Platinum, 58.3%.", said.Text);
    }

    [Fact]
    public void EveryMaterialInTheRockIsNamedRichestFirst()
    {
        var said = Assert.Single(new ProspectorCallout().Examine(Context(false, Core)));

        Assert.Equal("Tritium, 23.5%, plus Liquid oxygen at 5.1%.", said.Text);
    }

    /// <summary>
    /// The measured trap. Elite grades this rock <c>Material Content: Low</c> while it holds 58.3%
    /// Platinum — and across the corpus 45% of rocks carrying a material at 40% or more are labelled
    /// Low, with Low and High having near-identical distributions. Passing the grade on would send a
    /// Commander past the best rock in the cluster.
    /// </summary>
    [Fact]
    public void TheGamesOwnContentGradeIsNeverRepeated()
    {
        var said = Assert.Single(new ProspectorCallout().Examine(Context(false, Platinum)));

        Assert.DoesNotContain("Low", said.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", said.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A session best needs something to have beaten. The first rock is not "the best you have
    /// seen" — it is the only one, and saying so on every first prospect would make the phrase mean
    /// nothing by the third.
    /// </summary>
    [Fact]
    public void TheFirstRockOfASessionIsNotAnnouncedAsAPersonalBest()
    {
        var callout = new ProspectorCallout();

        var first = Assert.Single(callout.Examine(Context(false, Platinum)));
        Assert.DoesNotContain("Best you have found", first.Text, StringComparison.Ordinal);

        var better = Assert.Single(callout.Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:10:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"Platinum","Proportion":66.7}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)));

        Assert.Contains("Best you have found this session", better.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void APoorerRockIsNotAnnouncedAsABest()
    {
        var callout = new ProspectorCallout();

        callout.Examine(Context(false, Platinum)).ToList();

        var worse = Assert.Single(callout.Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:10:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"Platinum","Proportion":12.0}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)));

        Assert.DoesNotContain("Best you have found", worse.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The identity fold. Elite writes <c>gallite</c> and <c>Gallite</c> for one material depending
    /// on whether it wrote a localised name — 40 raw spellings for 27 materials. Without folding,
    /// one material keeps two high water marks and announces a personal best that is not one.
    /// </summary>
    [Fact]
    public void OneMaterialSpeltTwoWaysKeepsOneSessionBest()
    {
        var callout = new ProspectorCallout();

        callout.Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:00:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"gallite","Proportion":50.0}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)).ToList();

        var second = Assert.Single(callout.Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:05:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"gallite","Name_Localised":"Gallite","Proportion":20.0}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)));

        Assert.Equal("Gallite, 20%.", second.Text);
        Assert.DoesNotContain("Best you have found", second.Text, StringComparison.Ordinal);
    }

    /// <summary>Spoken as a name, not as the slug Elite happens to have written.</summary>
    [Fact]
    public void AMaterialWithNoLocalisedNameIsStillSpokenAsAName()
    {
        var said = Assert.Single(new ProspectorCallout().Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:00:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"bromellite","Proportion":36.0}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)));

        Assert.Equal("Bromellite, 36%.", said.Text);
    }

    /// <summary>
    /// The backlog is folded and not spoken — otherwise starting d47 after an hour of mining reads
    /// out an hour of rocks. But the high water marks must survive it, or the first live rock claims
    /// a session best over a baseline of nothing.
    /// </summary>
    [Fact]
    public void ThePrimingBacklogSetsTheBaselineWithoutSayingAnything()
    {
        var callout = new ProspectorCallout();

        Assert.Empty(callout.Examine(Context(true, Platinum)));

        var after = Assert.Single(callout.Examine(Context(
            false,
            """
            {"timestamp":"2026-08-16T10:10:00Z","event":"ProspectedAsteroid",
             "Materials":[{"Name":"Platinum","Proportion":12.0}],
             "Content":"$AsteroidMaterialContent_Low;","Remaining":100.0}
            """)));

        Assert.DoesNotContain("Best you have found", after.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two rocks are two prospects, not one warning said twice — so the keys differ and the engine's
    /// cooldown cannot swallow the second.
    /// </summary>
    [Fact]
    public void TwoProspectsGetTwoKeys()
    {
        var said = new ProspectorCallout().Examine(Context(false, Platinum, Core)).ToList();

        Assert.Equal(2, said.Count);
        Assert.NotEqual(said[0].Key, said[1].Key);
    }

    // --------------------------------------------------------------------- core

    [Fact]
    public void ACoreIsAnnouncedWithWhatIsInIt()
    {
        var said = Assert.Single(new CoreAsteroidCallout().Examine(Context(false, Core)));

        Assert.Equal("Core asteroid. Alexandrite.", said.Text);
    }

    [Fact]
    public void AnOrdinaryRockIsNotACore() =>
        Assert.Empty(new CoreAsteroidCallout().Examine(Context(false, Platinum)));

    /// <summary>
    /// Exciting is not dangerous. Urgent speaks over the top of whatever is being said and is for
    /// danger and fuel; a core announced across a hull warning would be the priority backwards.
    /// </summary>
    [Fact]
    public void ACoreDoesNotInterruptWhateverIsBeingSaid()
    {
        var said = Assert.Single(new CoreAsteroidCallout().Examine(Context(false, Core)));

        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
    }

    [Fact]
    public void NoCoreIsAnnouncedFromTheBacklog() =>
        Assert.Empty(new CoreAsteroidCallout().Examine(Context(true, Core)));

    /// <summary>
    /// The two are separate callouts so that turning the running commentary off leaves the rare
    /// announcement a Commander is actually mining for.
    /// </summary>
    [Fact]
    public void TheTwoCalloutsHaveTheirOwnIds()
    {
        Assert.Equal("prospector", new ProspectorCallout().Id);
        Assert.Equal("core-asteroid", new CoreAsteroidCallout().Id);
    }
}
