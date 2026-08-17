using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// One reply, spoken. Text deltas in, ordered speech on the arbiter out.
/// <para>
/// Synthesis runs ahead of playback and enqueueing stays in order, which is the point. Each
/// sentence starts synthesising the moment it closes, so by the time the first one has
/// finished playing the second is usually already rendered — but they still reach the queue
/// in the order they were written, because a reply delivered out of order is not a reply.
/// </para>
/// <para>
/// One of these per turn. The turn id is the arbiter group, so a new turn can drop the tail of
/// the previous one without disturbing an alert queued alongside it.
/// </para>
/// </summary>
public sealed class SpeechPipeline : IAsyncDisposable
{
    private readonly AudioArbiter _arbiter;
    private readonly ITtsProvider _tts;

    /// <summary>
    /// The voice in force. Not readonly, because a voice the provider refuses is dropped for
    /// the rest of the turn rather than sent again for every remaining sentence.
    /// </summary>
    private VoiceSelection _voice;
    private readonly string _group;
    private readonly ILogger _logger;

    private readonly SentenceSplitter _splitter = new();
    private readonly Channel<Task<Spoken?>> _rendered = Channel.CreateUnbounded<Task<Spoken?>>();

    /// <summary>
    /// Which channel the rendered sentences enter the arbiter on. Speech for a turn; Alert for
    /// a Phase 8 danger callout, which has to outrank whatever is being said rather than queue
    /// behind it — an alert that waits for the current sentence to finish is not an alert.
    /// </summary>
    private readonly AudioChannel _channel;

    /// <summary>
    /// What every rendered sentence is put through before it reaches the queue, or null for the
    /// voice as the provider sent it. <see cref="RadioVoice"/> is the one that exists: a sender
    /// who is not aboard the ship arrives over a link rather than from the next seat.
    /// <para>
    /// Applied here rather than in the arbiter because it is a property of who is speaking, and
    /// the arbiter knows only which channel a clip is on. Applied per sentence as it comes back
    /// from the provider rather than in the drain, so the arithmetic runs alongside the
    /// synthesis of the next sentence instead of in front of playback.
    /// </para>
    /// </summary>
    private readonly Func<AudioClip, AudioClip>? _colour;

    /// <summary>
    /// Who this is, for the log line — a sender's name, a role, or null when the caller has
    /// nothing more specific to say than the group already does.
    /// </summary>
    private readonly string? _speaker;

    /// <summary>
    /// Whether what is said here should also be read in the headset.
    /// <para>
    /// False for a re-voiced in-game message, which is already written on the comms page and
    /// does not need a second copy across the middle of the view — a station approach produces a
    /// steady stream of them, and captioning that is captioning traffic rather than the
    /// conversation (remediation.md, "NPC speech does not need captioning").
    /// </para>
    /// </summary>
    private readonly bool _captioned;

    private readonly CancellationTokenSource _abandon = new();
    private readonly Task _drain;

    private int _failures;

    /// <summary>Whether the voice has already been written down for this utterance.</summary>
    private int _recorded;

    private sealed record Spoken(string Text, AudioClip Clip);

    public SpeechPipeline(
        AudioArbiter arbiter,
        ITtsProvider tts,
        VoiceSelection voice,
        string group,
        ILogger logger,
        AudioChannel channel = AudioChannel.Speech,
        Func<AudioClip, AudioClip>? colour = null,
        string? speaker = null,
        bool captioned = true)
    {
        _arbiter = arbiter;
        _tts = tts;
        _voice = voice;
        _group = group;
        _logger = logger;
        _channel = channel;
        _colour = colour;
        _speaker = speaker;
        _captioned = captioned;

        // Shut up has to reach synthesis, not just the queue. Without this, a sentence still
        // rendering when the Commander says stop would arrive a moment later and start
        // speaking into the silence it was supposed to end (list.md Phase 5, "Shut up").
        _arbiter.Silenced += Abandon;

        _drain = DrainAsync();
    }

    /// <summary>
    /// Raised when the provider could not synthesise. The caller decides what that means —
    /// usually that the TTS capability is off until it works again, which is a state rather
    /// than a failure handler (list.md Phase 3, "Capabilities as state, not guard").
    /// </summary>
    public event Action<string>? SynthesisFailed;

    /// <summary>
    /// Raised with a voice id the provider refused, once per pipeline. Whoever chose it is
    /// expected to stop choosing it — the id is in settings, and a voice that fails once fails
    /// every sentence of every turn until something writes it out of there.
    /// </summary>
    public event Action<string>? VoiceRejected;

    /// <summary>How many sentences failed to render. Zero on a healthy turn.</summary>
    public int Failures => Volatile.Read(ref _failures);

    /// <summary>Feeds one model delta. Sentences that close as a result start rendering now.</summary>
    public void Push(string delta)
    {
        foreach (var sentence in _splitter.Push(delta))
        {
            Render(sentence);
        }
    }

    /// <summary>
    /// The stream ended. Renders the remainder and waits for everything already queued to
    /// reach the arbiter — not for it to finish playing, which is the arbiter's business.
    /// </summary>
    public async Task CompleteAsync()
    {
        if (_splitter.Flush() is { } tail)
        {
            Render(tail);
        }

        _rendered.Writer.TryComplete();
        await _drain.ConfigureAwait(false);
    }

    /// <summary>
    /// Stop. Cancels in-flight synthesis and drops anything not yet enqueued. Safe to call
    /// more than once, and safe to call from the hotkey path with a turn mid-flight — which
    /// is the only way it is ever called.
    /// </summary>
    public void Abandon()
    {
        _rendered.Writer.TryComplete();

        if (!_abandon.IsCancellationRequested)
        {
            _abandon.Cancel();
        }
    }

    private void Render(string sentence) =>
        _rendered.Writer.TryWrite(SynthesizeAsync(sentence));

    private async Task<Spoken?> SynthesizeAsync(string sentence)
    {
        try
        {
            var clip = await _tts
                .SynthesizeAsync(sentence, _voice, _abandon.Token)
                .ConfigureAwait(false);

            Record();

            return new Spoken(sentence, _colour is null ? clip : _colour(clip));
        }
        catch (OperationCanceledException)
        {
            // Abandoned. Not a failure — somebody asked for silence.
            return null;
        }
        catch (TtsException ex) when (ex.Fault == TtsFault.VoiceRejected && Forget() is { } refused)
        {
            // The one failure d47 can do something about. A voice the provider will not accept
            // is not a bad sentence, it is a bad setting, and sending it again for every
            // remaining sentence turns one wrong value into a silent turn.
            _logger.LogWarning(
                "{Voice} was refused; dropping it and letting the provider choose. {Because}",
                refused,
                ex.Message);

            VoiceRejected?.Invoke(refused);

            return await SpeakWithoutAVoiceAsync(sentence).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failures);
            _logger.LogWarning(ex, "Could not synthesise a sentence; it will not be spoken");
            SynthesisFailed?.Invoke(ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Writes down which voice this was actually spoken in
    /// (remediation.md, "In the log file, record which voice was used").
    /// <para>
    /// <b>Here because everything audible converges here.</b> A turn's reply, a Phase 8 callout,
    /// a re-voiced in-game message, a crew member and a core's own introduction are five callers
    /// with five different reasons, and all five build one of these — so this is one line rather
    /// than five that would drift apart.
    /// </para>
    /// <para>
    /// Once per utterance, not once per sentence: a six-sentence reply is one voice, and six
    /// identical lines would bury the one that differs. On the first sentence that actually
    /// rendered, so a voice the provider refused is not recorded as having spoken.
    /// </para>
    /// <para>
    /// The id rather than a name, because that is what this layer holds and what the settings
    /// file and every other log line say. Edge's read as words; ElevenLabs' are opaque, and the
    /// point of writing them down is to be able to tell two senders apart.
    /// </para>
    /// </summary>
    private void Record()
    {
        if (Interlocked.Exchange(ref _recorded, 1) == 1)
        {
            return;
        }

        _logger.LogInformation(
            "Spoken by {Who} in {Voice} ({Group}, {Link})",
            _speaker ?? "D47",
            _voice.VoiceId is { Length: > 0 } id ? id : "the provider's own voice",
            _group,

            // Which side of the hull this came from, because "it did not sound like a radio" is
            // otherwise a report with nothing to check it against.
            _colour is null ? "in the room" : "over the air");
    }

    /// <summary>
    /// Drops the voice, and answers what it was — or null if it has already been dropped, which
    /// is what stops several sentences failing at once from each raising the same complaint.
    /// </summary>
    private string? Forget()
    {
        var refused = Interlocked.Exchange(ref _voice, _voice with { VoiceId = null }).VoiceId;

        return string.IsNullOrEmpty(refused) ? null : refused;
    }

    /// <summary>
    /// The same sentence again with no voice named, so the provider falls back to its own.
    /// <para>
    /// Edge has a default and will speak. ElevenLabs has none — an account's voice list is its
    /// own, so it refuses rather than guessing — and answers with "no voice has been chosen",
    /// which is the sentence the Commander actually needs and not the one they were getting.
    /// Either way the failure stops being permanent.
    /// </para>
    /// </summary>
    private async Task<Spoken?> SpeakWithoutAVoiceAsync(string sentence)
    {
        try
        {
            var clip = await _tts
                .SynthesizeAsync(sentence, _voice, _abandon.Token)
                .ConfigureAwait(false);

            Record();

            return new Spoken(sentence, _colour is null ? clip : _colour(clip));
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failures);
            _logger.LogWarning(ex, "Could not synthesise a sentence; it will not be spoken");
            SynthesisFailed?.Invoke(ex.Message);
            return null;
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            await foreach (var pending in _rendered.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // Awaiting in read order is what keeps the reply in order while still letting
                // every sentence render concurrently.
                var spoken = await pending.ConfigureAwait(false);

                if (spoken is null || _abandon.IsCancellationRequested)
                {
                    continue;
                }

                _arbiter.Enqueue(new AudioRequest
                {
                    Channel = _channel,
                    Clip = spoken.Clip,
                    Group = _group,
                    Caption = _captioned ? spoken.Text : null,
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Abandoned mid-drain.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _arbiter.Silenced -= Abandon;
        Abandon();

        try
        {
            await _drain.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when disposal is what abandoned it.
        }

        _abandon.Dispose();
    }
}
