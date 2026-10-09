using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Persona;

[Trait("Category", "Integration")]
public sealed class NoStoryMeansEveryCoreTests
{
    [Fact]
    public void ANewInstallWithNoStoryOffersEveryGuardianCore()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());
        var cores = fixtures.Cores("F1");

        Assert.Equal(CoreHold.None, cores.Hold);
        Assert.All(PersonaCatalog.All, persona => Assert.True(cores.IsAwake(persona), persona.Id));
        Assert.True(cores.IsAwake(PersonaCatalog.Heretic));
    }

    [Fact]
    public void TheChosenCoreSpeaks()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());
        var host = new PersonaHost(PersonaCatalog.Covas, cores: fixtures.Cores("F1"));

        host.Apply(new PersonaSettings { Id = "kex" });

        Assert.Equal("kex", host.Current.Id);
    }

    [Fact]
    public void AnEndedStoryHoldsNothing()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());
        fixtures.Stories.Save("F1", new D47.Core.Stories.Story
        {
            Id = Id,
            Title = "T",
            PublicLayer = "P",
            PickedAt = Now,
            State = D47.Core.Stories.StoryState.Ended,
        });

        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);
    }
}
