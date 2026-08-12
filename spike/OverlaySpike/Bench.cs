using System;
using System.Diagnostics;
using System.Linq;

namespace OverlaySpike;

/// Per-stage timing. Everything is reported in milliseconds.
sealed class Stage
{
    readonly string _name;
    readonly System.Collections.Generic.List<double> _ms = new();
    long _t0;
    public Stage(string name) => _name = name;
    public void Begin() => _t0 = Stopwatch.GetTimestamp();
    public void End() => _ms.Add(Stopwatch.GetElapsedTime(_t0).TotalMilliseconds);
    public int Count => _ms.Count;

    public string Report(int warmup)
    {
        var s = _ms.Skip(warmup).OrderBy(x => x).ToArray();
        if (s.Length == 0) return $"{_name,-22} (no samples)";
        double Pct(double p) => s[Math.Min(s.Length - 1, (int)(p * s.Length))];
        return $"{_name,-22} n={s.Length,5}  mean={s.Average(),7:F3}  med={Pct(0.5),7:F3}  p95={Pct(0.95),7:F3}  max={s[^1],7:F3}";
    }
    public double Mean(int warmup) { var s = _ms.Skip(warmup).ToArray(); return s.Length == 0 ? 0 : s.Average(); }
}
