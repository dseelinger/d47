using Serilog.Core;
using Serilog.Events;

namespace D47.App.Logging;

/// <summary>
/// Puts logged <b>errors</b> from the speech loop onto the Technical page as well as into the log
/// file (docs/plans/change-requests.md item 6).
/// <para>
/// <b>Why a bridge and not more hand-written lines.</b> The stage indicators beside this are
/// authored, because a log line is phrased for grepping and a stage line is phrased for reading.
/// Errors are the opposite case: what matters is that <em>none</em> of them is missed, including
/// the ones raised at a call site nobody has written yet. A seam that picks up every existing
/// and future <c>LogError</c> is the only version of that which stays true.
/// </para>
/// <para>
/// <b>Errors only, and only from the speech path.</b> Without both filters this is the Log tab
/// twice, and a page that repeats another page is one nobody reads. Warnings stay in the log
/// file: the speech loop warns about things it has already worked around, and a Commander
/// watching a turn go past does not need to be told about each of them.
/// </para>
/// <para>
/// Sinks run on whichever thread raised the event, which for this path is an audio thread. That
/// is safe because the transcript's append is locked; it was not before this existed.
/// </para>
/// </summary>
public sealed class TechnicalLogBridge : ILogEventSink
{
    /// <summary>
    /// The namespaces the speech loop logs under.
    /// <para>
    /// Written out here rather than taken from <c>Subsystems.SourcePrefixes</c>, which is the
    /// repo's one vocabulary for this and would be the right source — except that its Voice entry
    /// points at a namespace that does not exist, so it currently matches nothing. That is a
    /// defect of its own and is recorded in bugs.md; when it is fixed this list should become a
    /// lookup rather than a second copy.
    /// </para>
    /// <para>
    /// The loop spans three of them regardless of how that is repaired: the pipeline that drives
    /// it, the audio it plays and captures, and the gate that decides when to listen.
    /// </para>
    /// </summary>
    public static readonly string[] SpeechSources =
    [
        "D47.App.Voice",
        "D47.Core.Audio",
        "D47.Core.Listening",
    ];

    private Action<string>? _write;

    /// <summary>
    /// Points the bridge at the panel, once there is one.
    /// <para>
    /// Late, because logging is composed first so that everything built after it has somewhere to
    /// report a failure — which means the log exists long before the surface does. Until this is
    /// called the bridge drops what it is given, and that is correct: an error raised during
    /// startup belongs in the file, and there is no page yet for it to be on.
    /// </para>
    /// </summary>
    public void WriteTo(Action<string> write) => _write = write;

    public void Emit(LogEvent logEvent)
    {
        if (_write is not { } write || logEvent.Level < LogEventLevel.Error)
        {
            return;
        }

        if (!IsSpeech(logEvent))
        {
            return;
        }

        var line = logEvent.RenderMessage();

        // The exception's own message, when there is one and it is not already in the text. A
        // logged error whose sentence is "could not start capture" and whose cause is
        // "device in use by another application" is one line short of being actionable.
        if (logEvent.Exception is { } ex && !line.Contains(ex.Message, StringComparison.Ordinal))
        {
            line = $"{line} — {ex.Message}";
        }

        try
        {
            write(line);
        }
        catch (Exception writing) when (writing is InvalidOperationException or ObjectDisposedException)
        {
            // A sink that throws takes down the logging pipeline, and with it every other
            // subsystem's ability to report anything. The page is the least important consumer
            // of this event; the file already has it.
        }
    }

    private static bool IsSpeech(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var value))
        {
            return false;
        }

        var source = value.ToString().Trim('"');

        return SpeechSources.Any(prefix => source.StartsWith(prefix, StringComparison.Ordinal));
    }
}
