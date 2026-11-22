using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace D47.EliteVad;

/// <summary>
/// The run's tab-separated log. Every line starts with its kind, the run time in seconds (the offset into the
/// 16 kHz WAV) and the UTC wall time to the millisecond. Safe to call from several threads.
/// </summary>
internal sealed class RunLog : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly long _startTimestamp;
    private readonly DateTime _startUtc;
    private readonly Lock _gate = new();

    public RunLog(string path)
    {
        _writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16);
        _startTimestamp = Stopwatch.GetTimestamp();
        _startUtc = DateTime.UtcNow;
    }

    /// <summary>QPC start of the run in 100 ns units, the clock the capture packets are stamped with.</summary>
    public long StartQpc100ns => (long)(_startTimestamp * (10_000_000.0 / Stopwatch.Frequency));

    /// <summary>Seconds since the run started.</summary>
    public double Now => Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds;

    public void Line(string kind, double runSeconds, string fields)
    {
        var utc = _startUtc.AddSeconds(runSeconds).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var line = string.Create(CultureInfo.InvariantCulture, $"{kind}\t{runSeconds:F3}\t{utc}\t{fields}");
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
