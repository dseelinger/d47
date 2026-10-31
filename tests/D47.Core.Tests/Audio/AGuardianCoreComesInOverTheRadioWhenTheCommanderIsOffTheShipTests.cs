using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;
using static D47.Core.Tests.Audio.PcmChunking;

namespace D47.Core.Tests.Audio;

public class AGuardianCoreComesInOverTheRadioWhenTheCommanderIsOffTheShipTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly AudioClip Speech = PcmConverter.ToStandard(StandInVoice.Clip);

    private static readonly SpeechSettings NoneTicked = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = false })],
        },
    };

    private static readonly SpeechSettings DamagedCore = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianPresets.EffectsFor(GuardianPresets.Find("damaged-core")!)],
        },
    };

    private static GameStatus Status(StatusFlags flags, StatusFlags2 two = StatusFlags2.None, DateTimeOffset? read = null) =>
        new() { Flags = flags, Flags2 = (uint)two, ReadAt = read ?? Now };

    [Fact]
    public void TheMainShipAloneIsNotAway() =>
        Assert.False(Status(StatusFlags.InMainShip).IsAwayFromShipAt(Now));

    [Theory]
    [InlineData(StatusFlags.InMainShip, StatusFlags2.OnFoot)]
    [InlineData(StatusFlags.InMainShip, StatusFlags2.InTaxi)]
    [InlineData(StatusFlags.InMainShip, StatusFlags2.InMulticrew)]
    [InlineData(StatusFlags.InSrv, StatusFlags2.None)]
    [InlineData(StatusFlags.InFighter, StatusFlags2.None)]
    [InlineData(StatusFlags.InMainShip | StatusFlags.InSrv, StatusFlags2.None)]
    [InlineData(StatusFlags.InMainShip | StatusFlags.InFighter, StatusFlags2.None)]
    public void EachWayOfBeingOffTheShipIsAway(StatusFlags flags, StatusFlags2 two) =>
        Assert.True(Status(flags, two).IsAwayFromShipAt(Now));

    [Fact]
    public void AStatusWithoutTheMainShipFlagButInTheGameIsAway() =>
        Assert.True(Status(StatusFlags.None, StatusFlags2.OnFoot).IsAwayFromShipAt(Now));

    [Fact]
    public void AStatusThatIsNotLiveIsNotAway()
    {
        Assert.False(Status(StatusFlags.None, StatusFlags2.OnFoot, Now - TimeSpan.FromMinutes(11)).IsAwayFromShipAt(Now));
        Assert.False(GameStatus.Unknown.IsAwayFromShipAt(Now));
        Assert.False(Status(StatusFlags.None).IsAwayFromShipAt(Now));
    }

    [Fact]
    public void AStockCoreHasNoAwayChain()
    {
        Assert.Null(GuardianVoice.AwayColourFor(DamagedCore, PersonaCatalog.Covas));
        Assert.Null(GuardianVoice.AwayRunningColourFor(DamagedCore, PersonaCatalog.Covas));
    }

    [Fact]
    public void ACoreWithNoEffectTickedStillGetsTheLink()
    {
        var whole = GuardianVoice.AwayColourFor(NoneTicked, PersonaCatalog.Warden)!(Speech).Pcm.ToArray();

        Assert.Equal(RadioVoice.Apply(Speech, 1).Pcm.ToArray(), whole);
        Assert.NotNull(GuardianVoice.AwayRunningColourFor(NoneTicked, PersonaCatalog.Warden));
    }

    [Fact]
    public void TheAwayChainRunsTheEffectsThenTheLink()
    {
        var treated = GuardianVoice.Apply(Speech, GuardianVoice.Effects(DamagedCore.GuardianVoice), GuardianVoice.BasePitchHz(PersonaCatalog.Warden.VoiceHint.Gender));

        Assert.Equal(
            RadioVoice.Apply(treated, 1).Pcm.ToArray(),
            GuardianVoice.AwayColourFor(DamagedCore, PersonaCatalog.Warden)!(Speech).Pcm.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheAwayRunningFilterGivesTheSameBytesHoweverTheInputIsCut(bool effects)
    {
        var speech = effects ? DamagedCore : NoneTicked;
        var factory = GuardianVoice.AwayRunningColourFor(speech, PersonaCatalog.Warden)!;
        var pcm = Speech.Pcm.ToArray();

        Assert.Equal(Whole(factory(), pcm), Chunked(factory(), pcm, 7));
    }
}
