using D47.Core.Audio;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The decorators over <see cref="ITtsProvider.StreamAsync"/>.</summary>
public class StreamedSpeechIsBilledOnceWholeTests
{
    private static readonly byte[] Pcm = new byte[9600];

    [Fact]
    public async Task ACompletedClipIsRecordedOnceWithItsDuration()
    {
        var spend = new SpeechSpend();
        var tts = new StreamingTtsProvider();
        ITtsProvider metered = new MeteredTtsProvider(tts, spend);

        var clip = await metered.StreamAsync("Shields up.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        clip.Append(Pcm);

        Assert.Empty(spend.Charges);

        clip.Complete();
        await StreamingTtsProvider.Eventually(() => spend.Charges.Count == 1, "the clip was never recorded");
        await Task.Delay(30, TestContext.Current.CancellationToken);

        var charge = Assert.Single(spend.Charges);
        Assert.Equal(1, charge.Utterances);
        Assert.Equal("Shields up.".Length, charge.Characters);
        Assert.Equal((await clip.Whole).Duration, charge.Audio);
    }

    [Fact]
    public async Task AFaultedClipIsNotRecorded()
    {
        var spend = new SpeechSpend();
        ITtsProvider metered = new MeteredTtsProvider(new StreamingTtsProvider(), spend);

        var clip = await metered.StreamAsync("Shields up.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        clip.Append(Pcm);
        clip.Fail(new TtsException("the body stopped"));

        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(spend.Charges);
    }

    [Fact]
    public async Task ACancelledClipIsNotRecorded()
    {
        var spend = new SpeechSpend();
        ITtsProvider metered = new MeteredTtsProvider(new StreamingTtsProvider(), spend);
        using var cancel = new CancellationTokenSource();

        var clip = await metered.StreamAsync("Shields up.", VoiceSelection.Default, cancel.Token);
        clip.Append(Pcm);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clip.Whole);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(spend.Charges);
    }

    [Fact]
    public async Task ACastVoiceRefusedBeforeTheStreamFallsBackToThePinnedVoice()
    {
        var chosen = new StreamingTtsProvider { Refuses = "chosen-voice" };
        var pinned = new StreamingTtsProvider();
        var failures = new List<string>();
        ITtsProvider cast = new FallingBackTtsProvider(chosen, pinned, "pinned-voice", failures.Add);

        var clip = await cast.StreamAsync("Hold position.", new VoiceSelection("chosen-voice"), TestContext.Current.CancellationToken);

        Assert.Same(await pinned.ClipFor("Hold position."), clip);
        Assert.Equal(["pinned-voice"], pinned.Voices);
        Assert.Single(failures);
    }
}
