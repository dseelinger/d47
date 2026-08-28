using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Per-category level, mute and ducking (Phase 12, "#96 Ambient audio mixer").
/// <para>
/// Driven against the null sink, so every claim about what would be audible is assertable with
/// no audio device present — which is the only way any of this can be covered at all. The
/// alternative is listening, and listening is not a test.
/// </para>
/// </summary>
public class AudioMixTests
{
    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate / 5 * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build(AudioMix? mix = null)
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        if (mix is not null)
        {
            arbiter.Mix = mix;
        }

        return (arbiter, sink);
    }

    private static AudioRequest On(AudioChannel channel, string name = "clip") =>
        new() { Channel = channel, Clip = Clip(name) };

    [Fact]
    public void ALevelReachesTheGainTheSinkIsGiven()
    {
        var (arbiter, sink) = Build(AudioMix.Default with { Speech = new ChannelMix(0.4, false, 1) });

        arbiter.Enqueue(On(AudioChannel.Speech));

        Assert.Equal(0.4f, sink.GainOf(sink.Started[0].Id)!.Value, 3);
    }

    [Fact]
    public void MutingACategorySilencesOnlyThatOne()
    {
        var (arbiter, sink) = Build(AudioMix.Default with
        {
            Bed = new ChannelMix(1, Muted: true, 0.35),
        });

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("bed"), Loop = true });
        arbiter.Enqueue(On(AudioChannel.Cue, "cue"));

        var bed = sink.Started.First(request => request.Clip.Name == "bed");
        var cue = sink.Started.First(request => request.Clip.Name == "cue");

        Assert.Equal(0f, sink.GainOf(bed.Id)!.Value);
        Assert.Equal(1f, sink.GainOf(cue.Id)!.Value);
    }

    /// <summary>
    /// The bed drops under speech and comes back when it stops. Recomputed from what is playing
    /// rather than held as a flag, which is why it cannot get stuck down.
    /// </summary>
    [Fact]
    public void ABedDucksUnderSpeechAndComesBackWhenItStops()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("bed"), Loop = true });

        var bed = sink.Started[0].Id;
        Assert.Equal(1f, sink.GainOf(bed)!.Value);

        arbiter.Enqueue(On(AudioChannel.Speech, "reply"));
        Assert.Equal(0.35f, sink.GainOf(bed)!.Value, 3);

        sink.CompleteCurrent();
        Assert.Equal(1f, sink.GainOf(bed)!.Value, 3);
    }

    /// <summary>And how far it drops is the Commander's, not a constant in the arbiter.</summary>
    [Fact]
    public void HowFarACategoryDucksFollowsItsOwnSetting()
    {
        var (arbiter, sink) = Build(AudioMix.Default with { Bed = new ChannelMix(0.8, false, 0.5) });

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("bed"), Loop = true });
        var bed = sink.Started[0].Id;

        arbiter.Enqueue(On(AudioChannel.Speech));

        // The duck is a fraction of the level rather than a level of its own: turn the category
        // down and its ducked form goes down with it.
        Assert.Equal(0.4f, sink.GainOf(bed)!.Value, 3);
    }

    /// <summary>An alert ducks nothing of its own, and neither does speech.</summary>
    [Fact]
    public void WhatEverythingElseDucksUnderDoesNotDuckItself()
    {
        var (arbiter, sink) = Build(AudioMix.Default with
        {
            Alert = new ChannelMix(0.9, false, 0.1),
            Speech = new ChannelMix(0.9, false, 0.1),
        });

        arbiter.Enqueue(On(AudioChannel.Alert, "danger"));

        Assert.Equal(0.9f, sink.GainOf(sink.Started[0].Id)!.Value, 3);

        Assert.False(AudioMix.Ducks(AudioChannel.Speech));
        Assert.False(AudioMix.Ducks(AudioChannel.Alert));
    }

    /// <summary>
    /// A level moved while something is audible takes effect on the thing that is audible. A
    /// mixer that only applied to the next clip is a mixer nobody can set by ear.
    /// </summary>
    [Fact]
    public void AChangeReachesWhatIsAlreadyPlaying()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("bed"), Loop = true });
        var bed = sink.Started[0].Id;

        arbiter.Mix = AudioMix.Default with { Bed = new ChannelMix(0.25, false, 0.35) };

        Assert.Equal(0.25f, sink.GainOf(bed)!.Value, 3);
    }

    /// <summary>
    /// Every channel has a level and a mute, generated from the enum — so a category added later
    /// cannot ship without them, which would present as "d47 is too loud and there is no setting
    /// for it".
    /// </summary>
    [Fact]
    public void EveryChannelHasItsRows()
    {
        var rows = AudioCapability.Create().Settings.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var channel in Enum.GetValues<AudioChannel>())
        {
            Assert.Contains(AudioCapability.LevelKey(channel), rows);
            Assert.Contains(AudioCapability.MuteKey(channel), rows);

            // And a duck row exactly where ducking means something.
            Assert.Equal(AudioMix.Ducks(channel), rows.Contains(AudioCapability.DuckKey(channel)));
        }
    }

    /// <summary>
    /// No tool surface. A model that can turn its own voice down, or the danger callouts', is a
    /// model that can make itself harder to hear at the moment hearing it matters.
    /// </summary>
    [Fact]
    public void TheModelCannotReachTheMixer()
    {
        Assert.Empty(AudioCapability.Create().Tools);
    }

    /// <summary>
    /// The defaults are what d47 sounded like before there was a mixer. Turning this on must not
    /// change what a Commander who never touches it hears.
    /// </summary>
    [Fact]
    public void TheDefaultsAreWhatItAlreadySoundedLike()
    {
        var mix = AudioMix.Default;

        Assert.Equal(1, mix.Speech.Level);
        Assert.Equal(1, mix.Cue.Level);
        Assert.Equal(1, mix.Alert.Level);
        Assert.Equal(1, mix.Bed.Level);

        // The constant the arbiter used to carry, now sayable.
        Assert.Equal(0.35, mix.Bed.DuckUnderSpeech);

        Assert.All(
            Enum.GetValues<AudioChannel>(),
            channel => Assert.False(mix.For(channel).Muted));
    }

    /// <summary>A level out of range is clamped rather than believed. Only a hand edit produces one.</summary>
    [Theory]
    [InlineData(-2.0, 0f)]
    [InlineData(4.0, 1f)]
    public void AnOutOfRangeLevelIsClamped(double level, float expected)
    {
        Assert.Equal(expected, new ChannelMix(level, false, 1).GainWhile(speaking: false));
    }
}
