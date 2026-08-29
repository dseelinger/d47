using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// One turn, made audible. Drives the loop-state cues, the thinking bed and the spoken reply
/// off the turn's own event stream, so there is exactly one description of what a turn sounds
/// like (Phase 5).
/// <para>
/// It lives in the App rather than in Core because it is composition: Core owns the arbiter,
/// the splitter and the turn loop separately, and this is the wiring that says how they run
/// together. Nothing here decides policy — a cue's timing, the bed's lifetime and the queue's
/// ordering are all properties of <see cref="AudioArbiter"/>.
/// </para>
/// </summary>
/// <param name="cues">
/// Asked for each time rather than held, because the library is rebuilt when the Commander drops
/// a file into <c>data/audio/</c> and a reference captured at startup would go on playing the set
/// that existed then (Phase 12, "Pick up dropped-in audio without a restart").
/// </param>
public sealed class VoicePipeline(
    AudioArbiter arbiter,
    Func<CueLibrary> cues,
    ILoggerFactory loggers)
{
    private readonly ILogger<VoicePipeline> _logger = loggers.CreateLogger<VoicePipeline>();

    private int _turnNumber;

    /// <summary>
    /// The state the loop is showing. Held here so the settle back to idle knows whether there
    /// is anything to settle from - a state that is already idle must not re-announce itself.
    /// </summary>
    private LoopState _state = LoopState.Idle;

    /// <summary>
    /// Whether this turn was spoken aloud. An answer that was just said out loud does not also
    /// need a chime saying an answer happened, and the chime lands after the speech because the
    /// arbiter is a queue - so what the Commander hears is the reply, a pause, and then a noise
    /// about the reply they already heard.
    /// </summary>
    private bool _spoke;

    /// <summary>
    /// The provider aboard the ship, or null when no voice is configured. Swapped on a settings
    /// change, and the one a turn's reply is spoken through — a reply is the ship's AI talking,
    /// which is <see cref="VoiceGroup.Aboard"/> by construction.
    /// </summary>
    public ITtsProvider? Tts { get; set; }

    /// <summary>
    /// The client for one slot, since Phase 57 let each name a different provider. Null, or a
    /// function answering null, falls back to <see cref="Tts"/> — which is what every surface
    /// that has not been told about slots gets, and is exactly what d47 did before them.
    /// <para>
    /// A lookup rather than a map so the host can answer from whatever it holds, and so this
    /// stays a seam rather than a copy of the host's wiring kept in step by hand.
    /// </para>
    /// </summary>
    public Func<VoiceGroup, ITtsProvider?>? SpeakerFor { get; set; }

    /// <summary>Which client speaks for a slot. One place, so no caller has to remember the fallback.</summary>
    private ITtsProvider? Speaker(VoiceGroup group) => SpeakerFor?.Invoke(group) ?? Tts;

    public VoiceSelection Voice { get; set; } = VoiceSelection.Default;

    /// <summary>
    /// An id to what the voice is called, or null where nothing can say (remediation.md 10,
    /// item 9). Set by the host, which is what holds the provider's voice list — Core has no
    /// catalogue and no way to get one.
    /// </summary>
    public Func<string?, string?>? VoiceName { get; set; }

    public bool CuesEnabled { get; set; } = true;

    public bool BedEnabled { get; set; } = true;

    public string? Bed { get; set; }

    /// <summary>
    /// Told what each sentence was rendered by, when something is recording
    /// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>). Null on every ordinary
    /// run, and every pipeline this one opens is wired to it — a reply, a callout, a crew line
    /// and a re-voiced message are all things a Commander might be reviewing afterwards.
    /// </summary>
    public Action<SynthesisNote>? Synthesised { get; set; }

    /// <summary>Raised when synthesis failed, so availability can be flipped rather than handled.</summary>
    public event Action<string>? SynthesisFailed;

    /// <summary>
    /// Raised with a voice id the provider refused, so it can be written out of settings. Every
    /// pipeline this one opens is wired to it, including the one-off lines: a refused voice is a
    /// stored value, and which sentence happened to hit it first says nothing about it.
    /// </summary>
    public event Action<string>? VoiceRejected;

    /// <summary>
    /// Consumes a turn's events, making each one audible as it arrives. Returns the completed
    /// result so the caller does not have to watch the stream twice.
    /// </summary>
    public async Task<TurnResult?> RunAsync(
        IAsyncEnumerable<TurnEvent> turn,
        Action<TurnEvent>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        var number = Interlocked.Increment(ref _turnNumber);
        var group = $"turn-{number}";

        // Whatever the previous turn had left to say is no longer the answer to anything.
        // Scoped to that turn's group so an alert queued alongside it is untouched. Both
        // numbers come from the same local, so two turns starting at once cannot have one of
        // them drop the other's group.
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
                        // Created on the first delta rather than up front, so a turn that
                        // never speaks never opens a pipeline — and, more to the point, the
                        // bed stops the moment there are words rather than when the turn ends.
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
                                noted: Synthesised);
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

            // The bed is dropped by entering any state that is not Thinking, so a turn that
            // ends by throwing still cannot leave it looping.
            //
            // A cancelled turn lands on Idle rather than Failed: nothing went wrong, the
            // Commander called it off, and a failure cue would be d47 complaining about being
            // told to stop.
            var settled = result?.Outcome switch
            {
                TurnOutcome.Answered => LoopState.Answered,
                TurnOutcome.Unsure => LoopState.Unsure,
                TurnOutcome.Failed => LoopState.Failed,
                _ => cancellationToken.IsCancellationRequested ? LoopState.Idle : LoopState.Failed,
            };

            // Answered and spoken needs no chime. Unsure and Failed keep theirs: those say
            // something the reply itself did not.
            EnterState(settled, cue: !(settled == LoopState.Answered && _spoke));
        }

        return result;
    }

    /// <summary>
    /// Says something without a turn behind it. Used for the startup warning when the model is
    /// misconfigured — silence there is indistinguishable from a model with nothing to say
    /// (Phase 5) — and for every Phase 8 callout.
    /// </summary>
    /// <param name="speaker">
    /// Who is talking, for the log line that records which voice said it. Null leaves the group
    /// to say what it can.
    /// </param>
    public async Task AnnounceAsync(
        string text,
        AudioChannel channel = AudioChannel.Speech,
        VoiceSelection? voice = null,
        string group = "announcement",
        Func<AudioClip, AudioClip>? colour = null,
        string? speaker = null,
        bool captioned = true,
        VoiceGroup slot = VoiceGroup.Aboard)
    {
        if (Speaker(slot) is not { } provider)
        {
            return;
        }

        // The voice is a parameter rather than always the ship AI's, because Phase 11 has
        // several things to say that are not the ship AI speaking — a re-voiced in-game
        // message, a carrier's tower, a crew member. They still go through this one path and
        // this one arbiter: separate paths per voice are how a line gets spoken in the wrong
        // one (architecture.md D7).
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
            Synthesised);

        speech.SynthesisFailed += OnSynthesisFailed;
        speech.VoiceRejected += OnVoiceRejected;

        speech.Push(text);
        await speech.CompleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The group a persona's introduction or gap reaction is spoken in. Its own group so a
    /// second switch can drop the first acknowledgement mid-word — which is the requirement:
    /// "if it changes before its acknowledgement has completed speaking, it stops and the next
    /// one starts" (Phase 11).
    /// </summary>
    private const string PersonaGroup = "persona-acknowledgement";

    /// <summary>
    /// A newly selected core, acknowledging that it has been picked.
    /// <para>
    /// Not urgent, so it does not silence the queue — a callout already in flight outranks a
    /// companion saying hello. It does supersede the <em>previous</em> acknowledgement, and
    /// like all speech the Commander can cut it off outright.
    /// </para>
    /// </summary>
    public async Task AcknowledgePersonaAsync(string text, VoiceSelection? voice = null)
    {
        arbiter.DropGroup(PersonaGroup);

        await AnnounceAsync(text, AudioChannel.Speech, voice, PersonaGroup, speaker: "D47").ConfigureAwait(false);
    }

    /// <summary>
    /// Speaks one unprompted callout (Phase 8).
    /// <para>
    /// An urgent one silences the queue first rather than joining it. That is the difference
    /// between a warning and a remark: an interdiction announced after d47 finishes reading out
    /// a station's commodity list has arrived after the interdiction. Routine callouts queue
    /// normally and wait their turn.
    /// </para>
    /// </summary>
    public async Task AnnounceAsync(Announcement announcement, VoiceSelection? voice = null)
    {
        if (announcement.Urgency == CalloutUrgency.Urgent)
        {
            arbiter.Silence();
        }

        // After the silence and before the speech, on the announcement's own channel, so the
        // queue orders the two: cue, then line. It is deliberately not gated on CuesEnabled —
        // that row is about the loop states marking a turn, and a Commander who finds those
        // chatty has not asked to lose the mark on a warning. Switching a warning off is what
        // the warning's own row is for.
        if (announcement.Cue is { } alert)
        {
            arbiter.Enqueue(new AudioRequest
            {
                Channel = announcement.Channel,
                Clip = cues().For(alert),
            });
        }

        _logger.LogDebug(
            "Speaking callout {Key} as {Role}", announcement.Key, announcement.Voice);

        // The role decides whether this is somebody in the ship or somebody transmitting to it.
        // Resolved here, at the one point where a role and a synthesiser meet, rather than by
        // each callout — a callout knows whose line it is, and nothing more than that should be
        // asked of it (see RadioVoice).
        await AnnounceAsync(
                announcement.Text,
                announcement.Channel,
                voice,
                colour: RadioVoice.Colours(announcement.Voice),

                // The sender where there is one and the role otherwise, which is the difference
                // between "Ilse Bruhn" and "Comms" in the log — and the whole point of writing
                // the voice down is being able to tell two senders apart.
                speaker: announcement.Speaker is { Length: > 0 } named
                    ? named
                    : announcement.Voice.ToString(),

                // Anything already written onto the comms page is not also captioned. That is the
                // same property rather than a second list: Transcript is non-null exactly for a
                // re-voiced in-game message, which is the traffic the request is about
                // (remediation.md, "NPC speech does not need captioning").
                captioned: announcement.Transcript is null,

                // Which slot pays for it, and so which provider says it. Resolved here for the
                // same reason the radio treatment is: this is the one point where a role and a
                // synthesiser meet, and a callout knows whose line it is and nothing more
                // (Phase 57).
                slot: VoiceGroups.Of(announcement.Voice, announcement.CommsChannel))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Raised as the loop moves. The cues have marked these states audibly since Phase 5; this
    /// is the same states, for the surfaces that show a face (Phase 11).
    /// </summary>
    public event Action<LoopState>? StateEntered;

    public void EnterState(LoopState state) => EnterState(state, cue: true);

    /// <summary>
    /// Moves the loop, optionally without its cue.
    /// <para>
    /// The cue is suppressible rather than removed because it is only redundant when the same
    /// news arrived by voice a moment earlier. With speech switched off, the chime is the only
    /// thing that says the turn finished, and Phase 5 (#20) asks for one per state.
    /// </para>
    /// </summary>
    public void EnterState(LoopState state, bool cue)
    {
        arbiter.EnterState(state, cues(), Bed, CuesEnabled && cue, BedEnabled);
        _state = state;
        StateEntered?.Invoke(state);
    }

    /// <summary>
    /// Returns the loop to idle once nothing is audible any more.
    /// <para>
    /// The face used to stop on the tick and stay there, because <see cref="LoopState.Answered"/>
    /// was terminal and nothing ever left it - so "the last turn succeeded" and "d47 is doing
    /// something right now" looked identical, which is the one distinction a loop-state icon
    /// exists to make. Settling on the arbiter going quiet rather than on the turn returning is
    /// deliberate: the turn returns while the reply is still being spoken.
    /// </para>
    /// <para>
    /// No cue, because arriving at rest is not news.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The same selection with the voice's name attached, where the host can say what it is. It
    /// is never sent to a provider — the id is still the value — and exists so that a log line
    /// naming a voice can be read by a person rather than matched against a settings file
    /// (remediation.md 10, item 9).
    /// </summary>
    internal VoiceSelection Introduce(VoiceSelection voice) =>
        voice.Name is { Length: > 0 } || voice.VoiceId is not { Length: > 0 } id
            ? voice
            : voice with { Name = VoiceName?.Invoke(id) };

    private void OnVoiceRejected(string voiceId) => VoiceRejected?.Invoke(voiceId);
}
