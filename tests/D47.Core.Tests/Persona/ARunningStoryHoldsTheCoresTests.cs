using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Persona;

public sealed class ARunningStoryHoldsTheCoresTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    private static readonly PersonaSettings Kex = new() { Id = "kex" };

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PickingAStoryWhileKexIsChosenPutsTheStockCoreAboard()
    {
        using var fixtures = Fixtures();
        var host = new PersonaHost(PersonaCatalog.Resolve("kex"), cores: fixtures.Cores("F1"));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        host.Apply(Kex);

        Assert.Same(PersonaCatalog.Covas, host.Current);
        Assert.Equal(new CoreHold(HeldCores.All, Card.Title), fixtures.Cores("F1").Hold);
        Assert.True(fixtures.Cores("F1").IsAwake(PersonaCatalog.Covas));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ABeaconScanWakesEveryCoreButTheHereticAndKexSpeaksAgain()
    {
        using var fixtures = Fixtures();
        var host = new PersonaHost(PersonaCatalog.Resolve("kex"), cores: fixtures.Cores("F1"));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        host.Apply(Kex);

        Assert.Equal(CoreWaking.Cores, fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1)));
        host.Apply(Kex);

        Assert.Equal("kex", host.Current.Id);
        Assert.False(fixtures.Cores("F1").IsAwake(PersonaCatalog.Heretic));
        Assert.True(fixtures.Cores("F1").IsAwake(PersonaCatalog.Warden));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASecondBeaconSystemWakesTheHereticAndTheSameSystemDoesNot()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(CoreWaking.Cores, fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1)));
        Assert.Null(fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(2)));
        Assert.False(fixtures.Cores("F1").IsAwake(PersonaCatalog.Heretic));

        Assert.Equal(CoreWaking.Heretic, fixtures.ScanBeacon("F1", SecondBeaconAddress, Now.AddHours(3)));
        Assert.True(fixtures.Cores("F1").IsAwake(PersonaCatalog.Heretic));
        Assert.Equal([BeaconAddress, SecondBeaconAddress], fixtures.Stories.Current("F1")!.BeaconSystems);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AScanBeforeTheStoryWasPickedDoesNotCount()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Null(fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(-1)));

        Assert.Equal(HeldCores.All, fixtures.Cores("F1").Hold.Cores);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AReplayedScanSaysNothingAgain()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var woke = BeaconFixture.Events().Select(e => fixtures.Director.Observe(e, "F1")).OfType<CoreWaking>().ToList();
        var again = BeaconFixture.Events().Select(e => fixtures.Director.Observe(e, "F1")).OfType<CoreWaking>().ToList();

        Assert.Equal([CoreWaking.Cores], woke);
        Assert.Empty(again);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ADataPointOutsideABeaconSystemWakesNothing()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        foreach (var journalEvent in BeaconFixture.Events().Take(BeaconFixture.BeforeTheBeacon))
        {
            Assert.Null(fixtures.Director.Observe(journalEvent, "F1"));
        }

        Assert.Null(fixtures.Director.Observe(BeaconFixture.DataPoint(), "F1"));
        Assert.Equal(HeldCores.All, fixtures.Cores("F1").Hold.Cores);
    }

    [Fact]
    public void AStoryScannedBeforeTheSystemsWereRecordedHoldsOnlyTheHeretic()
    {
        var story = new D47.Core.Stories.Story { Id = Id, Title = "T", PublicLayer = "P", PickedAt = Now, BeaconScanAt = Now };

        Assert.Equal(HeldCores.Heretic, story.HeldCores);
    }
}
