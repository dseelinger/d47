using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A custom character answers when named: clear aboard, faded by distance when not, refused beyond range.</summary>
public class ACustomCharacterAnswersByNameTests
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

    private static async Task<LineDecision.Taken> TakenAsync(CharacterLine line, string input) =>
        Assert.IsType<LineDecision.Taken>(await line.RouteAsync(input, CancellationToken.None));

    [Fact]
    public async Task AnAboardCharacterIsOnTheIntercomWithAClearSignal()
    {
        var (line, asked) = Build(Aboard, 900);

        var taken = await TakenAsync(line, "Mara, how are we doing?");

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

        var taken = await TakenAsync(line, "Juno, are you there?");

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

        var decision = await line.RouteAsync("Juno, are you there?", CancellationToken.None);

        var refused = Assert.IsType<LineDecision.Refused>(decision);
        Assert.Equal("Juno is 1,200 light years away, beyond comms range.", refused.Line);
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task AnUnknownDistanceIsClear()
    {
        var (line, _) = Build(Remote(), null);

        var taken = await TakenAsync(line, "Juno, are you there?");

        Assert.Equal(1, taken.Speaker.Signal);
    }

    [Fact]
    public async Task ACharacterInTheCommandersSystemIsClearWithoutAskingTheGalaxy()
    {
        var (line, asked) = Build(Remote("sol"), 900);

        var taken = await TakenAsync(line, "Juno, are you there?");

        Assert.Equal(1, taken.Speaker.Signal);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task TheLineStaysOpenUntilDismissedAndAnUnnamedTurnIsNotTaken()
    {
        var (line, _) = Build(Aboard, null);

        Assert.IsType<LineDecision.NotMine>(await line.RouteAsync("how are we doing?", CancellationToken.None));

        await TakenAsync(line, "Mara, hello");
        Assert.True(line.IsOpen);

        var follow = await TakenAsync(line, "and the fuel?");
        Assert.Equal("and the fuel?", follow.Question);

        await TakenAsync(line, "that's all Mara");
        Assert.False(line.IsOpen);
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
