using D47.Core.Audio;
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
            EnterState(result?.Outcome switch
            {
                TurnOutcome.Answered => LoopState.Answered,
                TurnOutcome.Unsure => LoopState.Unsure,
                TurnOutcome.Failed => LoopState.Failed,
                _ => LoopState.Failed,
            });
        }

        return result;
    }

    /// <summary>
    /// Says something without a turn behind it. Used for the startup warning when the model is
    /// misconfigured — silence there is indistinguishable from a model with nothing to say
    /// (list.md Phase 5).
    /// </summary>
    public async Task AnnounceAsync(string text)
    {
        if (Tts is not { } provider)
        {
            return;
        }

        await using var speech = new SpeechPipeline(
            arbiter, provider, Voice, "announcement", loggers.CreateLogger<SpeechPipeline>());

        speech.Push(text);
        await speech.CompleteAsync().ConfigureAwait(false);
    }

    public void EnterState(LoopState state) =>
        arbiter.EnterState(state, cues, Bed, CuesEnabled, BedEnabled);

    private void OnSynthesisFailed(string reason) => SynthesisFailed?.Invoke(reason);
}
