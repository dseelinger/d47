using System.Diagnostics;
using System.Globalization;

namespace D47.EliteVad;

/// <summary>
/// Captures one process's audio (Elite's by default), runs Silero VAD on it and logs when speech starts and stops.
/// Writes capture.wav (48 kHz stereo, as captured), vad16k.wav (16 kHz mono, what the VAD heard) and vad.log.
/// Both WAVs are padded with silence so that a WAV offset equals the log's run time.
///
/// vad.log is tab-separated: kind, run seconds, UTC, fields.
///   c        chunk start; speech probability; lag in ms from the chunk's last sample to its result
///   onN/offN rule N (N chunks over the threshold to start); run time is the deciding chunk's end;
///            first= is where the over (or under) run began; lag as for c
///   journal  a journal event as it was seen: event name, the journal's own timestamp
///   status   Status.json keys that changed, as they were seen
///   cpu      percent of one core, percent of the machine, over the last minute
/// Usage: d47-elite-vad [--process EliteDangerous64 | --pid N] [--model silero_vad.onnx] [--out folder]
///                      [--threshold 0.5] [--journals folder] [--seconds N]
/// </summary>
internal static class Program
{
    private const double ChunkSeconds = SileroVad.ChunkSamples / 16000.0;
    private const int StopChunks = 16;
    private const int GapToleranceFrames = ProcessLoopback.SampleRate / 50;

    [MTAThread]
    private static int Main(string[] args)
    {
        var processName = "EliteDangerous64";
        int? pid = null;
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "d47-elite-vad");
        var model = Path.Combine(home, "silero_vad.onnx");
        var output = Path.Combine(home, "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var threshold = 0.5f;
        var journals = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "Frontier Developments", "Elite Dangerous");
        double? seconds = null;

        for (var i = 0; i < args.Length; i++)
        {
            var hasValue = i + 1 < args.Length;
            switch (args[i])
            {
                case "--process" when hasValue: processName = args[++i]; break;
                case "--pid" when hasValue: pid = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--model" when hasValue: model = args[++i]; break;
                case "--out" when hasValue: output = args[++i]; break;
                case "--threshold" when hasValue: threshold = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--journals" when hasValue: journals = args[++i]; break;
                case "--seconds" when hasValue: seconds = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default:
                    Console.Error.WriteLine("Usage: d47-elite-vad [--process name | --pid N] [--model path] [--out folder] [--threshold 0.5] [--journals folder] [--seconds N]");
                    return 2;
            }
        }

        if (!File.Exists(model))
        {
            Console.Error.WriteLine($"Silero VAD model not found at {model}. Put silero_vad.onnx there or pass --model.");
            return 2;
        }

        Directory.CreateDirectory(output);
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };
        if (seconds is { } limit)
        {
            stop.CancelAfter(TimeSpan.FromSeconds(limit));
        }

        using var log = new RunLog(Path.Combine(output, "vad.log"));
        using var session = new CaptureSession(log, model, threshold, output);
        log.Line("run", 0, string.Create(CultureInfo.InvariantCulture,
            $"target={(pid is { } p ? $"pid {p}" : processName)}\tthreshold={threshold}\tstop={StopChunks} chunks\tmodel={Path.GetFileName(model)}"));

        Thread? watcherThread = null;
        if (Directory.Exists(journals))
        {
            var watcher = new GameWatcher(journals, log);
            watcherThread = new Thread(() => watcher.Run(stop.Token)) { IsBackground = true, Name = "journal watch" };
            watcherThread.Start();
        }
        else
        {
            log.Line("note", 0, "journal folder not found; no journal or status lines");
        }

        Console.WriteLine($"Writing to {output}");
        Console.WriteLine("Press Ctrl+C to stop.");
        session.Run(pid, processName, stop.Token);
        watcherThread?.Join(TimeSpan.FromSeconds(2));
        session.Finish();
        Console.WriteLine($"Done. {session.Summary}");
        return 0;
    }

    private sealed class CaptureSession : IDisposable
    {
        private readonly RunLog _log;
        private readonly float _threshold;
        private readonly WavWriter _wav48;
        private readonly WavWriter _wav16;
        private readonly SileroVad _vad;
        private readonly Decimator _decimator = new();
        private readonly OnsetRule[] _rules = [new(1, StopChunks), new(2, StopChunks), new(3, StopChunks)];
        private readonly List<float> _resampled = new(4096);
        private readonly float[] _chunk = new float[SileroVad.ChunkSamples];
        private readonly byte[] _pcm16 = new byte[8192];
        private readonly byte[] _silence = new byte[ProcessLoopback.BytesPerFrame * 4800];
        private int _chunkFill;
        private long _chunkIndex;
        private double _lastPacket;
        private long _earlyPackets;
        private double _maxLagMs;
        private readonly int[] _onsets = new int[3];

        public CaptureSession(RunLog log, string model, float threshold, string folder)
        {
            _log = log;
            _threshold = threshold;
            _vad = new SileroVad(model);
            _wav48 = new WavWriter(folder, "capture", ProcessLoopback.SampleRate, ProcessLoopback.Channels);
            _wav16 = new WavWriter(folder, "vad16k", 16000, 1);
            _wav48.PartOpened = (name, frame) => _log.Line("part", frame / (double)ProcessLoopback.SampleRate, name);
        }

        public string Summary => string.Create(CultureInfo.InvariantCulture,
            $"{_chunkIndex * ChunkSeconds / 60:F1} min; onsets by rule 1/2/3: {_onsets[0]}/{_onsets[1]}/{_onsets[2]}; packets earlier than the written audio: {_earlyPackets}");

        public void Run(int? pid, string processName, CancellationToken stop)
        {
            var process = Process.GetCurrentProcess();
            var cpuWall = _log.Now;
            var cpuTime = process.TotalProcessorTime;
            var nextCheckpoint = 5.0;
            var nextConsole = 10.0;
            var waitingLogged = false;
            ProcessLoopback? capture = null;
            Process? target = null;

            try
            {
                while (!stop.IsCancellationRequested)
                {
                    if (target is null)
                    {
                        target = FindTarget(pid, processName);
                        if (target is null)
                        {
                            if (!waitingLogged)
                            {
                                _log.Line("waiting", _log.Now, pid is { } p ? $"pid {p}" : processName);
                                Console.WriteLine($"Waiting for {(pid is { } q ? $"pid {q}" : processName)}…");
                                waitingLogged = true;
                            }

                            stop.WaitHandle.WaitOne(500);
                            FillToNow(0.5);
                        }
                        else
                        {
                            capture = ProcessLoopback.Start(target.Id);
                            _lastPacket = _log.Now;
                            waitingLogged = false;
                            _log.Line("target", _log.Now, $"pid {target.Id} {target.ProcessName}");
                            Console.WriteLine($"Capturing pid {target.Id} ({target.ProcessName}).");
                        }
                    }
                    else
                    {
                        capture!.Drain(TimeSpan.FromMilliseconds(100), OnPacket);
                        if (_log.Now - _lastPacket > 1.0)
                        {
                            FillToNow(0.5);
                        }

                        if (target.HasExited)
                        {
                            capture.Dispose();
                            capture = null;
                            _log.Line("target-exited", _log.Now, $"pid {target.Id}");
                            Console.WriteLine("Target exited.");
                            target.Dispose();
                            target = null;
                        }
                    }

                    var now = _log.Now;
                    if (now >= nextCheckpoint)
                    {
                        nextCheckpoint = now + 5;
                        _wav48.Checkpoint();
                        _wav16.Checkpoint();
                        _log.Flush();
                    }

                    if (now - cpuWall >= 60)
                    {
                        process.Refresh();
                        var used = (process.TotalProcessorTime - cpuTime).TotalSeconds / (now - cpuWall) * 100;
                        _log.Line("cpu", now, string.Create(CultureInfo.InvariantCulture, $"{used:F2}\t{used / Environment.ProcessorCount:F2}"));
                        cpuWall = now;
                        cpuTime = process.TotalProcessorTime;
                    }

                    if (now >= nextConsole)
                    {
                        nextConsole = now + 10;
                        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"{TimeSpan.FromSeconds(now):hh\\:mm\\:ss}  onsets 1/2/3: {_onsets[0]}/{_onsets[1]}/{_onsets[2]}  max speech lag {_maxLagMs:F0} ms"));
                    }
                }
            }
            finally
            {
                capture?.Dispose();
                target?.Dispose();
            }
        }

        public void Finish()
        {
            FillToNow(0);
            _log.Line("end", _log.Now, Summary);
        }

        public void Dispose()
        {
            _wav48.Dispose();
            _wav16.Dispose();
            _vad.Dispose();
        }

        private static Process? FindTarget(int? pid, string processName)
        {
            if (pid is { } id)
            {
                try
                {
                    var process = Process.GetProcessById(id);
                    return process.HasExited ? null : process;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            var found = Process.GetProcessesByName(processName);
            var target = found.FirstOrDefault(p => !p.HasExited);
            foreach (var other in found.Where(p => p != target))
            {
                other.Dispose();
            }

            return target;
        }

        private void OnPacket(ReadOnlySpan<byte> frames, ulong qpcPosition100ns)
        {
            var frameCount = frames.Length / ProcessLoopback.BytesPerFrame;
            var packetStart = qpcPosition100ns == 0
                ? _log.Now - frameCount / (double)ProcessLoopback.SampleRate
                : ((long)qpcPosition100ns - _log.StartQpc100ns) / 10_000_000.0;
            _lastPacket = _log.Now;

            var gap = (long)Math.Round(packetStart * ProcessLoopback.SampleRate) - _wav48.FramesWritten;
            if (gap > GapToleranceFrames)
            {
                AppendSilence(gap);
            }
            else if (gap < -GapToleranceFrames)
            {
                _earlyPackets++;
            }

            Append(frames);
        }

        private void FillToNow(double behindSeconds)
        {
            var target = (long)((_log.Now - behindSeconds) * ProcessLoopback.SampleRate);
            var gap = target - _wav48.FramesWritten;
            if (gap > 0)
            {
                AppendSilence(gap);
            }
        }

        private void AppendSilence(long frames)
        {
            var block = _silence.Length / ProcessLoopback.BytesPerFrame;
            while (frames > 0)
            {
                var n = (int)Math.Min(frames, block);
                Append(_silence.AsSpan(0, n * ProcessLoopback.BytesPerFrame));
                frames -= n;
            }
        }

        private void Append(ReadOnlySpan<byte> frames)
        {
            _wav48.Write(frames);
            _resampled.Clear();
            _decimator.Process(frames, _resampled);

            var pcmBytes = 0;
            foreach (var sample in _resampled)
            {
                var s = (short)Math.Clamp(sample * 32768f, short.MinValue, short.MaxValue);
                _pcm16[pcmBytes++] = (byte)s;
                _pcm16[pcmBytes++] = (byte)(s >> 8);
                if (pcmBytes == _pcm16.Length)
                {
                    _wav16.Write(_pcm16);
                    pcmBytes = 0;
                }

                _chunk[_chunkFill++] = sample;
                if (_chunkFill == _chunk.Length)
                {
                    _chunkFill = 0;
                    RunChunk();
                }
            }

            _wav16.Write(_pcm16.AsSpan(0, pcmBytes));
        }

        private void RunChunk()
        {
            var probability = _vad.Probability(_chunk);
            var index = _chunkIndex++;
            var start = index * ChunkSeconds;
            var end = start + ChunkSeconds;
            var lagMs = (_log.Now - end) * 1000;
            if (lagMs > _maxLagMs && probability >= _threshold)
            {
                _maxLagMs = lagMs;
            }

            _log.Line("c", start, string.Create(CultureInfo.InvariantCulture, $"{probability:F3}\t{lagMs:F0}"));
            for (var r = 0; r < _rules.Length; r++)
            {
                var rule = _rules[r];
                switch (rule.Feed(index, probability >= _threshold))
                {
                    case 1:
                        _onsets[r]++;
                        _log.Line($"on{rule.StartChunks}", end, string.Create(CultureInfo.InvariantCulture,
                            $"first={rule.OnsetFirstChunk * ChunkSeconds:F3}\t{lagMs:F0}"));
                        break;
                    case -1:
                        _log.Line($"off{rule.StartChunks}", end, string.Create(CultureInfo.InvariantCulture,
                            $"first={rule.OffsetFirstChunk * ChunkSeconds:F3}\t{lagMs:F0}"));
                        break;
                }
            }
        }
    }
}
