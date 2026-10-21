using System.Buffers.Binary;
using System.Globalization;

namespace D47.Core.Journal;

/// <summary>A <c>Screenshot</c> event: Elite's BMP and where it was taken.</summary>
public sealed record EliteScreenshot(string FileName, int Width, int Height, string? System, string? Body)
{
    private const string Prefix = "\\ED_Pictures\\";

    /// <summary>The event's file, size and place; null when it is not a <c>Screenshot</c> or its file is not one BMP in <c>\ED_Pictures\</c>.</summary>
    public static EliteScreenshot? TryParse(JournalEvent entry, JournalLocation fallback)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(fallback);

        if (entry.Kind != "Screenshot" || FileNameOf(entry.String("Filename")) is not { } name)
        {
            return null;
        }

        var system = Clean(entry.String("System"));
        var body = Clean(entry.String("Body"));

        if (system is null)
        {
            system = Clean(fallback.StarSystem);
            body = Clean(fallback.Body);
        }

        return new EliteScreenshot(name, entry.Raw.Int("Width") ?? 0, entry.Raw.Int("Height") ?? 0, system, ShortBody(system, body));
    }

    /// <summary>Elite's pictures folder, under the Windows Pictures known folder.</summary>
    public static string Folder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Frontier Developments", "Elite Dangerous");

    public string SourcePath(string folder) => Path.Combine(folder, FileName);

    /// <summary>The JPEG's file name; <paramref name="copy"/> above 1 adds <c> (n)</c>.</summary>
    public string Name(DateTime localTime, int copy)
    {
        var parts = new List<string> { localTime.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) };

        foreach (var part in new[] { System, Body })
        {
            if (Dropped(part) is { Length: > 0 } kept)
            {
                parts.Add(kept);
            }
        }

        var suffix = copy > 1 ? $" ({copy.ToString(CultureInfo.InvariantCulture)})" : "";

        return $"{string.Join(' ', parts)}{suffix}.jpg";
    }

    /// <summary>The BMP's <c>bfSize</c>, or null when the stream is shorter than 6 bytes or does not start with <c>BM</c>.</summary>
    public static long? ExpectedLength(Stream header)
    {
        ArgumentNullException.ThrowIfNull(header);

        Span<byte> bytes = stackalloc byte[6];
        var read = 0;

        while (read < bytes.Length)
        {
            var count = header.Read(bytes[read..]);

            if (count == 0)
            {
                return null;
            }

            read += count;
        }

        return bytes[0] == (byte)'B' && bytes[1] == (byte)'M'
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[2..])
            : null;
    }

    /// <summary>The body without a leading system name, or null when it is the system.</summary>
    internal static string? ShortBody(string? system, string? body)
    {
        if (body is null || string.Equals(body, system, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return system is not null && body.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase)
            ? Clean(body[(system.Length + 1)..])
            : body;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Dropped(string? value) =>
        value is null ? null : string.Concat(value.Split(Path.GetInvalidFileNameChars())).Trim();

    private static string? FileNameOf(string? filename)
    {
        if (filename is null || !filename.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = filename[Prefix.Length..];

        return name.Length > ".bmp".Length
            && name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("..", StringComparison.Ordinal)
            && name.IndexOfAny(['\\', '/']) < 0
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            ? name
            : null;
    }
}
