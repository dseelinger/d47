using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>While paid calls are stopped, an unprompted line on a billed voice reaches no provider; a turn's answer and a free voice are unchanged.</summary>
public class AnAwayCommanderIsNotSpokenToOnAPaidVoiceTests
{
    private static (VoicePipeline Voice, RecordingProvider Provider) Build(string providerId, bool stopped)
    {
        var provider = new RecordingProvider(providerId);
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = provider,
            CuesEnabled = false,
            BedEnabled = false,
            PaidCallsStopped = () => stopped,
        };

        return (voice, provider);
    }

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(
            new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ACalloutOnABilledVoiceReachesNoProviderAndReturnsNull()
    {
        var (voice, provider) = Build(TtsProviderCatalog.ElevenLabsId, stopped: true);

        var clip = await voice.AnnounceAsync(new Announcement("fuel", "Fuel is low.") { Urgency = CalloutUrgency.Urgent });

        Assert.Null(clip);
        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task AnAnnouncementOnABilledVoiceReachesNoProvider()
    {
        var (voice, provider) = Build(TtsProviderCatalog.ElevenLabsId, stopped: true);

        var clip = await voice.AnnounceAsync("Fuel is low.");

        Assert.Null(clip);
        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task ACalloutOnAFreeVoiceIsSpoken()
    {
        var (voice, provider) = Build(TtsProviderCatalog.EdgeId, stopped: true);

        await voice.AnnounceAsync(new Announcement("fuel", "Fuel is low."));

        Assert.Contains("Fuel is low.", provider.Texts);
    }

    [Fact]
    public async Task ACalloutOnABilledVoiceIsSpokenWhenPaidCallsAreNotStopped()
    {
        var (voice, provider) = Build(TtsProviderCatalog.ElevenLabsId, stopped: false);

        await voice.AnnounceAsync(new Announcement("fuel", "Fuel is low."));

        Assert.Contains("Fuel is low.", provider.Texts);
    }

    [Fact]
    public async Task ATurnAnswerOnABilledVoiceIsSpokenWhilePaidCallsAreStopped()
    {
        var (voice, provider) = Build(TtsProviderCatalog.ElevenLabsId, stopped: true);

        await voice.RunAsync(Reply("Fuel is at ninety percent."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(provider.Texts, text => text.Contains("Fuel is at ninety percent."));
    }

    private sealed class RecordingProvider(string id) : ITtsProvider
    {
        private readonly List<string> _texts = [];

        public IReadOnlyList<string> Texts
        {
            get
            {
                lock (_texts)
                {
                    return [.. _texts];
                }
            }
        }

        public string Id => id;

        public string Name => id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("recording", "Recording", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default)
        {
            lock (_texts)
            {
                _texts.Add(text);
            }

            return Task.FromResult(new AudioClip(text, new byte[9_600], AudioFormat.Standard));
        }
    }
}
