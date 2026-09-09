using D47.Core.Audio;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using SoundFlow.Extensions.WebRtc.Apm;

namespace D47.Audio;

/// <summary>Acoustic echo cancellation (Phase 13), sitting between the microphone and the gate.</summary>
public sealed class EchoCanceller : ICaptureSink, IDisposable
{
    /// <summary>The APM's frame, in hundredths of a second.</summary>
    private const int FramesPerSecond = 100;

    private readonly ICaptureSink _next;
    private readonly IRenderReferenceTap _tap;
    private readonly ILogger<EchoCanceller> _logger;
    private readonly int _captureRate;

    private readonly Lock _lifecycle = new();

    /// <summary>
    /// Held around every native call, and around the disposal that would pull the module out from under
    /// one.
    /// </summary>
    private readonly Lock _processing = new();

    private AudioProcessingModule? _apm;
    private StreamConfig? _captureIn;
    private StreamConfig? _captureOut;
    private StreamConfig? _renderIn;
    private StreamConfig? _renderOut;

    /// <summary>Capture-side framing.</summary>
    private readonly float[][] _captureFrame;
    private readonly float[][] _cleanFrame;
    private int _captureHeld;

    /// <summary>Render-side framing.</summary>
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

    /// <summary>Whether audio is actually being cleaned.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Why it is not running, when it is not.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>
    /// Noise suppression, which the same module offers and which is worth having on for its own sake:
    /// the voice-activity gate is a decision about how far a frame sits above the room, and removing
    /// the room is the most direct possible way to make that decision easier.
    /// </summary>
    public bool SuppressNoise { get; set; } = true;

    /// <summary>Starts cleaning, or records why it cannot.</summary>
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

                // Not mobile mode.
                config.SetEchoCanceller(enabled: true, mobileMode: false);
                config.SetNoiseSuppression(SuppressNoise, NoiseSuppressionLevel.Moderate);

                // Rumble, a desk knock and a fan's fundamental all sit under speech and all move the energy
                // the gate measures.
                config.SetHighPassFilter(true);

                // Both gain controllers off.
                config.SetGainController1(false, GainControlMode.AdaptiveDigital, 0, 0, false);
                config.SetGainController2(false);

                var apm = new AudioProcessingModule();

                Check(apm.ApplyConfig(config), "configure");
                Check(apm.Initialize(), "initialise");

                // A hint, not a measurement.
                apm.SetStreamDelayMs(80);

                // Published under the processing lock, and the module last of all.
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
                // DllNotFoundException on a machine missing the Visual C++ runtime is the case this is really
                // for, and it is not a reason for d47 to fail to start.
                Unavailable = $"Echo cancellation could not start: {ex.Message}";
                _logger.LogWarning(ex, "Echo cancellation could not start; capture will not be cleaned");
                Release();
            }
        }
    }

    /// <summary>Stops cleaning and stays reusable, like the microphone's own Close.</summary>
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

    /// <summary>One capture buffer, cleaned and passed on.</summary>
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
                    // Re-checked inside the lock: Stop could have run between the check at the top of this
                    // method and here, and this is the one place where being wrong about that is a native
                    // crash rather than an exception.
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
                // A real-time callback: it must never throw into NAudio's capture thread, and a canceller
                // that has started failing must not also take away the microphone.
                _logger.LogError(ex, "Echo cancellation failed on a capture frame; passing it through");

                // Recorded rather than acted on here.
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
        // Under the processing lock because these are not this thread's counters alone: _renderHeld indexes
        // the render thread's part-filled frame, and Release zeroes both.
        lock (_processing)
        {
            _captureHeld = 0;
            _renderHeld = 0;
        }

        // Outside it, because nothing downstream of the gate belongs behind a lock whose whole job is to keep
        // two audio threads out of a native module.
        _next.Reset();
    }

    /// <summary>A buffer that has just gone to the speakers, as the far-end reference.</summary>
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
                // The module read inside the lock is the one that gets called — never one glimpsed outside
                // it.
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

    /// <summary>Makes the render-side buffers match the format the mixer is actually producing.</summary>
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

    /// <summary>Gives up cleanly after a failure at runtime.</summary>
    private void Fail(Exception ex, AudioProcessingModule? module)
    {
        lock (_lifecycle)
        {
            // _apm is only ever written by Start, Stop, Release and Dispose, all of which hold this lock for
            // their whole run — so holding it here is what makes the comparison mean anything.
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

        // Under the processing lock, because the two audio threads are in the module continuously and freeing
        // its handle underneath one of them is an access violation rather than an exception.
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
