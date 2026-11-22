using System.Runtime.ExceptionServices;
using D47.Core.Listening;
using D47.Core.Viewing;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// Writes captions for one video from its soundtrack, on a thread of its own: <see cref="CaptionRun.Stretch"/> at a time,
/// up to <see cref="CaptionRun.MostAhead"/> ahead of the picture. While the captions are behind the picture it holds
/// the video, and lets it go once they are <see cref="CaptionRun.PlayOnAhead"/> past it. A picture moved outside the
/// written captions starts writing again from there.
/// </summary>
internal sealed class CaptionWriter : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(100);

    private readonly string _source;
    private readonly ISoundtrackCaptioner _captioner;
    private readonly Func<TimeSpan?> _position;
    private readonly Action<bool> _hold;
    private readonly ILogger _logger;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly CaptionRun _run;

    private CancellationTokenSource? _stretch;
    private bool _holding;
    private string? _problem;

    /// <param name="position">The picture's position, or null while the video is not playing this source.</param>
    /// <param name="hold">Holds the video still, or lets it go.</param>
    public CaptionWriter(
        string source,
        TimeSpan from,
        ISoundtrackCaptioner captioner,
        Func<TimeSpan?> position,
        Action<bool> hold,
        ILogger logger)
    {
        _source = source;
        _captioner = captioner;
        _position = position;
        _hold = hold;
        _logger = logger;
        _run = new CaptionRun(from);
        _thread = new Thread(Run) { IsBackground = true, Name = "d47-captions" };
    }

    /// <summary>Raised on the writer's thread when cues were written, or writing stopped.</summary>
    public event Action? Changed;

    public IReadOnlyList<SubtitleCue> Cues
    {
        get
        {
            lock (_gate)
            {
                return _run.Cues;
            }
        }
    }

    /// <summary>How far the captions reach.</summary>
    public TimeSpan WrittenTo
    {
        get
        {
            lock (_gate)
            {
                return _run.WrittenTo;
            }
        }
    }

    /// <summary>Whether the captions reach the end of the soundtrack.</summary>
    public bool Finished
    {
        get
        {
            lock (_gate)
            {
                return _run.Finished;
            }
        }
    }

    /// <summary>Why writing stopped short, or null.</summary>
    public string? Problem
    {
        get
        {
            lock (_gate)
            {
                return _problem;
            }
        }
    }

    public void Start() => _thread.Start();

    /// <summary>Stops writing and lets the video go; the writer's thread ends on its own.</summary>
    public void Dispose()
    {
        try
        {
            _stop.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        Hold(false);
    }

    private void Run()
    {
        SoundtrackReader? reader = null;

        try
        {
            reader = SoundtrackReader.Open(_source, out var problem);

            if (reader is null)
            {
                Stop($"The soundtrack could not be read: {problem}");
                return;
            }

            while (!_stop.IsCancellationRequested)
            {
                Check();

                if (Ahead())
                {
                    _stop.Token.WaitHandle.WaitOne(CheckEvery);
                    continue;
                }

                if (!Write(reader))
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Captions for {Source} could not be written", _source);
            Stop(ex is InvalidOperationException ? ex.Message : "The captions could not be written.");
        }
        finally
        {
            reader?.Dispose();
            Hold(false);
            _captioner.Release();
            _stop.Dispose();
        }
    }

    /// <summary>Writes one stretch, checking the picture while it is written; false once writing has stopped.</summary>
    private bool Write(SoundtrackReader reader)
    {
        TimeSpan from;

        lock (_gate)
        {
            from = _run.WrittenTo;
        }

        if (reader.Read(from, CaptionRun.Stretch) is not { } stretch)
        {
            Stop("The soundtrack stopped being read.");
            return false;
        }

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);

        lock (_gate)
        {
            _stretch = cancel;
        }

        try
        {
            var writing = _captioner.CaptionAsync(new Utterance(stretch.Samples, ISoundtrackCaptioner.SampleRate), cancel.Token);

            while (!writing.Wait(CheckEvery))
            {
                Check();
            }

            lock (_gate)
            {
                if (cancel.IsCancellationRequested)
                {
                    return true;
                }

                _run.Accept(stretch.From, stretch.Length, writing.Result, stretch.Ended);
            }
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            return true;
        }
        catch (AggregateException ex) when (ex.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
        finally
        {
            lock (_gate)
            {
                _stretch = null;
            }
        }

        Check();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Compares the picture with the captions: starts again where it moved outside them, and holds or lets go of the
    /// video. Nothing happens while the video is not playing this source.
    /// </summary>
    private void Check()
    {
        if (_stop.IsCancellationRequested || _position() is not { } position)
        {
            return;
        }

        bool behind;
        bool caughtUp;
        var restarted = false;

        lock (_gate)
        {
            if (_run.Jumped(position))
            {
                _run.Restart(position);
                _stretch?.Cancel();
                restarted = true;
            }

            behind = _run.Behind(position);
            caughtUp = _run.CaughtUp(position);
        }

        if (behind)
        {
            Hold(true);
        }
        else if (caughtUp)
        {
            Hold(false);
        }

        if (restarted)
        {
            Changed?.Invoke();
        }
    }

    private bool Ahead()
    {
        var position = _position();

        lock (_gate)
        {
            return _run.Ahead(position ?? _run.From);
        }
    }

    private void Hold(bool held)
    {
        lock (_gate)
        {
            if (_holding == held && !held)
            {
                return;
            }

            _holding = held;
        }

        _hold(held);
    }

    private void Stop(string problem)
    {
        _logger.LogWarning("Captions for {Source} stopped: {Problem}", _source, problem);

        lock (_gate)
        {
            _problem = problem;
        }

        Hold(false);
        Changed?.Invoke();
    }
}
