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
    private readonly VoiceSelection _voice;
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
    private readonly CancellationTokenSource _abandon = new();
    private readonly Task _drain;

    private int _failures;

    private sealed record Spoken(string Text, AudioClip Clip);

    public SpeechPipeline(
        AudioArbiter arbiter,
        ITtsProvider tts,
        VoiceSelection voice,
        string group,
        ILogger logger,
        AudioChannel channel = AudioChannel.Speech)
    {
        _arbiter = arbiter;
        _tts = tts;
        _voice = voice;
        _group = group;
        _logger = logger;
        _channel = channel;

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

            return new Spoken(sentence, clip);
        }
        catch (OperationCanceledException)
        {
            // Abandoned. Not a failure — somebody asked for silence.
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
                    Caption = spoken.Text,
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
