using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AShuttlePilotIsHeardInTheCabinTests
{
    [Theory]
    [InlineData(ShuttleKind.Apex)]
    [InlineData(ShuttleKind.Frontline)]
    public async Task TheShuttlePilotIsInTheCabinAndSpeaksAsComms(ShuttleKind kind)
    {
        var ride = new ShuttleRide(kind, "Sol", "Abraham Lincoln", false);
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Shuttle = ride });
        var line = new ShuttleLine(() => ride, () => "Sol");

        var decision = await line.RouteAsync(channels.Selected!, "how long", TestContext.Current.CancellationToken);
        var speaker = Assert.IsType<LineDecision.Taken>(decision).Speaker;

        Assert.Equal(VoiceRole.Comms, speaker.Role);
        Assert.True(speaker.InCabin);
        Assert.False(RadioVoice.IsOverTheAir(speaker.Role, speaker.InCabin));
        Assert.Null(RadioVoice.Colours(speaker.Role, speaker.Signal, overheard: false, inCabin: speaker.InCabin));
        Assert.Null(RadioVoice.RunningColours(speaker.Role, speaker.Signal, overheard: false, inCabin: speaker.InCabin));
    }

    [Fact]
    public void ASpeakerOutsideTheCabinOnTheCommsRoleIsStillOverTheAir()
    {
        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.Comms, inCabin: false));
        Assert.NotNull(RadioVoice.Colours(VoiceRole.Comms, 1, overheard: false));
        Assert.NotNull(RadioVoice.RunningColours(VoiceRole.Comms, 1, overheard: false));
    }
}
