using System.Globalization;
using System.Text;
using System.Text.Json;
using MirrorProbe;
using OpenCvSharp;

// spike/MirrorProbe — list.md Phase 22, "Spike: is there anything in the mirror to read".
//
// Three verbs and no cleverness:
//
//   windows                            what is on screen and how each window is framed
//   capture  --label supercruise-left  a frame down all three capture paths, plus a sidecar
//   analyse  --panel p.png --against … can the panel be found in the other frames
//
// Everything it decides, it decides from numbers it prints. Nothing here writes a finding; the
// finding is written by hand into docs/spikes/, from the report this produces.

Native.SetProcessDpiAwareness(2);

var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var options = Options.Parse(args);

try
{
    return verb switch
    {
        "windows" => ListWindows(options),
        "capture" => Capture(options),
        "analyse" or "analyze" => Analyse(options),
        _ => Help(),
    };
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static int Help()
{
    Console.WriteLine(
        """
        MirrorProbe — is there anything in Elite's desktop mirror to read?

          windows [--match <text>]
              Lists visible top-level windows with their framing, so the one carrying the mirror
              can be identified before anything is captured. In VR there is more than one
              candidate: Elite's own window and SteamVR's view are different pictures.

          capture --label <name> [--note <text>] [--match <text>] [--hwnd <0x…>]
                  [--delay <seconds>] [--out <directory>]
              Captures one frame down all three paths — Windows Graphics Capture, a GDI blit off
              the screen, and PrintWindow — and writes each beside a sidecar recording the window,
              its framing, and what Status.json says is on screen. --delay counts down first, which
              is how a frame gets taken with a headset on.

              --note is not optional in practice. Nothing this probe can read says whether Elite was
              rendering to a headset or which way the head was pointing, and neither is recoverable
              from the frame afterwards, so "vr, head straight ahead" belongs in the capture rather
              than in somebody's memory.

          analyse --panel <png> [--rect x,y,w,h] --against <png|directory>[,…]
                  [--detector orb|sift|akaze|brisk] [--out <report.md>]
              Measures the panel region, then tries to find it in every other frame. Writes a
              markdown report and an annotated copy of each frame showing where it thinks the panel
              landed. --rect crops the panel out of --panel first.

        A capture path that fails, or a frame that comes back black, is a finding and is reported
        as one. Nothing here is retried until it works.
        """);

    return 0;
}

static int ListWindows(Options options)
{
    var match = options.Value("match");

    foreach (var window in Native.TopLevelWindows())
    {
        if (match is not null && !Matches(window, match))
        {
            continue;
        }

        if (window.Title.Length == 0 && match is null)
        {
            continue;
        }

        Console.WriteLine($"0x{window.Handle.ToInt64():X}  {Native.Describe(window)}");
    }

    var status = GameStatus.Read();
    Console.WriteLine();
    Console.WriteLine($"Status.json: {status.Problem ?? $"GuiFocus {status.GuiFocus} — {status.Focus}"}");

    return 0;
}

static bool Matches(Native.WindowInfo window, string text) =>
    window.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
    || window.ClassName.Contains(text, StringComparison.OrdinalIgnoreCase)
    || window.ProcessName.Contains(text, StringComparison.OrdinalIgnoreCase);

static int Capture(Options options)
{
    var label = options.Value("label") ?? "frame";
    var directory = options.Value("out") ?? "captures";
    Directory.CreateDirectory(directory);

    var window = Resolve(options);

    if (window is null)
    {
        Console.Error.WriteLine("No window matched. Run `MirrorProbe windows` to see what is there.");
        return 1;
    }

    if (options.Value("delay") is { } delayText && int.TryParse(delayText, out var delay) && delay > 0)
    {
        for (var remaining = delay; remaining > 0; remaining--)
        {
            Console.Write($"\r{remaining} seconds — hold the head angle…   ");
            Thread.Sleep(1000);
        }

        Console.WriteLine();
    }

    Console.WriteLine(Native.Describe(window));

    var status = GameStatus.Read();
    var results = new List<object>();

    Mat? wgc = null;

    try
    {
        wgc = WgcCapture.Capture(window.Handle, out var note);
        results.Add(Save(directory, label, "wgc", wgc, note));
    }
    finally
    {
        wgc?.Dispose();
    }

    using (var screen = GdiCapture.FromScreen(window))
    {
        results.Add(Save(directory, label, "gdi-screen", screen, screen is null ? "The blit failed." : string.Empty));
    }

    using (var print = GdiCapture.FromPrintWindow(window))
    {
        results.Add(Save(directory, label, "printwindow", print, print is null ? "PrintWindow declined." : string.Empty));
    }

    var sidecar = new
    {
        label,

        // What the label cannot carry and nothing on this machine can recover afterwards: whether
        // the headset was on, and which head angle this is. Nothing d47 or Windows reads says
        // whether Elite is rendering to a headset, so if the person taking the measurement does not
        // write it down here, the frame is unclassifiable by the time it is looked at.
        note = options.Value("note"),
        takenUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        window = new
        {
            handle = $"0x{window.Handle.ToInt64():X}",
            window.Title,
            window.ClassName,
            window.ProcessName,
            window.ProcessId,
            windowRect = window.WindowRect.ToString(),
            clientRect = window.ClientRect.ToString(),
            monitorRect = window.MonitorRect.ToString(),
            window.Framing,
            style = $"0x{window.Style:X}",
            exStyle = $"0x{window.ExStyle:X}",
        },
        status = new
        {
            status.GuiFocus,
            status.Focus,
            status.Timestamp,
            status.Problem,
        },
        captures = results,
    };

    var json = Path.Combine(directory, $"{label}.json");
    File.WriteAllText(json, JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Status.json: {status.Problem ?? $"GuiFocus {status.GuiFocus} — {status.Focus}"}");
    Console.WriteLine($"wrote {json}");

    return 0;
}

static object Save(string directory, string label, string method, Mat? image, string note)
{
    if (image is null || image.Empty())
    {
        Console.WriteLine($"  {method,-12} no frame — {note}");
        return new { method, captured = false, note };
    }

    var path = Path.Combine(directory, $"{label}.{method}.png");
    Cv2.ImWrite(path, image);

    using var grey = new Mat();
    Cv2.CvtColor(image, grey, ColorConversionCodes.BGR2GRAY);
    Cv2.MinMaxLoc(grey, out double _, out double brightest);
    Cv2.MeanStdDev(grey, out var mean, out var deviation);

    // A capture path that "worked" and returned an empty picture is the failure most likely to be
    // read as a success, so blankness is measured rather than eyeballed.
    var blank = brightest < 8;

    Console.WriteLine(
        $"  {method,-12} {image.Width}x{image.Height}  mean {mean.Val0,6:F1}  sd {deviation.Val0,6:F1}  "
        + $"max {brightest,3:F0}{(blank ? "  ← black frame" : string.Empty)}");

    return new
    {
        method,
        captured = true,
        file = Path.GetFileName(path),
        image.Width,
        image.Height,
        mean = Math.Round(mean.Val0, 2),
        standardDeviation = Math.Round(deviation.Val0, 2),
        brightest,
        blank,
        note,
    };
}

static Native.WindowInfo? Resolve(Options options)
{
    var windows = Native.TopLevelWindows();

    if (options.Value("hwnd") is { } handleText)
    {
        var handle = handleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(handleText[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(handleText, CultureInfo.InvariantCulture);

        return windows.FirstOrDefault(window => window.Handle.ToInt64() == handle);
    }

    var match = options.Value("match") ?? "elite";

    return windows
        .Where(window => Matches(window, match))
        .OrderByDescending(window => window.WindowRect.Width * (long)window.WindowRect.Height)
        .FirstOrDefault();
}

static int Analyse(Options options)
{
    var panelPath = options.Value("panel") ?? throw new ArgumentException("--panel is required.");
    var against = options.Value("against") ?? throw new ArgumentException("--against is required.");
    var detectorName = options.Value("detector") ?? "orb";
    var reportPath = options.Value("out") ?? "report.md";

    using var panelSource = Cv2.ImRead(panelPath, ImreadModes.Color);

    if (panelSource.Empty())
    {
        throw new ArgumentException($"Could not read {panelPath}.");
    }

    using var panel = Crop(panelSource, options.Value("rect"));

    var cropPath = Path.Combine(Path.GetDirectoryName(reportPath) is { Length: > 0 } d ? d : ".", "panel.png");
    Cv2.ImWrite(cropPath, panel);

    using var detector = Matching.Detector(detectorName);

    var baseline = Matching.Measure(panel, detector);
    var source = Path.GetFullPath(panelPath);
    var targets = Expand(against).ToArray();
    var matches = new List<Matching.Match>();

    Console.WriteLine($"panel {baseline.Width}x{baseline.Height}  {baseline.Keypoints} keypoints  "
        + $"sharpness {baseline.Sharpness:F0}  contrast {baseline.Contrast:F1}");
    Console.WriteLine();

    foreach (var file in targets)
    {
        using var target = Cv2.ImRead(file, ImreadModes.Color);

        if (target.Empty())
        {
            Console.WriteLine($"{Path.GetFileName(file),-40} unreadable");
            continue;
        }

        // The frame the panel was cut out of is deliberately left in the set rather than skipped.
        // It is the control: if the panel cannot be found in the picture it came from, the fault is
        // in this probe and not in the mirror, and that is worth being told loudly.
        var control = string.Equals(Path.GetFullPath(file), source, StringComparison.OrdinalIgnoreCase);
        var name = Path.GetFileName(file) + (control ? "  (source frame, control)" : string.Empty);

        var match = Matching.Find(panel, target, name, detector);
        matches.Add(match);

        Console.WriteLine(
            $"{match.Target,-40} {match.Verdict,-12} inliers {match.Inliers,4}/{match.GoodMatches,-4} "
            + $"({match.InlierRatio:P0})  reprojection {match.ReprojectionError:F2}px");

        using var annotated = Matching.Annotate(target, match.Quad, $"{match.Verdict}  {match.Inliers} inliers");
        Cv2.ImWrite(Path.Combine(Path.GetDirectoryName(file) ?? ".", $"located.{Path.GetFileName(file)}"), annotated);

        if (control && match.Verdict != "located")
        {
            Console.Error.WriteLine(
                "The control failed: the panel was not found in the frame it was cut out of. Nothing "
                + "below this line means anything until that is explained.");
        }
    }

    File.WriteAllText(reportPath, Report(panelPath, options.Value("rect"), detectorName, baseline, matches));
    Console.WriteLine();
    Console.WriteLine($"wrote {reportPath}");

    return 0;
}

static Mat Crop(Mat image, string? rect)
{
    if (rect is null)
    {
        return image.Clone();
    }

    var parts = rect.Split(',', StringSplitOptions.TrimEntries);

    if (parts.Length != 4 || !parts.All(part => int.TryParse(part, out _)))
    {
        throw new ArgumentException("--rect wants four integers: x,y,width,height.");
    }

    var numbers = parts.Select(int.Parse).ToArray();
    var region = new Rect(numbers[0], numbers[1], numbers[2], numbers[3]);

    if (region.X < 0 || region.Y < 0 || region.Right > image.Width || region.Bottom > image.Height)
    {
        throw new ArgumentException($"--rect falls outside the {image.Width}x{image.Height} image.");
    }

    return new Mat(image, region).Clone();
}

static IEnumerable<string> Expand(string against) => against
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .SelectMany(entry => Directory.Exists(entry)
        ? Directory.EnumerateFiles(entry, "*.png").Where(file => !IsProbeOutput(file))
        : [entry])
    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

// The annotated frames and the saved crop are this probe's own output. Scanning them back in
// produces rows that look like measurements — a crop matches itself at 100% — and are not.
static bool IsProbeOutput(string file) =>
    Path.GetFileName(file).StartsWith("located.", StringComparison.Ordinal)
    || string.Equals(Path.GetFileName(file), "panel.png", StringComparison.OrdinalIgnoreCase);

static string Report(
    string panelPath,
    string? rect,
    string detector,
    Matching.Baseline baseline,
    IReadOnlyList<Matching.Match> matches)
{
    var report = new StringBuilder();

    report.AppendLine("# MirrorProbe report");
    report.AppendLine();
    report.AppendLine(CultureInfo.InvariantCulture, $"Taken {DateTime.UtcNow:u}. Detector `{detector}`.");
    report.AppendLine();
    report.AppendLine(CultureInfo.InvariantCulture, $"Panel from `{panelPath}`{(rect is null ? " (whole image)" : $", rect `{rect}`")}.");
    report.AppendLine();
    report.AppendLine("## Is there anything there at all");
    report.AppendLine();
    report.AppendLine("| | |");
    report.AppendLine("|---|---|");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Panel size | {baseline.Width}x{baseline.Height} |");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Keypoints | {baseline.Keypoints} |");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Keypoints per megapixel | {baseline.KeypointsPerMegapixel:F0} |");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Sharpness (variance of Laplacian) | {baseline.Sharpness:F0} |");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Mean luma | {baseline.MeanLuma:F1} |");
    report.AppendLine(CultureInfo.InvariantCulture, $"| Contrast (standard deviation) | {baseline.Contrast:F1} |");
    report.AppendLine();
    report.AppendLine("## Could it be found again");
    report.AppendLine();
    report.AppendLine("| Frame | Verdict | Good matches | Inliers | Ratio | Reprojection | Note |");
    report.AppendLine("|---|---|---:|---:|---:|---:|---|");

    foreach (var match in matches)
    {
        report.AppendLine(CultureInfo.InvariantCulture,
            $"| `{match.Target}` | {match.Verdict} | {match.GoodMatches} | {match.Inliers} | "
            + $"{match.InlierRatio:P0} | {match.ReprojectionError:F2}px | {match.Note} |");
    }

    report.AppendLine();
    report.AppendLine(CultureInfo.InvariantCulture,
        $"{matches.Count(m => m.Verdict == "located")} of {matches.Count} frames located the panel.");
    report.AppendLine();
    report.AppendLine("Each frame has an annotated copy beside it, `located.<name>.png`, with the projected");
    report.AppendLine("quadrilateral drawn on. **Look at them.** A homography can produce a confident number and");
    report.AppendLine("a quadrilateral in the wrong place, and that combination is exactly the failure mode this");
    report.AppendLine("phase is written against, so the numbers above are not the whole answer on their own.");

    return report.ToString();
}

internal sealed class Options
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    internal static Options Parse(string[] args)
    {
        var options = new Options();

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";

            options.values[name] = value;
        }

        return options;
    }

    internal string? Value(string name) => values.TryGetValue(name, out var value) ? value : null;
}
