using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChatterboxProbe;

/// <summary>
/// What the card is holding, and what Elite's frame time is doing while a line is spoken — the two
/// measurements #293's amended ruling turns on, since "the GPU is allowed" only settles permission.
/// <para>
/// Everything here shells out. Per-process VRAM on Windows is not in <c>nvidia-smi</c> at all under
/// WDDM, and frame time is not in any API a bystanding process can call, so the honest answers come
/// from a performance counter and from PresentMon respectively rather than from something the probe
/// could compute itself.
/// </para>
/// </summary>
internal static partial class Gpu
{
    [GeneratedRegex(@"^pid_(\d+)_luid", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessInstance();

    public sealed record Reading(string Name, int TotalMb, int UsedMb, int UtilisationPercent);

    /// <summary>Whole-card memory and utilisation, from <c>nvidia-smi</c>.</summary>
    public static IReadOnlyList<Reading> Read()
    {
        var output = Run(
            "nvidia-smi.exe",
            "--query-gpu=name,memory.total,memory.used,utilization.gpu --format=csv,noheader,nounits");

        var readings = new List<Reading>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(',', StringSplitOptions.TrimEntries);

            if (fields.Length == 4 && int.TryParse(fields[1], out var total))
            {
                readings.Add(new Reading(fields[0], total, int.Parse(fields[2]), int.Parse(fields[3])));
            }
        }

        return readings;
    }

    /// <summary>
    /// Dedicated VRAM per process, from the <c>GPU Process Memory</c> performance counter — the one
    /// place Windows will say how much of the card a named process is holding.
    /// </summary>
    public static IReadOnlyList<(string Process, int Pid, double Mb)> PerProcess()
    {
        var output = Run(
            "powershell.exe",
            "-NoProfile -Command \"(Get-Counter '\\GPU Process Memory(*)\\Dedicated Usage' " +
            "-ErrorAction SilentlyContinue).CounterSamples | Where-Object { $_.CookedValue -gt 0 } | " +
            "ForEach-Object { $_.InstanceName + ' ' + $_.CookedValue }\"");

        var totals = new Dictionary<int, double>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.LastIndexOf(' ');
            var match = ProcessInstance().Match(line);

            if (space < 0 || !match.Success ||
                !double.TryParse(line[(space + 1)..].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var bytes))
            {
                continue;
            }

            var pid = int.Parse(match.Groups[1].Value);
            totals[pid] = totals.GetValueOrDefault(pid) + bytes;
        }

        var named = new List<(string, int, double)>();

        foreach (var (pid, bytes) in totals)
        {
            string name;

            try
            {
                name = Process.GetProcessById(pid).ProcessName;
            }
            catch (ArgumentException)
            {
                continue;
            }

            named.Add((name, pid, bytes / 1024 / 1024));
        }

        return [.. named.OrderByDescending(entry => entry.Item3)];
    }

    /// <summary>Samples the card every 250 ms until disposed, and keeps the peak.</summary>
    public sealed class Watch : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _sampler;

        public int PeakUsedMb { get; private set; }

        public int PeakUtilisation { get; private set; }

        public int Samples { get; private set; }

        public Watch()
        {
            _sampler = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    foreach (var reading in Read())
                    {
                        PeakUsedMb = Math.Max(PeakUsedMb, reading.UsedMb);
                        PeakUtilisation = Math.Max(PeakUtilisation, reading.UtilisationPercent);
                    }

                    Samples++;

                    try
                    {
                        await Task.Delay(250, _stop.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }
            });
        }

        public void Dispose()
        {
            _stop.Cancel();
            _sampler.Wait(TimeSpan.FromSeconds(5));
            _stop.Dispose();
        }
    }

    public sealed record Frames(int Count, double MeanMs, double P95Ms, double P99Ms, double WorstMs);

    /// <summary>
    /// Elite's frame time over a window, through PresentMon. Not bundled: it is Intel's, MIT, one
    /// executable, and pass its path with <c>--presentmon</c>. Without it there is no frame-time
    /// number at all, and saying that is better than substituting GPU utilisation for it.
    /// </summary>
    public static Frames? FrameTimes(string presentMon, string process, int seconds)
    {
        var csv = Path.Combine(Path.GetTempPath(), $"presentmon-{Guid.NewGuid():N}.csv");

        try
        {
            Run(presentMon,
                $"--process_name {process} --output_file \"{csv}\" --timed {seconds} " +
                // --no_console_stats, not 1.x's --no_top: PresentMon 2.x rejects the old name and
                // exits rather than capturing. 2.x also needs administrative privilege or
                // membership of "Performance Log Users" to open its trace session at all.
                "--terminate_after_timed --stop_existing_session --no_console_stats");

            if (!File.Exists(csv))
            {
                return null;
            }

            var lines = File.ReadAllLines(csv);

            if (lines.Length < 2)
            {
                return null;
            }

            var headings = lines[0].Split(',');
            var column = Array.FindIndex(headings, h =>
                h.Trim().Equals("msBetweenPresents", StringComparison.OrdinalIgnoreCase) ||
                h.Trim().Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase) ||
                h.Trim().Equals("FrameTime", StringComparison.OrdinalIgnoreCase));

            if (column < 0)
            {
                return null;
            }

            var times = new List<double>();

            foreach (var line in lines.Skip(1))
            {
                var fields = line.Split(',');

                if (fields.Length > column &&
                    double.TryParse(fields[column], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var ms) && ms > 0)
                {
                    times.Add(ms);
                }
            }

            if (times.Count == 0)
            {
                return null;
            }

            times.Sort();

            return new Frames(
                times.Count,
                times.Average(),
                times[(int)(times.Count * 0.95)],
                times[Math.Min(times.Count - 1, (int)(times.Count * 0.99))],
                times[^1]);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    private static string Run(string executable, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
