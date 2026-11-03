using System.Globalization;

namespace D47.Core.Audio;

/// <summary>One drop-in folder with files in it, and how many.</summary>
public sealed record OwnAudioFolder(string Group, string Name, int Count);

/// <summary>What <see cref="CueLibrary"/> found in the Commander's own audio folder, as the status rows and the page show it.</summary>
public sealed class OwnAudioReport
{
    public const string Music = "music";
    public const string Cues = "cues";
    public const string Alerts = "alerts";
    public const string Beds = "beds";

    private const int NamedInLine = 3;

    private const string Formats = ".mp3, .m4a, .aac, .wma, .flac or .wav";

    private readonly IReadOnlyList<OwnAudioFolder> _folders;

    private OwnAudioReport(IReadOnlyList<OwnAudioFolder> folders, IReadOnlyList<string> ignored, IReadOnlyList<string> skipped)
    {
        _folders = folders;
        Ignored = ignored;
        Skipped = skipped;
    }

    /// <summary>Every file that was never a candidate, as <see cref="CueLibrary.Ignored"/> words it.</summary>
    public IReadOnlyList<string> Ignored { get; }

    /// <summary>Every file that was tried and would not load.</summary>
    public IReadOnlyList<string> Skipped { get; }

    public static OwnAudioReport From(CueLibrary library) =>
        Of(library.FolderCounts, library.Ignored, library.Skipped);

    /// <summary>A report over folder counts keyed <c>group/name</c>, or <c>beds</c>.</summary>
    public static OwnAudioReport Of(IEnumerable<KeyValuePair<string, int>> folderCounts, IReadOnlyList<string> ignored, IReadOnlyList<string> skipped) =>
        new([.. folderCounts.Select(Split)], ignored, skipped);

    /// <summary>The folders of one group, most files first and then by name.</summary>
    public IReadOnlyList<OwnAudioFolder> In(string group) =>
        [.. _folders.Where(folder => folder.Group == group).OrderByDescending(folder => folder.Count).ThenBy(folder => folder.Name, StringComparer.Ordinal)];

    /// <summary>The music line: the count, then the three folders with the most files.</summary>
    public string MusicLine()
    {
        var music = In(Music);

        return music.Count == 0 ? "None of your own." : Line(music, "situation");
    }

    /// <summary>What <c>cues/</c> and <c>alerts/</c> hold.</summary>
    public string CuesLine()
    {
        var both = Combined(Cues, Alerts);

        return both.Count == 0 ? "None of your own. D47 plays its own cues." : Line(both, "cue");
    }

    /// <summary>The grey line that says how to add a cue, or null once there is one.</summary>
    public string? CuesHint() => Combined(Cues, Alerts).Count == 0 ? "A file in cues/<state> replaces one." : null;

    /// <summary>What <c>beds/</c> holds.</summary>
    public string BedsLine()
    {
        var beds = In(Beds);

        return beds.Count == 0 ? "None of your own." : Files(beds.Sum(folder => folder.Count));
    }

    /// <summary>The grey line that says how to add a bed, or null once there is one.</summary>
    public string? BedsHint() => In(Beds).Count == 0 ? "A file in beds/ adds one." : null;

    public bool HasWarning => Ignored.Count + Skipped.Count > 0;

    /// <summary>"1 file skipped" or "2 files ignored · 1 skipped".</summary>
    public string WarningLabel()
    {
        if (Ignored.Count > 0 && Skipped.Count > 0)
        {
            return $"{Files(Ignored.Count)} ignored · {Skipped.Count.ToString(CultureInfo.InvariantCulture)} skipped";
        }

        return Ignored.Count > 0 ? $"{Files(Ignored.Count)} ignored" : $"{Files(Skipped.Count)} skipped";
    }

    /// <summary>What to do about the files in the warning.</summary>
    public string WarningText()
    {
        var parts = new List<string>();

        if (Ignored.Count > 0)
        {
            parts.Add("Move each ignored file into a folder D47 reads, or convert it to " + Formats + ".");
        }

        if (Skipped.Count > 0)
        {
            parts.Add($"D47 could not read {(Skipped.Count == 1 ? "the skipped file" : "the skipped files")}. Convert {(Skipped.Count == 1 ? "it" : "them")} to " + Formats + " and drop " + (Skipped.Count == 1 ? "it" : "them") + " in again.");
        }

        return string.Join(' ', parts);
    }

    /// <summary>The first reason, and a count of the rest.</summary>
    public string WarningDetail()
    {
        var reasons = Ignored.Concat(Skipped).ToList();

        return reasons.Count switch
        {
            0 => string.Empty,
            1 => reasons[0],
            _ => $"{reasons[0]} · and {(reasons.Count - 1).ToString(CultureInfo.InvariantCulture)} more",
        };
    }

    private static OwnAudioFolder Split(KeyValuePair<string, int> entry)
    {
        var slash = entry.Key.IndexOf('/', StringComparison.Ordinal);

        return slash < 0
            ? new OwnAudioFolder(entry.Key, entry.Key, entry.Value)
            : new OwnAudioFolder(entry.Key[..slash], entry.Key[(slash + 1)..], entry.Value);
    }

    private List<OwnAudioFolder> Combined(params string[] groups) =>
        [.. groups.SelectMany(In).OrderByDescending(folder => folder.Count).ThenBy(folder => folder.Name, StringComparer.Ordinal)];

    private static string Line(IReadOnlyList<OwnAudioFolder> folders, string noun)
    {
        var total = folders.Sum(folder => folder.Count);
        var named = folders.Take(NamedInLine).Select(folder => $"{Words(folder.Name)} {folder.Count.ToString(CultureInfo.InvariantCulture)}");
        var rest = folders.Count - NamedInLine;

        return $"{Files(total)} for {folders.Count.ToString(CultureInfo.InvariantCulture)} {noun}{(folders.Count == 1 ? "" : "s")}: "
            + string.Join(", ", named)
            + (rest > 0 ? $" and {rest.ToString(CultureInfo.InvariantCulture)} more" : string.Empty);
    }

    private static string Files(int count) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} file{(count == 1 ? "" : "s")}";

    private static string Words(string folder) => folder.Replace('-', ' ');
}
