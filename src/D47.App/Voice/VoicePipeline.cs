using D47.Core;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Speech;
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
    ILoggerFactory loggers,
    IWallClock? clock = null)
{
    private readonly ILogger<VoicePipeline> _logger = loggers.CreateLogger<VoicePipeline>();

    /// <summary>
    /// The thirty-second window shared by every voice, so the second pipeline of a turn knows what the
    /// first one just said (#196).
    /// </summary>
    private readonly SpokenAddress _address = new(clock ?? SystemWallClock.Instance);

    /// <summary>The Commander's name, so the address rule knows the surname to recognise.</summary>
    public string? CommanderName
    {
        set => _address.CommanderName = value;
    }

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

    /// <summary>The client for a provider id, whatever the slots are on, for a story line pinned to a cast member's voice.</summary>
    public Func<string, ITtsProvider?>? PinnedFor { get; set; }

    /// <summary>Told the StoryVoices key and the reason when a character's chosen voice fails and its pinned voice speaks instead.</summary>
    public Action<string, string>? CastVoiceFailed { get; set; }

    public VoiceSelection Voice { get; set; } = VoiceSelection.Default;

    /// <summary>Who to name on the captions of the next turn, or null for the ship's AI (#201).</summary>
    public string? CaptionSpeaker { get; set; }

    /// <summary>
    /// An id to what the voice is called, or null where nothing can say (remediation.md 10, item 9).
    /// </summary>
    public Func<string?, string?>? VoiceName { get; set; }

    public bool CuesEnabled { get; set; } = true;

    public bool BedEnabled { get; set; } = true;

    /// <summary>The ship AI's Guardian treatment for the settings in force, or null with every toggle off (#225).</summary>
    public Func<AudioClip, AudioClip>? GuardianColour { get; set; }

    /// <summary><see cref="GuardianColour"/> as a running filter, so the ship AI plays while it arrives.</summary>
    public Func<IPcmFilter>? GuardianRunning { get; set; }

    /// <summary>
    /// Who speaks the reply in progress. Guardian treatment reaches only <see cref="VoiceRole.ShipAi"/>, and a
    /// role heard over the air gets a radio link (#225).
    /// </summary>
    public VoiceRole SpeakingAs { get; set; } = VoiceRole.ShipAi;

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
        var signal = 1.0;

        try
        {
            _spoke = false;
            EnterState(LoopState.Thinking);

            await foreach (var turnEvent in turn.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                onEvent?.Invoke(turnEvent);

                switch (turnEvent)
                {
                    case TurnEvent.Addressed addressed:
                        signal = addressed.Signal;
                        break;

                    case TurnEvent.TextDelta text:
                        // Created on the first delta rather than up front, so a turn that never speaks never
                        // opens a pipeline — and, more to the point, the bed stops the moment there are words
                        // rather than when the turn ends.
                        if (speech is null && Speaker(VoiceGroups.Of(SpeakingAs)) is { } provider)
                        {
                            _spoke = true;

                            var role = SpeakingAs;
                            var colour = Colour(role, signal);

                            speech = new SpeechPipeline(
                                arbiter,
                                provider,
                                Introduce(Voice),
                                group,
                                loggers.CreateLogger<SpeechPipeline>(),
                                colour: colour,
                                speaker: "D47",
                                noted: Synthesised,
                                captionSpeaker: CaptionSpeaker,
                                address: _address,
                                guardianTreated: IsGuardianTreated(role, colour),
                                running: Running(role, colour, signal));
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
                TurnOutcome.Unsure or TurnOutcome.Truncated => LoopState.Unsure,
                TurnOutcome.Failed => LoopState.Failed,
                _ => cancellationToken.IsCancellationRequested ? LoopState.Idle : LoopState.Failed,
            };

            // Answered and spoken needs no chime.
            EnterState(settled, cue: !(settled == LoopState.Answered && _spoke));
        }

        return result;
    }

    /// <summary>Says something without a turn behind it, and returns what was queued to play.</summary>
    /// <param name="speaker">
    /// Who is talking, for the log line that records which voice said it.
    /// </param>
    /// <param name="role">
    /// Who this is, for choosing a treatment when <paramref name="colour"/> is not given: an
    /// over-the-air role gets a radio link, the ship AI gets its Guardian treatment where one is
    /// switched on, and every other role is unchanged (#225).
    /// </param>
    public async Task<SpokenClip?> AnnounceAsync(
        string text,
        AudioChannel channel = AudioChannel.Speech,
        VoiceSelection? voice = null,
        string group = "announcement",
        Func<AudioClip, AudioClip>? colour = null,
        string? speaker = null,
        bool captioned = true,
        VoiceGroup slot = VoiceGroup.Aboard,
        string? captionSpeaker = null,
        VoiceRole role = VoiceRole.ShipAi,
        bool overheard = false)
    {
        if (Speaker(slot) is not { } provider)
        {
            return null;
        }

        var applied = colour ?? Colour(role, overheard: overheard);
        var running = colour is null ? Running(role, applied, overheard: overheard) : null;

        return await SpeakAsync(provider, text, channel, voice, group, applied, running, speaker, captioned, captionSpeaker, role, pinned: false)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The client for a cast member's voice: for a voice the Commander chose, one that speaks a failed sentence in the
    /// story's pinned voice, or the pinned voice outright when the chosen provider has no client.
    /// </summary>
    private ITtsProvider? CastSpeaker(D47.Core.Stories.PinnedVoice pinned)
    {
        var client = PinnedFor?.Invoke(pinned.ProviderId);

        if (pinned.Fallback is not { } fallback || PinnedFor?.Invoke(fallback.ProviderId) is not { } story)
        {
            return client;
        }

        void Failed(string reason)
        {
            _logger.LogWarning("A story character's chosen {Provider} voice failed; its pinned voice speaks instead. {Reason}", pinned.ProviderId, reason);

            if (pinned.Key is { } key)
            {
                CastVoiceFailed?.Invoke(key, reason);
            }
        }

        if (client is null)
        {
            Failed($"d47 has no {pinned.ProviderId} voice.");
            return new D47.Core.Stories.FallingBackTtsProvider(story, story, fallback.VoiceId, _ => { }) { AlwaysFallBack = true };
        }

        return new D47.Core.Stories.FallingBackTtsProvider(client, story, fallback.VoiceId, Failed);
    }

    /// <summary>Speaks through <paramref name="provider"/> with the treatment already chosen, and returns what was queued.</summary>
    /// <param name="pinned">
    /// Whether the voice is a story cast member's rather than one from settings: a refusal or a failure is logged by the
    /// pipeline and neither written out of settings nor counted against the slot's provider.
    /// </param>
    private async Task<SpokenClip?> SpeakAsync(
        ITtsProvider provider,
        string text,
        AudioChannel channel,
        VoiceSelection? voice,
        string group,
        Func<AudioClip, AudioClip>? applied,
        Func<IPcmFilter>? running,
        string? speaker,
        bool captioned,
        string? captionSpeaker,
        VoiceRole role,
        bool pinned)
    {
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
            applied,
            speaker,
            captioned,
            Synthesised,
            captionSpeaker,
            _address,
            IsGuardianTreated(role, applied),
            keep: true,
            running: running);

        if (!pinned)
        {
            speech.SynthesisFailed += OnSynthesisFailed;
            speech.VoiceRejected += OnVoiceRejected;
        }

        speech.Push(text);
        await speech.CompleteAsync().ConfigureAwait(false);

        return speech.Kept;
    }

    /// <summary>The group a persona's introduction or gap reaction is spoken in.</summary>
    private const string PersonaGroup = "persona-acknowledgement";

    /// <summary>A newly selected core, acknowledging that it has been picked.</summary>
    public async Task AcknowledgePersonaAsync(string text, VoiceSelection? voice = null)
    {
        arbiter.DropGroup(PersonaGroup);

        await AnnounceAsync(text, AudioChannel.Speech, voice, PersonaGroup, speaker: "D47").ConfigureAwait(false);
    }

    /// <summary>Speaks one unprompted callout (Phase 8), and returns what was queued to play.</summary>
    public async Task<SpokenClip?> AnnounceAsync(Announcement announcement, VoiceSelection? voice = null)
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

        // A story's cast member, in the voice the story pinned and with the treatment the story gave them.
        if (announcement.Pinned is { } pinned)
        {
            if (CastSpeaker(pinned) is not { } client)
            {
                _logger.LogWarning("No {Provider} client to speak {Key} in its pinned voice", pinned.ProviderId, announcement.Key);
                return null;
            }

            return await SpeakAsync(
                    client,
                    announcement.Text,
                    announcement.Channel,
                    new VoiceSelection(pinned.VoiceId),
                    announcement.Group,
                    D47.Core.Stories.CastVoice.Treatment(pinned),
                    D47.Core.Stories.CastVoice.Running(pinned),
                    announcement.Speaker is { Length: > 0 } member ? member : announcement.Voice.ToString(),
                    announcement.Transcript is null,
                    announcement.Speaker,
                    announcement.Voice,
                    pinned: true)
                .ConfigureAwait(false);
        }

        // The role decides whether this is somebody in the ship or somebody transmitting to it.
        return await AnnounceAsync(
                announcement.Text,
                announcement.Channel,
                voice,

                // The group a reply may drop this by.
                announcement.Group,
                role: announcement.Voice,
                overheard: announcement.Overheard,

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
        // The arbiter calls back into Settle before these calls return, so the loop has to be showing the
        // state being entered first. Settle reading the state being left reopens the group just closed,
        // and a follow-up asked while the loop still sits in Answered goes unsuppressed (#61).
        _state = state;

        // Invented chatter gets out of the way the moment the Commander starts talking, or a turn starts
        // without them having talked at all — cut mid-word if it is playing, dropped if it is queued, and
        // refused until the loop settles, because the lines after it are synthesised while it is speaking
        // and would otherwise arrive behind the answer. Waiting for the answer is far too late:
        // transcription and the model together run to several seconds, and a four-line exchange finishes
        // inside them. Callouts and relayed in-game comms are in another group (#61).
        if (state is LoopState.Listening or LoopState.Thinking)
        {
            arbiter.CloseGroup(SpokenGroup.InventedChatter);
        }
        else if (state == LoopState.Idle)
        {
            arbiter.OpenGroup(SpokenGroup.InventedChatter);
        }

        arbiter.EnterState(state, cues(), CuesEnabled && cue, BedEnabled);

        // Settle may have moved the loop on while that ran.
        if (_state == state)
        {
            StateEntered?.Invoke(state);
        }
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
        arbiter.OpenGroup(SpokenGroup.InventedChatter);
        StateEntered?.Invoke(LoopState.Idle);
    }

    private void OnSynthesisFailed(string reason) => SynthesisFailed?.Invoke(reason);

    /// <summary>
    /// A radio link for an over-the-air role, the ship AI's Guardian treatment where one is switched
    /// on, and no treatment for every other role — Crew included (#225).
    /// </summary>
    private Func<AudioClip, AudioClip>? Colour(VoiceRole role, double signal = 1, bool overheard = false) =>
        RadioVoice.Colours(role, signal, overheard) ?? (role == VoiceRole.ShipAi ? GuardianColour : null);

    /// <summary>
    /// <see cref="Colour"/> as a running filter: the radio link for an over-the-air role, and
    /// <see cref="GuardianRunning"/> where the resolved colour is the ship AI's own treatment; otherwise null.
    /// </summary>
    private Func<IPcmFilter>? Running(VoiceRole role, Func<AudioClip, AudioClip>? colour, double signal = 1, bool overheard = false) =>
        RadioVoice.RunningColours(role, signal, overheard)
        ?? (role == VoiceRole.ShipAi && colour is not null && colour == GuardianColour ? GuardianRunning : null);

    /// <summary>Whether a resolved colour is the Guardian treatment rather than a radio link, for the log.</summary>
    private static bool IsGuardianTreated(VoiceRole role, Func<AudioClip, AudioClip>? colour) =>
        role == VoiceRole.ShipAi && colour is not null;

    /// <summary>The same selection with the voice's name attached, where the host can say what it is.</summary>
    internal VoiceSelection Introduce(VoiceSelection voice) =>
        voice.Name is { Length: > 0 } || voice.VoiceId is not { Length: > 0 } id
            ? voice
            : voice with { Name = VoiceName?.Invoke(id) };

    private void OnVoiceRejected(string voiceId) => VoiceRejected?.Invoke(voiceId);
}
