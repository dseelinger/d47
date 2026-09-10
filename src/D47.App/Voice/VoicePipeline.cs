using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>One turn, made audible.</summary>
/// <param name="cues">
/// Asked for each time rather than held, because the library is rebuilt when the Commander drops a file
/// into <c>data/audio/</c> and a reference captured at startup would go on playing the set that existed
/// then (Phase 12, "Pick up dropped-in audio without a restart").
/// </param>
public sealed class VoicePipeline(
    AudioArbiter arbiter,
    Func<CueLibrary> cues,
    ILoggerFactory loggers)
{
    private readonly ILogger<VoicePipeline> _logger = loggers.CreateLogger<VoicePipeline>();

    private int _turnNumber;

    /// <summary>The state the loop is showing. Read off the thread that speaks callouts.</summary>
    private volatile LoopState _state = LoopState.Idle;

    /// <summary>Whether this turn was spoken aloud.</summary>
    private bool _spoke;

    /// <summary>
    /// Whether the Commander is mid-exchange — from the moment the microphone opens until the answer
    /// has been spoken and the loop has settled (#61).
    /// </summary>
    public bool Engaged => _state != LoopState.Idle;

    /// <summary>The provider aboard the ship, or null when no voice is configured.</summary>
    public ITtsProvider? Tts { get; set; }

    /// <summary>The client for one slot, since Phase 57 let each name a different provider.</summary>
    public Func<VoiceGroup, ITtsProvider?>? SpeakerFor { get; set; }

    /// <summary>Which client speaks for a slot.</summary>
    private ITtsProvider? Speaker(VoiceGroup group) => SpeakerFor?.Invoke(group) ?? Tts;

    public VoiceSelection Voice { get; set; } = VoiceSelection.Default;

    /// <summary>Who to name on the captions of the next turn, or null for the ship's AI (#201).</summary>
    public string? CaptionSpeaker { get; set; }

    /// <summary>
    /// An id to what the voice is called, or null where nothing can say (remediation.md 10, item 9).
    /// </summary>
    public Func<string?, string?>? VoiceName { get; set; }

    public bool CuesEnabled { get; set; } = true;

    public bool BedEnabled { get; set; } = true;

    public string? Bed { get; set; }

    /// <summary>Told what each sentence was rendered by, when something is recording (#164).</summary>
    public Action<SynthesisNote>? Synthesised { get; set; }

    /// <summary>Raised when synthesis failed, so availability can be flipped rather than handled.</summary>
    public event Action<string>? SynthesisFailed;

    /// <summary>Raised with a voice id the provider refused, so it can be written out of settings.</summary>
    public event Action<string>? VoiceRejected;

    /// <summary>Consumes a turn's events, making each one audible as it arrives.</summary>
    public async Task<TurnResult?> RunAsync(
        IAsyncEnumerable<TurnEvent> turn,
        Action<TurnEvent>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        var number = Interlocked.Increment(ref _turnNumber);
        var group = $"turn-{number}";

        // Whatever the previous turn had left to say is no longer the answer to anything.
        arbiter.DropGroup($"turn-{number - 1}");

        SpeechPipeline? speech = null;
        TurnResult? result = null;

        try
        {
            _spoke = false;
            EnterState(LoopState.Thinking);

            await foreach (var turnEvent in turn.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                onEvent?.Invoke(turnEvent);

                switch (turnEvent)
                {
                    case TurnEvent.TextDelta text:
                        // Created on the first delta rather than up front, so a turn that never speaks never
                        // opens a pipeline — and, more to the point, the bed stops the moment there are words
                        // rather than when the turn ends.
                        if (speech is null && Tts is { } provider)
                        {
                            _spoke = true;
                            speech = new SpeechPipeline(
                                arbiter,
                                provider,
                                Introduce(Voice),
                                group,
                                loggers.CreateLogger<SpeechPipeline>(),
                                speaker: "D47",
                                noted: Synthesised,
                                captionSpeaker: CaptionSpeaker);
                            speech.SynthesisFailed += OnSynthesisFailed;
                            speech.VoiceRejected += OnVoiceRejected;
                        }

                        arbiter.StopBed();
                        speech?.Push(text.Text);
                        break;

                    case TurnEvent.Retrying retry:
                        _logger.LogInformation(
                            "Turn is being retried ({Attempt}/{Of}) after {Wait}: {Because}",
                            retry.Attempt,
                            retry.Of,
                            retry.Wait,
                            retry.Because);
                        break;

                    case TurnEvent.Completed completed:
                        result = completed.Result;
                        break;
                }
            }

            if (speech is not null)
            {
                await speech.CompleteAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (speech is not null)
            {
                speech.SynthesisFailed -= OnSynthesisFailed;
                speech.VoiceRejected -= OnVoiceRejected;
                await speech.DisposeAsync().ConfigureAwait(false);
            }

            // The bed is dropped by entering any state that is not Thinking, so a turn that ends by throwing
            // still cannot leave it looping.
            var settled = result?.Outcome switch
            {
                TurnOutcome.Answered => LoopState.Answered,
                TurnOutcome.Unsure => LoopState.Unsure,
                TurnOutcome.Failed => LoopState.Failed,
                _ => cancellationToken.IsCancellationRequested ? LoopState.Idle : LoopState.Failed,
            };

            // Answered and spoken needs no chime.
            EnterState(settled, cue: !(settled == LoopState.Answered && _spoke));
        }

        return result;
    }

    /// <summary>Says something without a turn behind it.</summary>
    /// <param name="speaker">
    /// Who is talking, for the log line that records which voice said it.
    /// </param>
    public async Task AnnounceAsync(
        string text,
        AudioChannel channel = AudioChannel.Speech,
        VoiceSelection? voice = null,
        string group = "announcement",
        Func<AudioClip, AudioClip>? colour = null,
        string? speaker = null,
        bool captioned = true,
        VoiceGroup slot = VoiceGroup.Aboard,
        string? captionSpeaker = null)
    {
        if (Speaker(slot) is not { } provider)
        {
            return;
        }

        // The voice is a parameter rather than always the ship AI's, because Phase 11 has several things to
        // say that are not the ship AI speaking — a re-voiced in-game message, a carrier's tower, a crew
        // member.
        await using var speech = new SpeechPipeline(
            arbiter,
            provider,
            Introduce(voice ?? Voice),
            group,
            loggers.CreateLogger<SpeechPipeline>(),
            channel,
            colour,
            speaker,
            captioned,
            Synthesised,
            captionSpeaker);

        speech.SynthesisFailed += OnSynthesisFailed;
        speech.VoiceRejected += OnVoiceRejected;

        speech.Push(text);
        await speech.CompleteAsync().ConfigureAwait(false);
    }

    /// <summary>The group a persona's introduction or gap reaction is spoken in.</summary>
    private const string PersonaGroup = "persona-acknowledgement";

    /// <summary>A newly selected core, acknowledging that it has been picked.</summary>
    public async Task AcknowledgePersonaAsync(string text, VoiceSelection? voice = null)
    {
        arbiter.DropGroup(PersonaGroup);

        await AnnounceAsync(text, AudioChannel.Speech, voice, PersonaGroup, speaker: "D47").ConfigureAwait(false);
    }

    /// <summary>Speaks one unprompted callout (Phase 8).</summary>
    public async Task AnnounceAsync(Announcement announcement, VoiceSelection? voice = null)
    {
        if (announcement.Urgency == CalloutUrgency.Urgent)
        {
            arbiter.Silence();
        }

        // After the silence and before the speech, on the announcement's own channel, so the queue orders the
        // two: cue, then line.
        if (announcement.Cue is { } alert)
        {
            arbiter.Enqueue(new AudioRequest
            {
                Channel = announcement.Channel,
                Clip = cues().For(alert),

                // Written down as well as heard (#201).
                Caption = AlertCues.Caption(alert),
            });
        }

        _logger.LogDebug(
            "Speaking callout {Key} as {Role}", announcement.Key, announcement.Voice);

        // The role decides whether this is somebody in the ship or somebody transmitting to it.
        await AnnounceAsync(
                announcement.Text,
                announcement.Channel,
                voice,

                // The group a reply may drop this by.
                announcement.Group,
                colour: RadioVoice.Colours(announcement.Voice),

                // The sender where there is one and the role otherwise, which is the difference between "Ilse
                // Bruhn" and "Comms" in the log — and the reason for writing the voice down is being able
                // to tell two senders apart.
                speaker: announcement.Speaker is { Length: > 0 } named
                    ? named
                    : announcement.Voice.ToString(),

                // Anything already written onto the comms page is not also captioned.
                captioned: announcement.Transcript is null,

                // Which slot pays for it, and so which provider says it.
                slot: VoiceGroups.Of(announcement.Voice, announcement.CommsChannel),

                // And who to name on the caption when this is not d47 talking (#201).
                captionSpeaker: VoiceRoles.Called(announcement.Voice))
            .ConfigureAwait(false);
    }

    /// <summary>Raised as the loop moves.</summary>
    public event Action<LoopState>? StateEntered;

    public void EnterState(LoopState state) => EnterState(state, cue: true);

    /// <summary>Moves the loop, optionally without its cue.</summary>
    public void EnterState(LoopState state, bool cue)
    {
        // Invented chatter gets out of the way the moment the Commander starts talking, or a turn starts
        // without them having talked at all — cut mid-word if it is playing, dropped if it is queued.
        // Waiting for the answer is far too late: transcription and the model together run to several
        // seconds, and a four-line exchange finishes inside them. Callouts and relayed in-game comms are
        // in another group and are left where they are (#61).
        if (state is LoopState.Listening or LoopState.Thinking)
        {
            arbiter.DropGroup(SpokenGroup.InventedChatter);
        }

        arbiter.EnterState(state, cues(), Bed, CuesEnabled && cue, BedEnabled);
        _state = state;
        StateEntered?.Invoke(state);
    }

    /// <summary>Returns the loop to idle once nothing is audible any more.</summary>
    public void Settle(AudioActivity activity)
    {
        if (activity.Channel is not null || activity.BedPlaying)
        {
            return;
        }

        if (_state is LoopState.Idle or LoopState.Listening or LoopState.Transcribing or LoopState.Thinking)
        {
            return;
        }

        _state = LoopState.Idle;
        StateEntered?.Invoke(LoopState.Idle);
    }

    private void OnSynthesisFailed(string reason) => SynthesisFailed?.Invoke(reason);

    /// <summary>The same selection with the voice's name attached, where the host can say what it is.</summary>
    internal VoiceSelection Introduce(VoiceSelection voice) =>
        voice.Name is { Length: > 0 } || voice.VoiceId is not { Length: > 0 } id
            ? voice
            : voice with { Name = VoiceName?.Invoke(id) };

    private void OnVoiceRejected(string voiceId) => VoiceRejected?.Invoke(voiceId);
}
