using System.Security.Cryptography;
using System.Text.RegularExpressions;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// Reads <c>catalog.tsv</c>, every Chatterbox voice. A clip that does not ship is fetched from the release
/// <see cref="Release"/> into the data folder, and used only while its size and SHA-256 match its row.
/// </summary>
public static partial class ChatterboxCatalog
{
    public const string TableName = "catalog.tsv";

    public const string Release = "chatterbox-voices-1";

    /// <summary>Where a clip is fetched from.</summary>
    public const string Host = "github.com";

    /// <summary>The largest clip a row may name; a 7 s clip at 24 kHz mono 16-bit is about 336 KB.</summary>
    private const long MaxBytes = 1024 * 1024;

    private const string Source ="https://github.com/dseelinger/d47/releases/download/" + Release + "/";

    private static readonly string[] Columns =
        ["id", "name", "gender", "locale", "pitch", "pace", "role", "source", "sha256", "bytes"];

    internal static readonly string[] Pitches = ["low", "mid", "high"];

    internal static readonly string[] Paces = ["slow", "even", "brisk"];

    /// <summary>
    /// The shipped voices first, in their own order, with their catalogue rows' bands; then every other row of
    /// <c>catalog.tsv</c> in <paramref name="folder"/>, its clip at <paramref name="fetched"/>. Each rejected row
    /// is logged with its reason.
    /// </summary>
    public static IReadOnlyList<ChatterboxVoice> Load(
        IFileSystem files, string folder, string fetched, IReadOnlyList<ChatterboxVoice> shipped, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(shipped);
        ArgumentNullException.ThrowIfNull(logger);

        var rows = Rows(files, Path.Combine(folder, TableName), fetched, logger);
        var byId = rows.ToDictionary(row => row.Voice.Id, StringComparer.Ordinal);
        var voices = new List<ChatterboxVoice>(rows.Count + shipped.Count);

        foreach (var voice in shipped)
        {
            voices.Add(byId.TryGetValue(voice.Voice.Id, out var row)
                ? voice with
                {
                    Voice = voice.Voice with { Description = row.Voice.Description },
                    Pitch = row.Pitch,
                    Pace = row.Pace,
                    Sha256 = row.Sha256,
                    Bytes = row.Bytes,
                    Shipped = true,
                }
                : voice with { Shipped = true });
        }

        var shippedIds = shipped.Select(voice => voice.Voice.Id).ToHashSet(StringComparer.Ordinal);

        voices.AddRange(rows.Where(row => !shippedIds.Contains(row.Voice.Id)));

        return voices;
    }

    /// <summary>The address a voice's clip is fetched from, or null for a shipped voice.</summary>
    public static Uri? Url(ChatterboxVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);

        return voice.Shipped ? null : new Uri(Source + voice.Voice.Id + ".wav");
    }

    /// <summary>Whether <paramref name="clip"/> is the size and SHA-256 the voice's row gives.</summary>
    public static bool Matches(ChatterboxVoice voice, ReadOnlySpan<byte> clip)
    {
        ArgumentNullException.ThrowIfNull(voice);

        return voice.Sha256 is { Length: 64 } expected
               && clip.Length == voice.Bytes
               && string.Equals(Convert.ToHexStringLower(SHA256.HashData(clip)), expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// The clip to speak a voice from: a shipped voice's own, or a fetched one whose size and SHA-256 match.
    /// A fetched file that does not match is deleted. Null when there is none to use.
    /// </summary>
    public static string? Here(IFileSystem files, ChatterboxVoice voice, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(logger);

        if (voice.Shipped)
        {
            return voice.ClipPath;
        }

        try
        {
            if (files.ReadBytes(voice.ClipPath) is not { } clip)
            {
                return null;
            }

            if (Matches(voice, clip))
            {
                return voice.ClipPath;
            }

            files.Delete(voice.ClipPath);
            logger.LogWarning(
                "Chatterbox voice {Id}: {Clip} did not match its catalog.tsv row and was deleted.", voice.Voice.Id, voice.ClipPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Chatterbox voice {Id}: {Clip} could not be read: {Reason}", voice.Voice.Id, voice.ClipPath, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// The shipped voice that speaks for <paramref name="wanted"/> while its clip is not here: of the same gender, then
    /// cast in <paramref name="role"/>, then nearest in pitch and pace. Null when nothing ships.
    /// </summary>
    public static ChatterboxVoice? StandIn(ChatterboxVoice wanted, VoiceRole? role, IEnumerable<ChatterboxVoice> voices)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(voices);

        var shipped = voices.Where(voice => voice.Shipped).ToList();
        var sameGender = shipped
            .Where(voice => string.Equals(voice.Voice.Gender, wanted.Voice.Gender, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return (sameGender.Count > 0 ? sameGender : shipped)
            .OrderBy(voice => role is not null && voice.Role == role ? 0 : 1)
            .ThenBy(voice => Distance(Band(voice.Pitch, Pitches), Band(wanted.Pitch, Pitches))
                             + Distance(Band(voice.Pace, Paces), Band(wanted.Pace, Paces)))
            .FirstOrDefault();
    }

    private static int Band(string? value, string[] bands) =>
        Array.IndexOf(bands, value) is var index and >= 0 ? index : 1;

    private static int Distance(int a, int b) => Math.Abs(a - b);

    private static List<ChatterboxVoice> Rows(IFileSystem files, string table, string fetched, ILogger logger)
    {
        if (files.ReadText(table) is not { } text)
        {
            logger.LogWarning("Chatterbox voices: {Table} is missing.", table);
            return [];
        }

        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).ToList();

        if (lines.Count == 0 || !lines[0].Split('\t').SequenceEqual(Columns))
        {
            logger.LogWarning(
                "Chatterbox voices: {Table} does not start with the columns {Columns}.", table, string.Join(", ", Columns));
            return [];
        }

        var rows = new List<ChatterboxVoice>(lines.Count - 1);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split('\t');

            if (fields.Length != Columns.Length)
            {
                logger.LogWarning(
                    "Chatterbox voices: row \"{Row}\" has {Count} columns, not {Expected}; left out.", line, fields.Length, Columns.Length);
                continue;
            }

            if (Reject(fields, seen, out var role, out var bytes) is { } reason)
            {
                logger.LogWarning("Chatterbox voices: {Id} left out of {Table}: {Reason}", fields[0], TableName, reason);
                continue;
            }

            var (id, name, gender, locale, pitch, pace) = (fields[0], fields[1], fields[2], fields[3], fields[4], fields[5]);

            rows.Add(new ChatterboxVoice(
                new VoiceInfo(id, name, locale, gender) { Description = $"{pitch} pitch, {pace} pace" },
                role,
                fields[7],
                Path.Combine(fetched, id + ".wav"))
            {
                Shipped = false,
                Pitch = pitch,
                Pace = pace,
                Sha256 = fields[8],
                Bytes = bytes,
            });
        }

        return rows;
    }

    private static string? Reject(string[] fields, HashSet<string> seen, out VoiceRole? role, out long bytes)
    {
        role = null;
        bytes = 0;

        if (!SafeId().IsMatch(fields[0]))
        {
            return "the id is not lower-case letters, digits and hyphens.";
        }

        if (string.Equals(fields[0], OwnVoice.VoiceId, StringComparison.OrdinalIgnoreCase))
        {
            return $"the id \"{OwnVoice.VoiceId}\" is the Commander's own recorded voice.";
        }

        if (fields[0].StartsWith(CustomVoices.IdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"the id prefix \"{CustomVoices.IdPrefix}\" is for the Commander's custom voices.";
        }

        if (!seen.Add(fields[0]))
        {
            return "the id is listed twice.";
        }

        if (fields[1].Length == 0 || string.IsNullOrWhiteSpace(fields[7]))
        {
            return "the name or the source is empty.";
        }

        if (!Pitches.Contains(fields[4]) || !Paces.Contains(fields[5]))
        {
            return $"the pitch \"{fields[4]}\" or the pace \"{fields[5]}\" is not a band.";
        }

        if (fields[6].Length > 0)
        {
            if (!Enum.TryParse<VoiceRole>(fields[6], out var parsed) || !Enum.IsDefined(parsed))
            {
                return $"the role \"{fields[6]}\" is not a voice role.";
            }

            role = parsed;
        }

        if (!Sha256Hex().IsMatch(fields[8]))
        {
            return "the sha256 is not 64 lower-case hex digits.";
        }

        if (!long.TryParse(fields[9], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out bytes)
            || bytes is <= 0 or > MaxBytes)
        {
            return $"the size \"{fields[9]}\" is not a whole number of bytes up to {MaxBytes}.";
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex SafeId();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex();
}
