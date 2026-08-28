using System.Globalization;
using System.Text;

namespace D47.Core.Tests.Logbook;

/// <summary>
/// A folder of journals written by hand, for the Commander's log tests (Phase 33).
/// <para>
/// By hand rather than replayed from the real corpus, for the reason
/// records: the 914 journals are on one machine and CI has none, so what
/// these prove is that the arithmetic is the arithmetic. A builder that aggregates forty-two jumps
/// on real data has to aggregate three on three lines of JSON.
/// </para>
/// </summary>
public sealed class JournalCorpus : IDisposable
{
    public const string Cmdr = "F1234567";

    private int _files;

    public JournalCorpus() => Directory.CreateDirectory(Folder);

    public string Folder { get; } = Path.Combine(
        Path.GetTempPath(), "d47-logbook-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Every journal written, oldest first — what the App hands the digest builder.</summary>
    public IReadOnlyList<string> Files =>
    [
        .. Directory.EnumerateFiles(Folder, "Journal.*.log").OrderBy(Path.GetFileName, StringComparer.Ordinal),
    ];

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    /// <summary>
    /// One journal file, named for the instant it starts — which is how Elite names them and what
    /// <see cref="D47.Core.Logbook.LogRanges.FilesFor"/> filters on.
    /// </summary>
    public string Journal(DateTimeOffset startedAt, params string[] lines)
    {
        var path = Path.Combine(
            Folder,
            $"Journal.{startedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HHmmss", CultureInfo.InvariantCulture)}."
            + $"{(++_files).ToString("00", CultureInfo.InvariantCulture)}.log");

        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine, Encoding.UTF8);
        return path;
    }

    /// <summary>One event line. Extra fields are pasted in as JSON, which keeps the tests readable.</summary>
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
