using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Debrief;

/// <summary>One session's debrief: what was said, the signals, the latched directions and the rewording (#162).</summary>
public sealed class DebriefHost(
    DebriefBook book,
    Func<DateTimeOffset> now,
    SettingsService settings,
    PersonaHost personas,
    ILogger<DebriefHost> logger) : IDisposable
{
    private readonly List<DebriefSignal> _signals = [];

    private readonly Lock _signalGate = new();

    private readonly CancellationTokenSource _rewording = new();

    /// <summary>The standing directions the debrief pass drafts and the Commander adopts.</summary>
    public DebriefBook Book { get; } = book;

    /// <summary>The clock an adoption is stamped with.</summary>
    public Func<DateTimeOffset> Now { get; } = now;

    /// <summary>What this session has sounded like, in memory and never on disk.</summary>
    public DebriefSession Session { get; } = new();

    /// <summary>What the prompt carries for the length of this session.</summary>
    public StandingDirectionsSession Directions { get; } = new();

    private bool Enabled => settings.Current.Debrief.Enabled;

    /// <summary>Writes down both halves of one exchange.</summary>
    public void NoteTurn(string? asked, string? answered)
    {
        if (Enabled)
        {
            var heardAt = Now();

            Session.Say(heardAt, DebriefSpeaker.Commander, asked ?? string.Empty);
            Session.Say(heardAt, DebriefSpeaker.Ship, answered ?? string.Empty);
        }
    }

    /// <summary>
    /// Writes down something that reached the Commander from outside the two of them — an in-game
    /// message read aloud, a quoted search result.
    /// </summary>
    public void NoteHeardFromOutside(string text)
    {
        if (Enabled)
        {
            Session.Say(Now(), DebriefSpeaker.Game, text);
        }
    }

    /// <summary>Records that d47 was stopped mid-sentence.</summary>
    public void NoteInterrupted() => NoteSignal(new DebriefSignal(
        Now(),
        DebriefSignalKind.SpeechCutOff,
        "you stopped me while I was talking"));

    /// <summary>Records that a callout was switched off within seconds of it speaking.</summary>
    public void NoteSilenced(CalloutSilenced silenced)
    {
        ArgumentNullException.ThrowIfNull(silenced);

        NoteSignal(new DebriefSignal(
            silenced.When,
            DebriefSignalKind.WarningDisabledSoonAfter,
            $"the {silenced.Id} callout"));
    }

    /// <summary>Opens a directions session over what the file says right now.</summary>
    public void Begin()
    {
        Book.Store.Poll();
        Directions.Begin(Book.Adopted);
    }

    /// <summary>
    /// Has <paramref name="ask"/> reword the proposals earlier sessions drafted, once each, on the pool
    /// (#677). Completes at once when the debrief is off or nothing is pending.
    /// </summary>
    public Task Reword(Func<string, CancellationToken, Task<string?>> ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        if (!Enabled || DebriefRewording.Pending(Book.Store).Count == 0)
        {
            return Task.CompletedTask;
        }

        var token = _rewording.Token;

        return Task.Run(async () =>
        {
            try
            {
                await DebriefRewording.RunAsync(Book.Store, ask, logger, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown; what was not asked is asked at the next launch.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not write a reworded debrief proposal");
            }
        });
    }

    /// <summary>Stops the rewording, at shutdown.</summary>
    public void CancelRewording() => _rewording.Cancel();

    /// <summary>Runs the debrief over what this session sounded like, files what it drafted, and empties the session.</summary>
    /// <param name="frontierId">Who the session belonged to.</param>
    public void Run(string? frontierId = null)
    {
        if (!Enabled)
        {
            return;
        }

        DebriefSignal[] signals;

        lock (_signalGate)
        {
            signals = [.. _signals];
            _signals.Clear();
        }

        try
        {
            var drafted = Book.Propose(
                Session,
                signals,
                Now(),
                personas.Current.Id,

                // What this installation answers to, so "hey Warden, stop calling it that" reads as an
                // instruction rather than as a sentence beginning with a name.
                [personas.Current.Name, settings.Current.Persona.ShipName ?? string.Empty],
                frontierId);

            logger.LogInformation(
                "Debrief drafted {Count} proposals from {Lines} lines and {Signals} signals",
                drafted.Count,
                Session.Lines.Count,
                signals.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a debrief costs a list nobody had agreed to.
            logger.LogWarning(ex, "The debrief pass could not write its proposals");
        }
        finally
        {
            Session.Empty();
        }
    }

    public void Dispose() => _rewording.Dispose();

    private void NoteSignal(DebriefSignal signal)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_signalGate)
        {
            _signals.Add(signal);
        }
    }
}
