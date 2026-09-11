using System.Threading.Channels;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>What one sentence was rendered by, and how long it took (#164).</summary>
/// <param name="Phonemes">
/// The phoneme string the phonemiser emitted, for a provider that speaks phonemes rather than text.
/// </param>
public sealed record SynthesisNote(
    string Text,
    string Provider,
    string? Voice,
    string? Phonemes,
    TimeSpan Elapsed);

/// <summary>One reply, spoken.</summary>
public sealed class SpeechPipeline : IAsyncDisposable
{
    private readonly AudioArbiter _arbiter;
    private readonly ITtsProvider _tts;

    /// <summary>The voice in force.</summary>
    private VoiceSelection _voice;
    private readonly string _group;
    private readonly ILogger _logger;

    private readonly SentenceSplitter _splitter = new();
    private readonly Channel<Task<Spoken?>> _rendered = Channel.CreateUnbounded<Task<Spoken?>>();

    /// <summary>
    /// Closed sentences waiting for the rest of their group, for a provider that asked to be handed
    /// more than one at a time (<see cref="ITtsProvider.GroupsSentencesUpTo"/>).
    /// </summary>
    private readonly List<string> _gathering = [];

    /// <summary>How long <see cref="_gathering"/> would be once joined.</summary>
    private int _gathered;

    /// <summary>Whether a group has gone for rendering yet.</summary>
    private bool _spoke;

    /// <summary>Which channel the rendered sentences enter the arbiter on.</summary>
    private readonly AudioChannel _channel;

    /// <summary>
    /// What every rendered sentence is put through before it reaches the queue, or null for the voice
    /// as the provider sent it.
    /// </summary>
    private readonly Func<AudioClip, AudioClip>? _colour;

    /// <summary>
    /// Who this is, for the log line — a sender's name, a role, or null when the caller has nothing
    /// more specific to say than the group already does.
    /// </summary>
    private readonly string? _speaker;

    /// <summary>Whether what is said here should also be read in the headset.</summary>
    private readonly bool _captioned;

    /// <summary>
    /// Who to name on the caption, or null for the speaker a caption band is already understood to
    /// belong to (#201).
    /// </summary>
    private readonly string? _captionSpeaker;

    /// <summary>Whether the speaker ID has already gone out for this utterance.</summary>
    private int _attributed;

    /// <summary>Told what each sentence was rendered by, or null when nobody is recording.</summary>
    private readonly Action<SynthesisNote>? _noted;

    private readonly CancellationTokenSource _abandon = new();
    private readonly Task _drain;

    private int _failures;

    /// <summary>Whether the voice has already been written down for this utterance.</summary>
    private int _recorded;

    /// <summary><param name="Text"> The written form: no delivery direction in it, ever.</summary>
    /// <param name="Text">The written form: no delivery direction in it, ever.</param>
    /// <param name="Directed">
    /// The same words with the direction still in place, where any was written and the provider
    /// performs it.
    /// </param>
    private sealed record Spoken(string Text, string Directed, AudioClip Clip);

    public SpeechPipeline(
        AudioArbiter arbiter,
        ITtsProvider tts,
        VoiceSelection voice,
        string group,
        ILogger logger,
        AudioChannel channel = AudioChannel.Speech,
        Func<AudioClip, AudioClip>? colour = null,
        string? speaker = null,
        bool captioned = true,

        // Appended rather than slotted in beside the parameters it most resembles: every caller here passes
        // positionally, so a parameter added in the middle silently rebinds every argument after it
        // (remediation.md 11, item 9).
        Action<SynthesisNote>? noted = null,
        string? captionSpeaker = null)
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
        _noted = noted;
        _captionSpeaker = captionSpeaker;

        // Shut up has to reach synthesis, not just the queue.
        _arbiter.Silenced += Abandon;

        _drain = DrainAsync();
    }

    /// <summary>Raised when the provider could not synthesise.</summary>
    public event Action<string>? SynthesisFailed;

    /// <summary>Raised with a voice id the provider refused, once per pipeline.</summary>
    public event Action<string>? VoiceRejected;

    /// <summary>How many sentences failed to render.</summary>
    public int Failures => Volatile.Read(ref _failures);

    /// <summary>Feeds one model delta.</summary>
    public void Push(string delta)
    {
        foreach (var sentence in _splitter.Push(delta))
        {
            Group(sentence);
        }

        // The first group of a turn goes with whatever has arrived, however little. That is the Phase
        // 5 latency win and it is not negotiable: waiting for a group to fill would put a whole group's worth
        // of streaming in front of the first sound.
        if (!_spoke)
        {
            Gathered();
        }
    }

    /// <summary>One closed sentence, either rendered on its own or added to the group being gathered.</summary>
    private void Group(string sentence)
    {
        if (_tts.GroupsSentencesUpTo is var budget && budget <= 0)
        {
            Render(sentence);
            return;
        }

        // A sentence that will not fit closes the group in front of it rather than being cut, and one longer
        // than the whole budget is its own group.
        if (_gathering.Count > 0 && _gathered + 1 + sentence.Length > budget)
        {
            Gathered();
        }

        _gathered += (_gathering.Count == 0 ? 0 : 1) + sentence.Length;
        _gathering.Add(sentence);

        if (_gathered >= budget)
        {
            Gathered();
        }
    }

    /// <summary>Sends the gathered group to be rendered, if there is one.</summary>
    private void Gathered()
    {
        if (_gathering.Count == 0)
        {
            return;
        }

        Render(string.Join(' ', _gathering));

        _gathering.Clear();
        _gathered = 0;
        _spoke = true;
    }

    /// <summary>The stream ended.</summary>
    public async Task CompleteAsync()
    {
        if (_splitter.Flush() is { } tail)
        {
            Group(tail);
        }

        Gathered();

        _rendered.Writer.TryComplete();
        await _drain.ConfigureAwait(false);
    }

    /// <summary>Stop.</summary>
    public void Abandon()
    {
        _rendered.Writer.TryComplete();

        if (!_abandon.IsCancellationRequested)
        {
            _abandon.Cancel();
        }
    }

    /// <summary>One sentence, in both of its forms.</summary>
    private void Render(string sentence)
    {
        // Markdown first, and for the whole sentence rather than the spoken half alone: a model writes
        // **bold** without being asked, a voice reads the asterisks aloud, and a caption or a "said:" line
        // carrying them is markup on a surface that renders none.
        var plain = PlainSpeech.Strip(sentence);

        // Delivery direction parts company from the words here, and this is the only place it could: the
        // written form goes to the caption and the panel, the spoken form goes on the wire, and only one of
        // the two may carry [sighs].
        var written = AudioTags.Strip(plain);

        if (written.Length == 0)
        {
            return;
        }

        var directed = AudioTags.For(plain, _tts.ReadsAudioTags);

        _rendered.Writer.TryWrite(
            SynthesizeAsync(
                written, SpokenUnits.Rewrite(SpokenDesignations.Rewrite(directed)), directed));
    }

    /// <summary>
    /// <param name="directed"> The sentence with its delivery direction still in it, for the log.
    /// </summary>
    /// <param name="directed">
    /// The sentence with its delivery direction still in it, for the log.
    /// </param>
    private async Task<Spoken?> SynthesizeAsync(
        string sentence,
        string spoken,
        string directed)
    {
        try
        {
            var started = System.Diagnostics.Stopwatch.StartNew();

            var clip = await _tts
                .SynthesizeAsync(spoken, _voice, _abandon.Token)
                .ConfigureAwait(false);

            Record();
            Note(sentence, spoken, started.Elapsed);

            return new Spoken(sentence, directed, _colour is null ? clip : _colour(clip));
        }
        catch (OperationCanceledException)
        {
            // Abandoned.
            return null;
        }
        catch (TtsException ex) when (ex.Fault == TtsFault.VoiceRejected && Forget() is { } refused)
        {
            // The one failure d47 can do something about.
            _logger.LogWarning(
                "{Voice} was refused; dropping it and letting the provider choose. {Because}",
                refused,
                ex.Message);

            VoiceRejected?.Invoke(refused);

            return await SpeakWithoutAVoiceAsync(sentence, spoken, directed).ConfigureAwait(false);
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
    /// Writes down which voice this was actually spoken in (remediation.md, "In the log file, record
    /// which voice was used").
    /// </summary>
    private void Record()
    {
        if (Interlocked.Exchange(ref _recorded, 1) == 1)
        {
            return;
        }

        _logger.LogInformation(
            "Spoken by {Who} in {Voice} through {Provider} ({Group}, {Link})",
            _speaker ?? "D47",
            Named(_voice),

            // Which service said it, asked of the client rather than of settings, so it names what actually
            // spoke rather than what is currently selected (2026-08-28). A voice id means nothing without
            // it. Since Phase 57 six slots can name three providers at once, and this line read "Spoken
            // by Boe Dock in pFQStpMdprGFILRDrWR2" — an opaque id, with no way to tell whose it was or which
            // of the three was billed for it.
            _tts.Name,
            _group,

            // Which side of the hull this came from, because "it did not sound like a radio" is otherwise a
            // report with nothing to check it against.
            _colour is null ? "in the room" : "over the air");
    }

    /// <summary>
    /// Hands one sentence's rendering to whoever is recording, and does nothing at all when nobody is.
    /// </summary>
    private void Note(string sentence, string spoken, TimeSpan elapsed)
    {
        if (_noted is not { } noted)
        {
            return;
        }

        noted(new SynthesisNote(
            sentence,
            _tts.Name,
            Named(_voice),
            _tts.Phonemes(spoken, _voice),
            elapsed));
    }

    /// <summary>
    /// The voice as a person would say it: the name with the id beside it, the id alone when no name
    /// was resolved, and a plain sentence when there is no voice at all.
    /// </summary>
    private static string Named(VoiceSelection voice) => voice switch
    {
        { VoiceId: { Length: > 0 } id, Name: { Length: > 0 } name } => $"{name} ({id})",
        { VoiceId: { Length: > 0 } id } => id,
        _ => "the provider's own voice",
    };

    /// <summary>
    /// Drops the voice, and answers what it was — or null if it has already been dropped, which is what
    /// stops several sentences failing at once from each raising the same complaint.
    /// </summary>
    private string? Forget()
    {
        var refused = Interlocked.Exchange(ref _voice, _voice with { VoiceId = null }).VoiceId;

        return string.IsNullOrEmpty(refused) ? null : refused;
    }

    /// <summary>The same sentence again with no voice named, so the provider falls back to its own.</summary>
    private async Task<Spoken?> SpeakWithoutAVoiceAsync(
        string sentence,
        string spoken,
        string directed)
    {
        try
        {
            var started = System.Diagnostics.Stopwatch.StartNew();

            var clip = await _tts
                .SynthesizeAsync(spoken, _voice, _abandon.Token)
                .ConfigureAwait(false);

            Record();
            Note(sentence, spoken, started.Elapsed);

            return new Spoken(sentence, directed, _colour is null ? clip : _colour(clip));
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

    /// <summary>
    /// The caption text, with a speaker ID in front of it the first time somebody who is not the ship's
    /// AI says something (#201).
    /// </summary>
    private string Attributed(string text) =>
        _captionSpeaker is { Length: > 0 } named && Interlocked.Exchange(ref _attributed, 1) == 0
            ? $"[{named}] {text}"
            : text;

    private async Task DrainAsync()
    {
        var said = new System.Text.StringBuilder();

        try
        {
            await foreach (var pending in _rendered.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // Awaiting in read order is what keeps the reply in order while still letting every sentence
                // render concurrently.
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
                    Caption = _captioned ? Attributed(spoken.Text) : null,
                });

                // Accumulated here rather than where the text arrived, because this is the point a sentence
                // is actually going to be heard.
                said.Append(said.Length == 0 ? string.Empty : " ").Append(spoken.Directed);
            }
        }
        catch (OperationCanceledException)
        {
        // Abandoned mid-drain.
        }

        Said(said);
    }

    /// <summary>Writes down what was spoken, beside <see cref="Record"/>'s note of who spoke it.</summary>
    private void Said(System.Text.StringBuilder said)
    {
        if (said.Length == 0)
        {
            return;
        }

        _logger.LogInformation(
            "{Who} said: {Said}",
            _speaker ?? "D47",
            said.ToString());
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
