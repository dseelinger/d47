using System.Globalization;
using D47.Core.Storage;

namespace D47.Core.Tests.Logbook;

/// <summary>A folder of journals written by hand, for the Commander's log tests.</summary>
public sealed class JournalCorpus
{
    public const string Cmdr = "F1234567";

    private int _files;

    public MemoryFileSystem FileSystem { get; } = new();

    public string Folder { get; } = @"C:\d47-test\journals";

    /// <summary>Every journal written, oldest first — what the App hands the digest builder.</summary>
    public IReadOnlyList<string> Files =>
    [
        .. FileSystem.Enumerate(Folder, "Journal.*.log").OrderBy(Path.GetFileName, StringComparer.Ordinal),
    ];

    /// <summary>
    /// One journal file, named for the instant it starts — which is how Elite names them and what <see
    /// cref="D47.Core.Logbook.LogRanges.FilesFor"/> filters on.
    /// </summary>
    public string Journal(DateTimeOffset startedAt, params string[] lines)
    {
        var path = Path.Combine(
            Folder,
            $"Journal.{startedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HHmmss", CultureInfo.InvariantCulture)}."
            + $"{(++_files).ToString("00", CultureInfo.InvariantCulture)}.log");

        FileSystem.WriteText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return path;
    }

    /// <summary>One event line.</summary>
    public static string Event(DateTimeOffset at, string kind, string? fields = null) =>
        $$"""{"timestamp":"{{at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}}","event":"{{kind}}"{{(fields is null ? string.Empty : "," + fields)}}}""";

    public static string LoadGame(DateTimeOffset at) =>
        Event(
            at,
            "LoadGame",
            $"\"Name\":\"Jameson\",\"FID\":\"{Cmdr}\",\"Ship\":\"Python\",\"Ship_Localised\":\"Python\","
            + "\"ShipName\":\"Directive\",\"Credits\":1000000");

    public static string Jump(DateTimeOffset at, string system, double distance) =>
        Event(at, "FSDJump", $"\"StarSystem\":\"{system}\",\"JumpDist\":{distance.ToString("0.00", CultureInfo.InvariantCulture)}");
}
