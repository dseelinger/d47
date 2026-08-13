using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// One turn, made audible. Drives the loop-state cues, the thinking bed and the spoken reply
/// off the turn's own event stream, so there is exactly one description of what a turn sounds
/// like (list.md Phase 5).
/// <para>
/// It lives in the App rather than in Core because it is composition: Core owns the arbiter,
/// the splitter and the turn loop separately, and this is the wiring that says how they run
/// together. Nothing here decides policy — a cue's timing, the bed's lifetime and the queue's
/// ordering are all properties of <see cref="AudioArbiter"/>.
/// </para>
/// </summary>
public sealed class VoicePipeline(
    AudioArbiter arbiter,
    CueLibrary cues,
    ILoggerFactory loggers)
{
    private readonly ILogger<VoicePipeline> _logger = loggers.CreateLogger<VoicePipeline>();

    private int _turnNumber;

    /// <summary>The provider, or null when no voice is configured. Swapped on a settings change.</summary>
    public ITtsProvider? Tts { get; set; }

    public VoiceSelection Voice { get; set; } = VoiceSelection.Default;

    public bool CuesEnabled { get; set; } = true;

    public bool BedEnabled { get; set; } = true;

    public string? Bed { get; set; }

    /// <summary>Raised when synthesis failed, so availability can be flipped rather than handled.</summary>
    public event Action<string>? SynthesisFailed;

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
                            speech = new SpeechPipeline(
                                arbiter, provider, Voice, group, loggers.CreateLogger<SpeechPipeline>());
                            speech.SynthesisFailed += OnSynthesisFailed;
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
                await speech.DisposeAsync().ConfigureAwait(false);
            }

            // The bed is dropped by entering any state that is not Thinking, so a turn that
            // ends by throwing still cannot leave it looping.
            //
            // A cancelled turn lands on Idle rather than Failed: nothing went wrong, the
            // Commander called it off, and a failure cue would be d47 complaining about being
            // told to stop.
            EnterState(result?.Outcome switch
            {
                TurnOutcome.Answered => LoopState.Answered,
                TurnOutcome.Unsure => LoopState.Unsure,
                TurnOutcome.Failed => LoopState.Failed,
                _ => cancellationToken.IsCancellationRequested ? LoopState.Idle : LoopState.Failed,
            });
        }

        return result;
    }

    /// <summary>
    /// Says something without a turn behind it. Used for the startup warning when the model is
    /// misconfigured — silence there is indistinguishable from a model with nothing to say
    /// (list.md Phase 5) — and for every Phase 8 callout.
    /// </summary>
    public async Task AnnounceAsync(
        string text,
        AudioChannel channel = AudioChannel.Speech,
        VoiceSelection? voice = null,
        string group = "announcement")
    {
        if (Tts is not { } provider)
        {
            return;
        }

        // The voice is a parameter rather than always the ship AI's, because Phase 11 has
        // several things to say that are not the ship AI speaking — a re-voiced in-game
        // message, a carrier's tower, a crew member. They still go through this one path and
        // this one arbiter: separate paths per voice are how a line gets spoken in the wrong
        // one (architecture.md D7).
        await using var speech = new SpeechPipeline(
            arbiter, provider, voice ?? Voice, group, loggers.CreateLogger<SpeechPipeline>(), channel);

        speech.Push(text);
        await speech.CompleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The group a persona's introduction or gap reaction is spoken in. Its own group so a
    /// second switch can drop the first acknowledgement mid-word — which is the requirement:
    /// "if it changes before its acknowledgement has completed speaking, it stops and the next
    /// one starts" (list.md Phase 11).
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

        await AnnounceAsync(text, AudioChannel.Speech, voice, PersonaGroup).ConfigureAwait(false);
    }

    /// <summary>
    /// Speaks one unprompted callout (list.md Phase 8).
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

        _logger.LogDebug(
            "Speaking callout {Key} as {Role}", announcement.Key, announcement.Voice);

        await AnnounceAsync(announcement.Text, announcement.Channel, voice).ConfigureAwait(false);
    }

    public void EnterState(LoopState state) =>
        arbiter.EnterState(state, cues, Bed, CuesEnabled, BedEnabled);

    private void OnSynthesisFailed(string reason) => SynthesisFailed?.Invoke(reason);
}
