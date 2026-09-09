using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using D47.Core;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.App.Diagnostics;

/// <summary>
/// What the injector actually sent, step by step, with stills of the game beside the steps a caller
/// marked (#365).
/// </summary>
public sealed class InputTraceWriter : IInputStepObserver, IDisposable
{
    /// <summary>Set this to <c>1</c> to trace every injected sequence for one run.</summary>
    public const string EnvironmentVariable = "D47_TRACE_INPUT";

    /// <summary>The same switch with no shell in it, for a shortcut to carry (#180's lesson).</summary>
    public const string Flag = "--trace-input";

    /// <summary>Where traces are written, under <c>data\</c>.</summary>
    public const string FolderName = "input-traces";

    /// <summary>Whether the command line carried <see cref="Flag"/>.</summary>
    internal static bool Switched { get; private set; }

    /// <summary>
    /// Reads the command line for <see cref="Flag"/>, the way the audio recorder's switch is read and
    /// for the same reason: one place decides what the switch is spelled like, and a test can drive it
    /// without setting a process-wide variable.
    /// </summary>
    public static void ReadCommandLine(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Switched = args.Contains(Flag, StringComparer.Ordinal);
    }

    /// <summary>Whether tracing is on for this process, by either road.</summary>
    public static bool Enabled =>
        Switched || Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>How wide a still is written.</summary>
    public const int StillWidth = 960;

    private readonly string _folder;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<GameStatus> _status;
    private readonly Func<string?> _music;
    private readonly IWindowCapture? _capture;
    private readonly ILogger _logger;
    private readonly BlockingCollection<Entry> _pending = [];
    private readonly Thread _writer;
    private volatile bool _closed;

    private InputTraceWriter(
        string folder,
        Func<DateTimeOffset> now,
        Func<GameStatus> status,
        Func<string?> music,
        IWindowCapture? capture,
        ILogger logger)
    {
        _folder = folder;
        _now = now;
        _status = status;
        _music = music;
        _capture = capture;
        _logger = logger;

        // A thread of its own rather than a pool task, for the audio recorder's reason: shutdown waits for
        // the queue to drain, and a pool task waited on from the pool is the starvation this repository has
        // already recorded once.
        _writer = new Thread(Write)
        {
            IsBackground = true,
            Name = "d47 input trace",
        };

        _writer.Start();
    }

    /// <summary>
    /// A writer if this process asked for one, otherwise null — so every caller's check is "is there
    /// one", and nothing dormant sits in the injector's step loop.
    /// </summary>
    public static InputTraceWriter? Create(
        AppPaths paths,
        Func<DateTimeOffset> now,
        Func<GameStatus> status,
        Func<string?> music,
        Func<IWindowCapture?> capture,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Enabled)
        {
            // The capture is asked for behind the gate rather than passed in, so an ordinary run does not
            // even construct the thing that talks to Direct3D.
            return null;
        }

        var folder = Path.Combine(paths.Data, "flight", FolderName);

        logger.LogInformation(
            "Input tracing is on; every injected sequence writes a folder under {Folder}",
            folder);

        return new InputTraceWriter(folder, now, status, music, capture(), logger);
    }

    /// <summary>
    /// A writer without consulting the environment, so a test can exercise the writing without setting
    /// a process-wide variable every other test in the run would also see.
    /// </summary>
    internal static InputTraceWriter Regardless(
        string folder,
        Func<DateTimeOffset> now,
        Func<GameStatus> status,
        Func<string?> music,
        IWindowCapture? capture,
        ILogger logger) =>
        new(folder, now, status, music, capture, logger);

    /// <summary>Where the traces of this run are, for a log line and for a test to read.</summary>
    public string Folder => _folder;

    public IInputTrace Open(string caller)
    {
        var name = $"{_now():yyyyMMdd-HHmmss-fff}-{Safe(caller)}";
        var trace = new Trace(this, Path.Combine(_folder, name));

        trace.Line("open", [("caller", caller)]);

        return trace;
    }

    /// <summary>A caller's name as a folder name.</summary>
    private static string Safe(string caller)
    {
        var clean = new StringBuilder(caller.Length);

        foreach (var character in caller)
        {
            clean.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return clean.Length == 0 ? IInputStepObserver.Anonymous : clean.ToString();
    }

    /// <summary>One trace: one folder, one <c>trace.jsonl</c>, and a still per marked step.</summary>
    private sealed class Trace(InputTraceWriter writer, string folder) : IInputTrace
    {
        private readonly Lock _gate = new();

        private GuiFocus _focus = writer._status().GuiFocus;
        private string? _music = writer._music();
        private bool _verdict;
        private bool _closed;

        /// <summary>Stills written by this trace so far, which is what keeps their names apart (#404).</summary>
        private int _stills;

        public void Stepped(InputStepReport report)
        {
            GuiFocus before;
            string? musicBefore;

            var after = writer._status().GuiFocus;
            var musicAfter = writer._music();

            lock (_gate)
            {
                // "Before" is what was read at the end of the previous step, which is what a reader wants:
                // the pair says whether this step is the one that moved the focus.
                before = _focus;
                musicBefore = _music;
                _focus = after;
                _music = musicAfter;
            }

            Line(
                "step",
                [
                    ("i", report.Index),
                    ("step", report.Step.ToString()),
                    ("mark", report.Step.Mark),
                    ("foreground", report.Foreground),
                    ("guiFocusBefore", before.ToString()),
                    ("guiFocusAfter", after.ToString()),
                    ("musicBefore", musicBefore),
                    ("musicAfter", musicAfter),
                ],
                report.At,
                report.Step.Capture
                    ? $"{Interlocked.Increment(ref _stills):00}-{report.Index:00}-{report.Step.Mark ?? "step"}.png"
                    : null);
        }

        public void Declare(string name, string value) =>
            Line("declare", [("name", name), ("value", value)]);

        public void Verdict(string verdict, string reason)
        {
            lock (_gate)
            {
                _verdict = true;
            }

            Line("verdict", [("verdict", verdict), ("reason", reason)]);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;

                if (_verdict)
                {
                    return;
                }
            }

            // A sequence that never reached a verdict is the interesting one: a cancelled turn, or a fault.
            Line("verdict", [("verdict", "unfinished"), ("reason", "the trace closed without a verdict")]);
        }

        internal void Line(
            string kind,
            IReadOnlyList<(string Name, object? Value)> fields,
            DateTimeOffset? at = null,
            string? still = null) =>
            writer.Queue(new Entry(folder, kind, at ?? writer._now(), fields, still));
    }

    private sealed record Entry(
        string Folder,
        string Kind,
        DateTimeOffset At,
        IReadOnlyList<(string Name, object? Value)> Fields,
        string? Still);

    /// <summary>One writer thread for every trace.</summary>
    private void Write()
    {
        foreach (var line in _pending.GetConsumingEnumerable())
        {
            try
            {
                Directory.CreateDirectory(line.Folder);

                var fields = new List<(string Name, object? Value)>(line.Fields.Count + 4)
                {
                    ("kind", line.Kind),
                    ("at", line.At.ToString("O")),
                };

                fields.AddRange(line.Fields);

                if (line.Still is { } still)
                {
                    // A capture that fails is a line in the trace, never a failed sequence.
                    var began = _now();
                    var refused = _capture is null
                        ? "no capture on this run"
                        : _capture.Capture(Path.Combine(line.Folder, still));

                    fields.Add(("still", refused is null ? still : null));

                    // When the picture was taken, beside when the step went. The frame is grabbed on
                    // this thread, behind the sequence, so it shows the game at <c>stillAt</c> and not at
                    // <c>at</c> — and the first capture of a run pays for the Direct3D device and can arrive
                    // after the next step has landed.
                    fields.Add(("stillAt", began.ToString("O")));
                    fields.Add(("stillMs", (int)(_now() - began).TotalMilliseconds));
                    fields.Add(("stillError", refused));
                }

                File.AppendAllText(
                    Path.Combine(line.Folder, "trace.jsonl"),
                    Render(fields) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A full disk, or a folder the Commander has open.
                _logger.LogWarning(ex, "Could not write an input trace line");
            }
        }
    }

    private static string Render(IReadOnlyList<(string Name, object? Value)> fields)
    {
        var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();

            foreach (var (name, value) in fields)
            {
                switch (value)
                {
                    case null:
                        json.WriteNull(name);
                        break;
                    case bool flag:
                        json.WriteBoolean(name, flag);
                        break;
                    case int number:
                        json.WriteNumber(name, number);
                        break;
                    default:
                        json.WriteString(name, value.ToString());
                        break;
                }
            }

            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void Queue(Entry line)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _pending.Add(line);
        }
        catch (InvalidOperationException)
        {
        // Closed between the check above and this line.
        }
    }

    /// <summary>Drains what is queued and stops the writer.</summary>
    public void Dispose()
    {
        _closed = true;
        _pending.CompleteAdding();

        if (!_writer.Join(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarning("The input trace was still writing at shutdown; some lines were dropped");
        }

        _pending.Dispose();
        (_capture as IDisposable)?.Dispose();
    }
}
