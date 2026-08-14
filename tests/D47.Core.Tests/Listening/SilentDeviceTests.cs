using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Telling "the Commander said nothing" apart from "the device sent nothing".
/// <para>
/// A fresh install has no input device chosen, so Windows picks the default — and on a machine
/// with VR or streaming software that default is routinely a virtual endpoint which delivers
/// digital zeroes forever. It opens without error, captures for as long as the key is held, and
/// transcribes to nothing, which reported as "nothing intelligible" and read as though the
/// Commander had mumbled. Six consecutive holds on a real install looked exactly like a broken
/// push-to-talk key; push-to-talk was fine.
/// </para>
/// </summary>
public class SilentDeviceTests
{
    private const int Rate = 16000;

    [Fact]
    public void AStreamOfZeroesIsADeviceProblemRatherThanAQuietCommander()
    {
        var utterance = new Utterance(new float[Rate * 2], Rate);

        Assert.True(utterance.IsSilent);
        Assert.Equal(0f, utterance.Peak);
    }

    /// <summary>
    /// Room tone through a live microphone is very quiet and still nowhere near the floor: a
    /// threshold that called this silent would blame the hardware every time someone spoke
    /// softly, which is worse than the fault it is trying to name.
    /// </summary>
    [Fact]
    public void RoomToneFromALiveMicrophoneIsNotSilence()
    {
        var samples = new float[Rate];
        var noise = new Random(47);

        for (var i = 0; i < samples.Length; i++)
        {
            // About -60 dBFS, far below speech and far above a dead device.
            samples[i] = (float)((noise.NextDouble() - 0.5) * 0.002);
        }

        Assert.False(new Utterance(samples, Rate).IsSilent);
    }

    [Fact]
    public void SpeechIsNeverSilence()
    {
        var samples = new float[Rate];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Sin(i * 0.05f) * 0.3f;
        }

        var utterance = new Utterance(samples, Rate);

        Assert.False(utterance.IsSilent);
        Assert.True(utterance.Peak > 0.2f);
    }

    /// <summary>
    /// One live sample in an otherwise dead buffer still means the device is delivering, so
    /// this is deliberately peak rather than an average — an average would call a genuine
    /// utterance with long pauses in it silent.
    /// </summary>
    [Fact]
    public void OneAudibleSampleIsEnoughToProveTheDeviceIsAlive()
    {
        var samples = new float[Rate];
        samples[Rate / 2] = 0.5f;

        Assert.False(new Utterance(samples, Rate).IsSilent);
    }

    [Fact]
    public void AnEmptyUtteranceIsSilent()
    {
        Assert.True(new Utterance([], Rate).IsSilent);
    }
}
