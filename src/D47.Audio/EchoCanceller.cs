using D47.Core.Audio;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using SoundFlow.Extensions.WebRtc.Apm;

namespace D47.Audio;

/// <summary>
/// Acoustic echo cancellation (list.md Phase 13), sitting between the microphone and the gate.
/// <para>
/// <b>It consumes the arbiter's render reference tap rather than a loopback capture</b>, which
/// is the whole reason the tap was built in Phase 5 with nothing to consume it
/// (architecture.md D7). A loopback capture is a second WASAPI stream, opened on a device that
/// may not be the one d47 is rendering to, arriving tens of milliseconds late with a clock of
/// its own; the tap is the exact buffer the mixer produced, carrying the sample position that
/// locates it. Lower latency and correctly aligned, in the checklist's own words.
/// </para>
/// <para>
/// The processing is WebRTC's AEC3 through <c>webrtc-apm</c> — BSD-3, the choice
/// architecture.md §2 names. Nothing about that library is reimplemented here: what this class
/// is, is framing. The APM takes exactly 10 ms at a time on both streams and WASAPI hands over
/// whatever it feels like on either, so both directions are accumulated into 10 ms frames and
/// the remainder is carried to the next call.
/// </para>
/// <para>
/// <b>Failure is the capability being off, not a crash.</b> A machine where the native library
/// will not load is a machine that still has push-to-talk, so the constructor records why and
/// <see cref="IsActive"/> stays false — and every buffer then goes to the gate untouched, which
/// is exactly what happened before this class existed (list.md Phase 3).
/// </para>
/// </summary>
public sealed class EchoCanceller : ICaptureSink, IDisposable
{
    /// <summary>
    /// The APM's frame, in hundredths of a second. Not configurable — it is 10 ms in WebRTC and
    /// passing anything else is an error rather than a preference.
    /// </summary>
    private const int FramesPerSecond = 100;

    private readonly ICaptureSink _next;
    private readonly IRenderReferenceTap _tap;
    private readonly ILogger<EchoCanceller> _logger;
    private readonly int _captureRate;

    private readonly Lock _lifecycle = new();

    /// <summary>
    /// Held around every native call, and around the disposal that would pull the module out
    /// from under one.
    /// <para>
    /// It is the answer to a race that is otherwise a native access violation rather than an
    /// exception: <see cref="Stop"/> runs on whichever thread applied a settings row, and the
    /// capture and render threads are inside the module continuously. Freeing the handle while
    /// one of them is in <c>ProcessStream</c> takes the process down, and no <c>catch</c>
    /// anywhere would see it.
    /// </para>
    /// <para>
    /// The cost is that render and capture serialise against each other, where the module itself
    /// holds separate locks and would have allowed them to overlap. That is a real loss and a
    /// small one — a 10 ms frame is tens of microseconds of work — and it buys the disposal
    /// being safe by construction rather than by timing.
    /// </para>
    /// </summary>
    private readonly Lock _processing = new();

    private AudioProcessingModule? _apm;
    private StreamConfig? _captureIn;
    private StreamConfig? _captureOut;
    private StreamConfig? _renderIn;
    private StreamConfig? _renderOut;

    /// <summary>Capture-side framing. One channel, because the microphone path is already mono.</summary>
    private readonly float[][] _captureFrame;
    private readonly float[][] _cleanFrame;
    private int _captureHeld;

    /// <summary>
    /// Render-side framing. Sized on the first reference frame, because the mixer's channel
    /// count and rate are its business and this only has to agree with them.
    /// </summary>
    private float[][] _renderFrame = [];
    private float[][] _renderOutFrame = [];
    private int _renderHeld;
    private int _renderRate;
    private int _renderChannels;

    private bool _subscribed;
    private bool _disposed;

    public EchoCanceller(
        ICaptureSink next,
        IRenderReferenceTap tap,
        int captureRate,
        ILogger<EchoCanceller> logger)
    {
        _next = next;
        _tap = tap;
        _captureRate = captureRate;
        _logger = logger;

        var frame = captureRate / FramesPerSecond;
        _captureFrame = [new float[frame]];
        _cleanFrame = [new float[frame]];
    }

    /// <summary>Whether audio is actually being cleaned. False is a state to report, not an error.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Why it is not running, when it is not.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>
    /// Noise suppression, which the same module offers and which is worth having on for its own
    /// sake: the voice-activity gate is a decision about how far a frame sits above the room, and
    /// removing the room is the most direct possible way to make that decision easier.
    /// </summary>
    public bool SuppressNoise { get; set; } = true;

    /// <summary>
    /// Starts cleaning, or records why it cannot. Safe to call repeatedly — an instance already
    /// running is left alone rather than cycled, so applying an unrelated listening setting does
    /// not throw away a converged filter and make d47 audible to itself for a second.
    /// </summary>
    public void Start()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsActive)
            {
                return;
            }

            try
            {
                using var config = new ApmConfig();

                // Not mobile mode. That variant is the fixed-point one written for phones with
                // a known handset geometry, and it trades the delay estimator for assumptions
                // that are wrong about a desk with speakers on it.
                config.SetEchoCanceller(enabled: true, mobileMode: false);
                config.SetNoiseSuppression(SuppressNoise, NoiseSuppressionLevel.Moderate);

                // Rumble, a desk knock and a fan's fundamental all sit under speech and all move
                // the energy the gate measures. Free here, and the gate is downstream of it.
                config.SetHighPassFilter(true);

                // Both gain controllers off. Whisper is not level-sensitive, the gate's floor
                // follows the room by design, and an automatic gain that pumps between sentences
                // is a detector permanently re-learning what the room is.
                config.SetGainController1(false, GainControlMode.AdaptiveDigital, 0, 0, false);
                config.SetGainController2(false);

                var apm = new AudioProcessingModule();

                Check(apm.ApplyConfig(config), "configure");
                Check(apm.Initialize(), "initialise");

                // A hint, not a measurement. AEC3 estimates the true delay itself and keeps
                // re-estimating it, which is what it has to do given that render and capture are
                // two devices on two clocks; this only saves it the first second or so of
                // hunting. The number is the mixer's own 60 ms of WASAPI latency plus a capture
                // buffer, which is the right order for every machine this will meet.
                apm.SetStreamDelayMs(80);

                // Published under the processing lock, and the module last of all. The capture
                // thread's only test for "is there something to process" is whether the module
                // is there, so a module set before its stream configs is a window — small, real,
                // and hit within seconds under a start/stop loop — in which a frame arrives, sees
                // a module, and dereferences a config that does not exist yet. Which then reads
                // as the canceller failing at runtime, because that is how it reports.
                lock (_processing)
                {
                    _captureIn = new StreamConfig(_captureRate, 1);
                    _captureOut = new StreamConfig(_captureRate, 1);
                    _apm = apm;
                }

                if (!_subscribed)
                {
                    _tap.Rendered += OnRendered;
                    _subscribed = true;
                }

                IsActive = true;
                Unavailable = null;

                _logger.LogInformation(
                    "Echo cancellation running (WebRTC AEC3, {Rate} Hz capture, noise suppression {State})",
                    _captureRate,
                    SuppressNoise ? "on" : "off");
            }
            catch (Exception ex)
            {
                // DllNotFoundException on a machine missing the Visual C++ runtime is the case
                // this is really for, and it is not a reason for d47 to fail to start.
                Unavailable = $"Echo cancellation could not start: {ex.Message}";
                _logger.LogWarning(ex, "Echo cancellation could not start; capture will not be cleaned");
                Release();
            }
        }
    }

    /// <summary>
    /// Stops cleaning and stays reusable, like the microphone's own Close. The Commander can
    /// turn the row back on, and that has to start it again rather than fail.
    /// </summary>
    public void Stop()
    {
        lock (_lifecycle)
        {
            if (!IsActive && _apm is null)
            {
                return;
            }

            Release();
            Unavailable = null;
        }
    }

    /// <summary>
    /// One capture buffer, cleaned and passed on.
    /// <para>
    /// Called on the WASAPI capture thread. Whatever does not fill a whole 10 ms frame is held
    /// for the next call rather than padded — padding a short frame with silence tells the
    /// canceller the Commander stopped talking, mid-word, several times a second.
    /// </para>
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (_apm is null || !IsActive)
        {
            _next.Write(samples);
            return;
        }

        var frame = _captureFrame[0];
        var at = 0;
        Exception? failure = null;
        AudioProcessingModule? attempted = null;

        while (at < samples.Length)
        {
            var take = Math.Min(frame.Length - _captureHeld, samples.Length - at);
            samples.Slice(at, take).CopyTo(frame.AsSpan(_captureHeld));
            _captureHeld += take;
            at += take;

            if (_captureHeld < frame.Length)
            {
                break;
            }

            _captureHeld = 0;

            try
            {
                lock (_processing)
                {
                    // Re-checked inside the lock: Stop could have run between the check at the
                    // top of this method and here, and this is the one place where being wrong
                    // about that is a native crash rather than an exception.
                    if (_apm is not { } live)
                    {
                        _next.Write(frame);
                        continue;
                    }

                    attempted = live;

                    var result = live.ProcessStream(_captureFrame, _captureIn!, _captureOut!, _cleanFrame);

                    _next.Write(result == ApmError.NoError ? _cleanFrame[0] : frame);
                }
            }
            catch (Exception ex)
            {
                // A real-time callback: it must never throw into NAudio's capture thread, and a
                // canceller that has started failing must not also take away the microphone.
                _logger.LogError(ex, "Echo cancellation failed on a capture frame; passing it through");

                // Recorded rather than acted on here. Giving up takes the lifecycle lock and then
                // this one, and taking them the other way round is how the two threads deadlock.
                failure = ex;
                _next.Write(frame);
            }
        }

        if (failure is not null)
        {
            Fail(failure, attempted);
        }
    }

    public void Reset()
    {
        // Under the processing lock because these are not this thread's counters alone:
        // _renderHeld indexes the render thread's part-filled frame, and Release zeroes both.
        // The capture thread is the only caller, and that is not the same as being the only
        // writer.
        lock (_processing)
        {
            _captureHeld = 0;
            _renderHeld = 0;
        }

        // Outside it, because nothing downstream of the gate belongs behind a lock whose whole
        // job is to keep two audio threads out of a native module.
        _next.Reset();
    }

    /// <summary>
    /// A buffer that has just gone to the speakers, as the far-end reference.
    /// <para>
    /// Called on the WASAPI render thread, concurrently with <see cref="Write"/> on the capture
    /// thread. That is the arrangement the APM is built for — it holds separate render and
    /// capture locks internally — so the only reason this one serialises against the capture
    /// side is the disposal, which is what <see cref="_processing"/> already explains.
    /// </para>
    /// <para>
    /// The tap carries 16-bit PCM because that is what an <see cref="AudioClip"/> is; it is
    /// widened back to float here rather than the tap being changed, since every other consumer
    /// of a clip wants the bytes.
    /// </para>
    /// </summary>
    private void OnRendered(RenderReferenceFrame reference)
    {
        if (_apm is null || !IsActive)
        {
            return;
        }

        Exception? failure = null;
        AudioProcessingModule? attempted = null;

        try
        {
            lock (_processing)
            {
                // The module read inside the lock is the one that gets called — never one
                // glimpsed outside it. Between that glimpse and here a Stop and a Start can both
                // have run, and finding the field non-null again says nothing about the handle
                // this thread was holding: that one has been freed. Calling through it is the
                // access violation the lock exists to prevent, which is why Write has always
                // re-read and why this does now too.
                if (_apm is not { } live)
                {
                    return;
                }

                attempted = live;

                Reshape(reference.Format);

                var pcm = reference.Pcm.Span;
                var channels = _renderChannels;
                var frame = _renderFrame[0].Length;

                // Interleaved shorts in, planar floats out, which is the layout the APM takes.
                for (var sample = 0; sample + (channels * 2) <= pcm.Length; sample += channels * 2)
                {
                    for (var channel = 0; channel < channels; channel++)
                    {
                        var at = sample + (channel * 2);
                        var value = (short)(pcm[at] | (pcm[at + 1] << 8));
                        _renderFrame[channel][_renderHeld] = value / 32768f;
                    }

                    _renderHeld++;

                    if (_renderHeld < frame)
                    {
                        continue;
                    }

                    _renderHeld = 0;
                    live.ProcessReverseStream(_renderFrame, _renderIn!, _renderOut!, _renderOutFrame);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Echo cancellation failed on a render reference frame");
            failure = ex;
        }

        if (failure is not null)
        {
            Fail(failure, attempted);
        }
    }

    /// <summary>
    /// Makes the render-side buffers match the format the mixer is actually producing. It is
    /// fixed in practice — 48 kHz stereo, stated once in <see cref="WasapiAudioSink"/> — and is
    /// read from the frame anyway, because a canceller that assumes the mixer's format is a
    /// canceller that silently cancels nothing the day it changes.
    /// </summary>
    private void Reshape(AudioFormat format)
    {
        if (_renderRate == format.SampleRate && _renderChannels == format.Channels)
        {
            return;
        }

        _renderRate = format.SampleRate;
        _renderChannels = Math.Max(1, format.Channels);
        _renderHeld = 0;

        var frame = Math.Max(1, _renderRate / FramesPerSecond);

        _renderFrame = new float[_renderChannels][];
        _renderOutFrame = new float[_renderChannels][];

        for (var channel = 0; channel < _renderChannels; channel++)
        {
            _renderFrame[channel] = new float[frame];
            _renderOutFrame[channel] = new float[frame];
        }

        _renderIn?.Dispose();
        _renderOut?.Dispose();
        _renderIn = new StreamConfig(_renderRate, _renderChannels);
        _renderOut = new StreamConfig(_renderRate, _renderChannels);

        _logger.LogDebug(
            "Echo cancellation reference is {Rate} Hz, {Channels} channel(s)", _renderRate, _renderChannels);
    }

    /// <summary>
    /// Gives up cleanly after a failure at runtime. The microphone keeps working and the panel
    /// says why the cancellation stopped, rather than the Commander finding out by being told
    /// d47 answered itself.
    /// <para>
    /// The failure belongs to a module rather than to this class, which is what
    /// <paramref name="module"/> is for. An audio thread reaches here after leaving the
    /// processing lock, and a Stop and a Start can both have run in between — so the canceller
    /// now running may be a different one that has done nothing wrong. Tearing that one down
    /// reports a fault the Commander has already cleared, and because
    /// <c>ApplyListeningSettings</c> reads <see cref="IsActive"/> immediately after
    /// <see cref="Start"/>, it also leaves the gate with the wrong answer about whether d47's
    /// own voice is being subtracted — the one belief that decides whether hands-free listening
    /// stays open while d47 speaks.
    /// </para>
    /// </summary>
    private void Fail(Exception ex, AudioProcessingModule? module)
    {
        lock (_lifecycle)
        {
            // _apm is only ever written by Start, Stop, Release and Dispose, all of which hold
            // this lock for their whole run — so holding it here is what makes the comparison
            // mean anything. A null module cannot match a running one, which is the right
            // answer for the only path that reaches here without one: a canceller that was
            // already stopped has nothing left to give up.
            if (!IsActive || !ReferenceEquals(_apm, module))
            {
                return;
            }

            Unavailable = $"Echo cancellation stopped: {ex.Message}";
            Release();
        }
    }

    private void Release()
    {
        IsActive = false;

        if (_subscribed)
        {
            _tap.Rendered -= OnRendered;
            _subscribed = false;
        }

        // Under the processing lock, because the two audio threads are in the module
        // continuously and freeing its handle underneath one of them is an access violation
        // rather than an exception. Unsubscribing above is not enough on its own: a render
        // callback already past the subscription check is still on its way in.
        lock (_processing)
        {
            _apm?.Dispose();
            _apm = null;

            _captureIn?.Dispose();
            _captureOut?.Dispose();
            _renderIn?.Dispose();
            _renderOut?.Dispose();
            _captureIn = _captureOut = _renderIn = _renderOut = null;

            _captureHeld = 0;
            _renderHeld = 0;
            _renderRate = 0;
            _renderChannels = 0;
        }
    }

    private static void Check(ApmError result, string what)
    {
        if (result != ApmError.NoError)
        {
            throw new InvalidOperationException($"WebRTC APM failed to {what}: {result}.");
        }
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release();
        }
    }
}
