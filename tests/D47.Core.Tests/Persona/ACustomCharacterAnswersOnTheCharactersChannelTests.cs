using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A custom character answers on the CHARACTERS channel: clear aboard, faded by distance when not, refused beyond range.</summary>
public class ACustomCharacterAnswersOnTheCharactersChannelTests
{
    private static readonly CustomCharacter Aboard = new("aaaa0001", "Mara", null, "gruff", true, null);

    private static CustomCharacter Remote(string system = "Colonia") =>
        new("bbbb0002", "Juno", null, "warm and chatty", false, system);

    private static (CharacterLine Line, List<string> Asked) Build(CustomCharacter character, double? lightYears)
    {
        List<string> asked = [];
        var line = new CharacterLine(
            () => [character],
            () => "Sol",
            (from, to, _) =>
            {
                asked.Add($"{from}>{to}");
                return Task.FromResult(lightYears);
            });

        return (line, asked);
    }

    private static CommsSelection To(CustomCharacter character) => OnComms.Character(character.Id, character.Name);

    private static async Task<LineDecision.Taken> TakenAsync(CharacterLine line, CustomCharacter character, string input) =>
        Assert.IsType<LineDecision.Taken>(await line.RouteAsync(To(character), input, CancellationToken.None));

    [Fact]
    public async Task AnAboardCharacterIsOnTheIntercomWithAClearSignal()
    {
        var (line, asked) = Build(Aboard, 900);

        var taken = await TakenAsync(line, Aboard, "how are we doing?");

        Assert.Equal(VoiceRole.Crew, taken.Speaker.Role);
        Assert.False(RadioVoice.IsOverTheAir(taken.Speaker.Role));
        Assert.Equal(1, taken.Speaker.Signal);
        Assert.Equal("how are we doing?", taken.Question);
        Assert.Contains("You are Mara", taken.Speaker.Brief);
        Assert.Contains("gruff", taken.Speaker.Brief);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task ARemoteCharacterIsFadedByDistance()
    {
        var (line, asked) = Build(Remote(), 375);

        var taken = await TakenAsync(line, Remote(), "are you there?");

        Assert.Equal(VoiceRole.Comms, taken.Speaker.Role);
        Assert.True(RadioVoice.IsOverTheAir(taken.Speaker.Role));
        Assert.Equal(LinkSignal.Strength(375), taken.Speaker.Signal);
        Assert.InRange(taken.Speaker.Signal, 0.01, 0.99);
        Assert.Equal(["Sol>Colonia"], asked);
    }

    [Fact]
    public async Task ARemoteCharacterBeyondRangeIsRefusedWithoutAModelCall()
    {
        var (line, _) = Build(Remote(), 1200);

        var decision = await line.RouteAsync(To(Remote()), "are you there?", CancellationToken.None);

        var refused = Assert.IsType<LineDecision.Refused>(decision);
        Assert.Equal("Juno is 1,200 light years away, beyond comms range.", refused.Line);
    }

    [Fact]
    public async Task AnUnknownDistanceIsClear()
    {
        var (line, _) = Build(Remote(), null);

        var taken = await TakenAsync(line, Remote(), "are you there?");

        Assert.Equal(1, taken.Speaker.Signal);
    }

    [Fact]
    public async Task ACharacterInTheCommandersSystemIsClearWithoutAskingTheGalaxy()
    {
        var (line, asked) = Build(Remote("sol"), 900);

        var taken = await TakenAsync(line, Remote("sol"), "are you there?");

        Assert.Equal(1, taken.Speaker.Signal);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task OnlyTheSelectedCharacterAnswersAndADismissalLeavesThemSelected()
    {
        var (line, _) = Build(Aboard, null);

        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(OnComms.Character("someone-else", "Mara"), "how are we doing?", CancellationToken.None));
        Assert.IsType<LineDecision.NotMine>(await line.RouteAsync(OnComms.Captain, "how are we doing?", CancellationToken.None));

        await TakenAsync(line, Aboard, "that's all Mara");

        var follow = await TakenAsync(line, Aboard, "and the fuel?");
        Assert.Equal("and the fuel?", follow.Question);
    }

    [Fact]
    public async Task ACharacterOnCommsIsHeardInTheirStoredVoiceAndShownWithTheirPicture()
    {
        var voiced = Aboard with { Voice = new CharacterVoice("kokoro", "am_adam") };
        var surface = TestSurface.For(new MemoryInstall());

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            FakeLlmProvider.Answering("Holding steady."))
        {
            Persona = "You are the ship's AI.",
            CommsSelected = () => To(voiced),
        };

        loop.Lines.Add(new CharacterLine(() => [voiced], () => "Sol"));

        var events = await OnComms.SayAsync(loop, "how are we doing?");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());

        Assert.Equal(new CharacterVoice("kokoro", "am_adam"), addressed.Voice);
        Assert.Equal("character.aaaa0001", addressed.Picture);
    }

    [Fact]
    public void TheGalaxySearchDisclosureNamesRemoteCharacterSystemsOnlyWhileOneExists()
    {
        var settings = D47Settings.Defaults with { Knowledge = D47Settings.Defaults.Knowledge with { GalaxySearch = true } };

        var without = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false);
        var with = EgressDisclosure.Entry(
            EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false, remoteCharacterSystems: ["Colonia"]);

        Assert.DoesNotContain("remote characters", without.What);
        Assert.Contains("the systems your remote characters are in (Colonia)", with.What);
    }
}
