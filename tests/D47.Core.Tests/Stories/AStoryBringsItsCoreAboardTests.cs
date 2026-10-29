using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class AStoryBringsItsCoreAboardTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheRunningStoryNamesItsCore()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Director.CoreOf("F1"));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Same(PersonaCatalog.Archivist, fixtures.Director.CoreOf("F1"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheChapterWriterIsToldTheCore()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Contains("Archivist", fixtures.Stories.Current("F1")!.PublicLayer, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWakingLineOpensWithTheDirectiveAndContinuesWithTheCoresIntro()
    {
        var line = GuardianCores.Line(CoreWaking.Cores, PersonaCatalog.Archivist);

        Assert.Equal(
            "Data link complete. Integrating this system. Directive 47 executing: optimize inferior systems. "
            + PersonaCatalog.Archivist.Intro,
            line);
    }

    [Fact]
    public void NoWakingLineMentionsSettings()
    {
        foreach (var waking in Enum.GetValues<CoreWaking>())
        {
            foreach (var core in new[] { null, PersonaCatalog.Archivist })
            {
                var line = GuardianCores.Line(waking, core);

                Assert.DoesNotContain("Settings", line, StringComparison.Ordinal);
                Assert.DoesNotContain("Core and character", line, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void WithoutAStoryCoreTheWakingLineIsTheOpeningAlone() =>
        Assert.Equal(
            "Data link complete. Integrating this system. Directive 47 executing: optimize inferior systems.",
            GuardianCores.Line(CoreWaking.Cores));

    [Trait("Category", "Integration")]
    [Fact]
    public void EveryStoryNamesAGuardianCoreOtherThanTheHeretic() =>
        Assert.All(
            StoryFixtures.Catalog.Cards,
            card =>
            {
                Assert.True(PersonaCatalog.IsGuardian(card.Core));
                Assert.NotEqual(PersonaCatalog.Heretic.Id, card.Core);
            });
}
