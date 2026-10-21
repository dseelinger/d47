using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Knowledge;

namespace D47.Core.Interface;

/// <summary>A codex picture's identity: the journal EntryID and the codex region number.</summary>
public readonly partial record struct CodexPictureKey(long EntryId, int RegionId)
{
    /// <summary>The stored file name, <c>&lt;entryId&gt;-&lt;regionId&gt;.jpg</c>.</summary>
    public string FileName => string.Create(CultureInfo.InvariantCulture, $"{EntryId}-{RegionId}.jpg");

    /// <summary>The kept capture the picture was cropped from, <c>&lt;entryId&gt;-&lt;regionId&gt;.source.jpg</c>.</summary>
    public string SourceFileName => string.Create(CultureInfo.InvariantCulture, $"{EntryId}-{RegionId}.source.jpg");

    /// <summary>The square cut from the source, <c>&lt;entryId&gt;-&lt;regionId&gt;.crop.json</c>.</summary>
    public string CropFileName => string.Create(CultureInfo.InvariantCulture, $"{EntryId}-{RegionId}.crop.json");

    /// <summary>Reads back a name <see cref="FileName"/> wrote; false for any other name.</summary>
    public static bool TryParse(string? fileName, out CodexPictureKey key)
    {
        key = default;

        if (fileName is null || !NameShape().IsMatch(fileName))
        {
            return false;
        }

        var parts = fileName[..^4].Split('-');

        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var entryId)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var regionId)
            || entryId <= 0
            || GalacticRegions.NameOf(regionId) is null)
        {
            return false;
        }

        var candidate = new CodexPictureKey(entryId, regionId);

        if (!string.Equals(candidate.FileName, fileName, StringComparison.Ordinal))
        {
            return false;
        }

        key = candidate;
        return true;
    }

    [GeneratedRegex(@"^[0-9]+-[0-9]+\.jpg$")]
    private static partial Regex NameShape();
}

/// <summary>One stock picture as <c>index.json</c> lists it.</summary>
public sealed record CodexPictureRow(long EntryId, int RegionId, string Tag, string Asset, string Sha256, long Bytes)
{
    public CodexPictureKey Key => new(EntryId, RegionId);
}

/// <summary>The rows <see cref="CodexPictureIndex.Parse"/> kept and a reason for each one it left out.</summary>
public sealed record CodexPictureIndexParse(IReadOnlyList<CodexPictureRow> Rows, IReadOnlyList<string> Reasons);

/// <summary>The stock picture index: <c>index.json</c> in the codex pictures repository.</summary>
public static partial class CodexPictureIndex
{
    public const string Repository = "dseelinger/d47-codex-pictures";

    public const string IndexName = "index.json";

    public const string TagPrefix = "codex-pictures-";

    public const string FirstTag = TagPrefix + "1";

    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Reads an index; a row that fails a check is left out with the reason.</summary>
    public static CodexPictureIndexParse Parse(string json)
    {
        var rows = new List<CodexPictureRow>();
        var reasons = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new CodexPictureIndexParse(rows, ["The index is not a JSON array."]);
            }

            var seen = new HashSet<CodexPictureKey>();
            var position = 0;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                position++;

                var reason = ReadRow(element, out var row);

                if (reason is null && !seen.Add(row!.Key))
                {
                    reason = $"the entry {row.EntryId} in region {row.RegionId} is listed twice.";
                }

                if (reason is null)
                {
                    rows.Add(row!);
                }
                else
                {
                    reasons.Add($"Row {position}: {reason}");
                }
            }
        }
        catch (JsonException ex)
        {
            return new CodexPictureIndexParse([], [$"The index is not valid JSON: {ex.Message}"]);
        }

        return new CodexPictureIndexParse(rows, reasons);
    }

    /// <summary>The index text for <paramref name="rows"/>, sorted by EntryID then region.</summary>
    public static string Write(IEnumerable<CodexPictureRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ordered = rows
            .OrderBy(row => row.EntryId)
            .ThenBy(row => row.RegionId)
            .Select(row => new Dto(row.EntryId, row.RegionId, row.Tag, row.Asset, row.Sha256, row.Bytes));

        return JsonSerializer.Serialize(ordered, WriteOptions);
    }

    /// <summary>A copy of <paramref name="rows"/> with <paramref name="row"/> replacing the row of the same key, or added.</summary>
    public static IReadOnlyList<CodexPictureRow> Upsert(IEnumerable<CodexPictureRow> rows, CodexPictureRow row)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(row);

        return [.. rows.Where(existing => existing.Key != row.Key), row];
    }

    /// <summary>A copy of <paramref name="rows"/> without the row of <paramref name="key"/>.</summary>
    public static IReadOnlyList<CodexPictureRow> Remove(IEnumerable<CodexPictureRow> rows, CodexPictureKey key)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return [.. rows.Where(existing => existing.Key != key)];
    }

    /// <summary>The release asset name: the key and the first 12 hex digits of the SHA-256.</summary>
    public static string AssetName(long entryId, int regionId, string sha256)
    {
        ArgumentNullException.ThrowIfNull(sha256);

        return string.Create(CultureInfo.InvariantCulture, $"{entryId}-{regionId}-{sha256[..Math.Min(12, sha256.Length)]}.jpg");
    }

    /// <summary>Whether <paramref name="picture"/> is the size and SHA-256 the row gives.</summary>
    public static bool Matches(CodexPictureRow row, ReadOnlySpan<byte> picture)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Sha256 is { Length: 64 } expected
               && picture.Length == row.Bytes
               && string.Equals(Convert.ToHexStringLower(SHA256.HashData(picture)), expected, StringComparison.Ordinal);
    }

    private static string? ReadRow(JsonElement element, out CodexPictureRow? row)
    {
        row = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return "it is not an object.";
        }

        if (!TryGetInt64(element, "entryId", out var entryId) || entryId <= 0)
        {
            return "the entryId is not a positive whole number.";
        }

        if (!TryGetInt64(element, "regionId", out var regionId)
            || regionId is < int.MinValue or > int.MaxValue
            || GalacticRegions.NameOf((int)regionId) is null)
        {
            return $"the region of entry {entryId} is not a known region number.";
        }

        var tag = GetString(element, "tag");

        if (tag is null || !tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return $"the tag of entry {entryId} does not start with \"{TagPrefix}\".";
        }

        var asset = GetString(element, "asset");

        if (asset is null || !SafeAsset().IsMatch(asset))
        {
            return $"the asset of entry {entryId} is not a safe .jpg name.";
        }

        var sha256 = GetString(element, "sha256");

        if (sha256 is null || !Sha256Hex().IsMatch(sha256))
        {
            return $"the sha256 of entry {entryId} is not 64 lower-case hex digits.";
        }

        if (!TryGetInt64(element, "bytes", out var bytes) || bytes is < 1 or > MaxBytes)
        {
            return $"the size of entry {entryId} is not a whole number of bytes from 1 to {MaxBytes}.";
        }

        row = new CodexPictureRow(entryId, (int)regionId, tag, asset, sha256, bytes);
        return null;
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;

        return element.TryGetProperty(name, out var property)
               && property.ValueKind == JsonValueKind.Number
               && property.TryGetInt64(out value);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private sealed record Dto(long EntryId, int RegionId, string Tag, string Asset, string Sha256, long Bytes);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,63}\.jpg$")]
    private static partial Regex SafeAsset();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex();
}

/// <summary>Where a codex picture comes from.</summary>
public enum CodexPictureSource
{
    Local,
    Stock,
}

/// <summary>The picture <see cref="CodexPictures.Find"/> chose; <see cref="Row"/> is set for a stock picture.</summary>
public sealed record CodexPictureChoice(CodexPictureSource Source, CodexPictureKey Key, bool FromOtherRegion, CodexPictureRow? Row)
{
    public int RegionId => Key.RegionId;
}

public static class CodexPictures
{
    /// <summary>
    /// The picture to show for an entry: the Commander's for the region, then stock for the region, then the
    /// Commander's from the lowest other region, then stock from the lowest other region; null for none.
    /// With no region only the other-region steps apply. Another EntryID's picture is never used.
    /// </summary>
    public static CodexPictureChoice? Find(
        long entryId,
        int? regionId,
        IReadOnlySet<CodexPictureKey> local,
        IReadOnlyList<CodexPictureRow> stock)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(stock);

        if (regionId is { } here)
        {
            var key = new CodexPictureKey(entryId, here);

            if (local.Contains(key))
            {
                return new CodexPictureChoice(CodexPictureSource.Local, key, false, null);
            }

            if (stock.FirstOrDefault(row => row.Key == key) is { } same)
            {
                return new CodexPictureChoice(CodexPictureSource.Stock, key, false, same);
            }
        }

        var otherLocal = local
            .Where(key => key.EntryId == entryId && key.RegionId != regionId)
            .OrderBy(key => key.RegionId)
            .Select(key => (CodexPictureKey?)key)
            .FirstOrDefault();

        if (otherLocal is { } localKey)
        {
            return new CodexPictureChoice(CodexPictureSource.Local, localKey, true, null);
        }

        var otherStock = stock
            .Where(row => row.EntryId == entryId && row.RegionId != regionId)
            .OrderBy(row => row.RegionId)
            .FirstOrDefault();

        return otherStock is null
            ? null
            : new CodexPictureChoice(CodexPictureSource.Stock, otherStock.Key, true, otherStock);
    }
}

/// <summary>The maintainer's token for publishing stock codex pictures: the stored secret, else the environment variable.</summary>
public static class CodexPublishToken
{
    public const string SecretName = "codex-publish-token";

    public const string Variable = "D47_CODEX_PUBLISH_TOKEN";

    /// <summary>The stored token, else <see cref="Variable"/>; null when neither holds one.</summary>
    public static string? Resolve(string? stored, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var fromEnvironment = environment(Variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
    }

    /// <summary>Whether a token is stored or in <see cref="Variable"/>.</summary>
    public static bool Present(bool stored, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return stored || !string.IsNullOrWhiteSpace(environment(Variable));
    }
}
