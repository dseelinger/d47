using System.Text.Json;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>
/// A story run's line that kept no clip is synthesised in the speaker's current voice once, and the clip is kept in the
/// run's record; a voice the Commander asked to forget, or a provider that fails, holds it for reading instead.
/// </summary>
public sealed class ALineWithNoClipIsSpokenAgainOnceTests
{
    private const string Commander = "F100";
    private const string ForgottenId = "my-0123abcd";

    private static readonly Story Run = new() { Id = "quiet-relay", Title = "The Quiet Relay", PublicLayer = "-", PickedAt = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero) };

    private static readonly StoryRunLine Line = new()
    {
        Sent = Run.PickedAt.AddDays(20),
        From = MessageStore.Narrator,
        Body = "The coordinates point at an empty patch of the Pleiades.",
        Key = "line.1",
        AdventureKey = "ch1",
    };

    private sealed record Rig(StoryRunVoicing Voicing, StoryRunRecord Record, Recording Provider, Recording Chatterbox, MemoryFileSystem Files);

    private static Rig Build(MemoryFileSystem? files = null, PinnedVoice? pinned = null, bool fails = false, Func<string, bool>? forgotten = null)
    {
        files ??= Recorded();
        var record = new StoryRunRecord(files, "story-runs", StoryStore.InMemory(), () => Commander, null, new NoopProtector(), NullLogger.Instance);
        var provider = new Recording(TtsProviderCatalog.EdgeId, fails);
        var chatterbox = new Recording(TtsProviderCatalog.ChatterboxId, fails);
        var voice = new VoicePipeline(new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start(), CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = provider,
            SpeakerFor = _ => provider,
            PinnedFor = id => id == TtsProviderCatalog.ChatterboxId ? chatterbox : null,
            CuesEnabled = false,
            BedEnabled = false,
        };

        var voicing = new StoryRunVoicing(
            () => record,
            () => Commander,
            line => pinned is null
                ? new StoryLineSpeech(VoiceRole.Narrator, new VoiceSelection("narrator-voice"))
                : new StoryLineSpeech(VoiceRole.Crew, new VoiceSelection("crew-voice"), pinned),
            forgotten ?? (id => id == ForgottenId),
            (speech, text, progress) => voice.RenderAsync(text, speech.Role, speech.Voice, speech.Pinned, progress),
            NullLogger.Instance);

        return new Rig(voicing, record, provider, chatterbox, files);
    }

    /// <summary>A run's record holding <see cref="Line"/>, with no clip.</summary>
    private static MemoryFileSystem Recorded()
    {
        var files = new MemoryFileSystem();
        var folder = Path.Combine("story-runs", StoryRunRecord.FolderName(Commander, Run.Id, Run.PickedAt));
        var json = JsonSerializer.Serialize(Line, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        files.WriteText(Path.Combine(folder, StoryRunRecord.LinesFile), json + "\n");
        return files;
    }

    [Fact]
    public async Task ItIsSynthesisedOnceAndItsClipKeptInTheRecord()
    {
        var rig = Build();

        var spoken = await rig.Voicing.SpeakAsync(Run, Line);

        Assert.NotNull(spoken.Clip);
        Assert.Equal([(Line.Body, "narrator-voice")], rig.Provider.Asked);

        var kept = Assert.Single(rig.Record.Read(Commander, Run));
        Assert.NotNull(kept.Clip);
        Assert.Equal("narrator-voice", kept.Voice?.Id);
        Assert.NotNull(rig.Record.Clip(Commander, Run, kept));
    }

    [Fact]
    public async Task ASecondPlayDoesNotSynthesiseAgainInThisSessionOrTheNext()
    {
        var rig = Build();

        await rig.Voicing.SpeakAsync(Run, Line);
        var again = await rig.Voicing.SpeakAsync(Run, Line);

        var next = Build(rig.Files);
        var later = await next.Voicing.SpeakAsync(Run, Line);

        Assert.NotNull(again.Clip);
        Assert.NotNull(later.Clip);
        Assert.Single(rig.Provider.Asked);
        Assert.Empty(next.Provider.Asked);
    }

    [Fact]
    public async Task TwoPlaysAtOnceSynthesiseOnce()
    {
        var rig = Build();

        var both = await Task.WhenAll(rig.Voicing.SpeakAsync(Run, Line), rig.Voicing.SpeakAsync(Run, Line));

        Assert.All(both, spoken => Assert.NotNull(spoken.Clip));
        Assert.Single(rig.Provider.Asked);
    }

    [Fact]
    public async Task ALineWhoseSpeakerIsInAForgottenCustomVoiceIsHeldNotSynthesised()
    {
        var rig = Build(pinned: new PinnedVoice(StorySpeaker.Chatterbox, ForgottenId));

        var spoken = await rig.Voicing.SpeakAsync(Run, Line);

        Assert.Null(spoken.Clip);
        Assert.Equal(StoryLineHold.Forgotten, spoken.Hold);
        Assert.Empty(rig.Chatterbox.Asked);
        Assert.Empty(rig.Provider.Asked);
        Assert.Null(Assert.Single(rig.Record.Read(Commander, Run)).Clip);
    }

    [Fact]
    public async Task AFailingProviderHoldsTheLineWithItsError()
    {
        var rig = Build(fails: true);

        var spoken = await rig.Voicing.SpeakAsync(Run, Line);

        Assert.Null(spoken.Clip);
        Assert.Equal(StoryLineHold.Failed, spoken.Hold);
        Assert.Equal("HTTP 503 · EDGE", spoken.Detail);
        Assert.Null(Assert.Single(rig.Record.Read(Commander, Run)).Clip);
    }

    [Fact]
    public async Task AClipInTheCommandersOwnVoiceIsKeptProtected()
    {
        var rig = Build(pinned: new PinnedVoice(StorySpeaker.Chatterbox, OwnVoice.VoiceId));

        var spoken = await rig.Voicing.SpeakAsync(Run, Line);

        Assert.NotNull(spoken.Clip);
        Assert.Equal([(Line.Body, OwnVoice.VoiceId)], rig.Chatterbox.Asked);
        Assert.True(MessageClips.IsProtected(Assert.Single(rig.Record.Read(Commander, Run)).Clip!));
    }

    [Fact]
    public async Task AClipMadeInYourOwnVoiceIsNotPlayedOnceThatVoiceIsDeleted()
    {
        var deleted = false;
        var rig = Build(pinned: new PinnedVoice(StorySpeaker.Chatterbox, OwnVoice.VoiceId), forgotten: id => deleted && id == OwnVoice.VoiceId);

        Assert.NotNull((await rig.Voicing.SpeakAsync(Run, Line)).Clip);

        deleted = true;
        rig.Record.ForgetOwnVoice();
        var after = await rig.Voicing.SpeakAsync(Run, Line);

        Assert.Null(after.Clip);
        Assert.Equal(StoryLineHold.Forgotten, after.Hold);
        Assert.Single(rig.Chatterbox.Asked);
        Assert.Null(Assert.Single(rig.Record.Read(Commander, Run)).Clip);
    }

    /// <summary>A fake provider that keeps what it was asked to say and in which voice, or fails every request.</summary>
    private sealed class Recording(string id, bool fails) : ITtsProvider
    {
        public List<(string Text, string? Voice)> Asked { get; } = [];

        public string Id => id;

        public string Name => id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            if (fails)
            {
                throw new TtsException($"HTTP 503 · {id.ToUpperInvariant()}", fault: TtsFault.Unreachable);
            }

            lock (Asked)
            {
                Asked.Add((text, voice.VoiceId));
            }

            return Task.FromResult(new AudioClip(text, TestTone.Pcm(4_800), AudioFormat.Standard));
        }
    }
}
