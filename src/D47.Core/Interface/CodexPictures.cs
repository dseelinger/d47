using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Knowledge;

namespace D47.Core.Interface;

/// <summary>A codex picture's identity: the <see cref="CodexKeys">codex key</see> and the codex region number.</summary>
public readonly record struct CodexPictureKey(string Key, int RegionId)
{
    /// <summary>The journal EntryID of a Canonn entry's key; null for a d47 kind.</summary>
    public long? EntryId => CodexKeys.EntryIdOf(Key);

    /// <summary>The stored file name, <c>&lt;key&gt;-&lt;regionId&gt;.jpg</c>.</summary>
    public string FileName => string.Create(CultureInfo.InvariantCulture, $"{Key}-{RegionId}.jpg");

    /// <summary>The kept capture the picture was cropped from, <c>&lt;key&gt;-&lt;regionId&gt;.source.jpg</c>.</summary>
    public string SourceFileName => string.Create(CultureInfo.InvariantCulture, $"{Key}-{RegionId}.source.jpg");

    /// <summary>The square cut from the source, <c>&lt;key&gt;-&lt;regionId&gt;.crop.json</c>.</summary>
    public string CropFileName => string.Create(CultureInfo.InvariantCulture, $"{Key}-{RegionId}.crop.json");

    /// <summary>Reads back a name <see cref="FileName"/> wrote, the region after the last <c>-</c>; false for any other name.</summary>
    public static bool TryParse(string? fileName, out CodexPictureKey key)
    {
        key = default;

        if (fileName is null || !fileName.EndsWith(".jpg", StringComparison.Ordinal))
        {
            return false;
        }

        var stem = fileName[..^4];
        var dash = stem.LastIndexOf('-');

        if (dash < 0
            || !CodexKeys.IsValid(stem[..dash])
            || !int.TryParse(stem[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var regionId)
            || GalacticRegions.NameOf(regionId) is null)
        {
            return false;
        }

        var candidate = new CodexPictureKey(stem[..dash], regionId);

        if (!string.Equals(candidate.FileName, fileName, StringComparison.Ordinal))
        {
            return false;
        }

        key = candidate;
        return true;
    }
}

/// <summary>One stock picture as <c>index.json</c> lists it, by its <see cref="CodexKeys">codex key</see> and region.</summary>
public sealed record CodexPictureRow(string EntryKey, int RegionId, string Tag, string Asset, string Sha256, long Bytes)
{
    public CodexPictureKey Key => new(EntryKey, RegionId);
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
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
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
                    reason = $"the entry {row.EntryKey} in region {row.RegionId} is listed twice.";
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

    /// <summary>
    /// The index text for <paramref name="rows"/>: <c>entryId</c> for a numeric key and <c>kind</c> otherwise, sorted by
    /// EntryID, then kind key, then region.
    /// </summary>
    public static string Write(IEnumerable<CodexPictureRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ordered = rows
            .Select(row => (Row: row, EntryId: CodexKeys.EntryIdOf(row.EntryKey)))
            .OrderBy(item => item.EntryId is null)
            .ThenBy(item => item.EntryId)
            .ThenBy(item => item.Row.EntryKey, StringComparer.Ordinal)
            .ThenBy(item => item.Row.RegionId)
            .Select(item => new Dto(
                item.EntryId,
                item.EntryId is null ? item.Row.EntryKey : null,
                item.Row.RegionId,
                item.Row.Tag,
                item.Row.Asset,
                item.Row.Sha256,
                item.Row.Bytes));

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
    public static string AssetName(CodexPictureKey key, string sha256)
    {
        ArgumentNullException.ThrowIfNull(sha256);

        return string.Create(CultureInfo.InvariantCulture, $"{key.Key}-{key.RegionId}-{sha256[..Math.Min(12, sha256.Length)]}.jpg");
    }

    /// <summary>Whether <paramref name="asset"/> is a release asset name a row may give: a key's characters and <c>-</c>, up to 128 before <c>.jpg</c>, no <c>..</c>.</summary>
    public static bool IsSafeAsset([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? asset) => asset is not null && SafeAsset().IsMatch(asset);

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

        string entry;

        if (element.TryGetProperty("kind", out _))
        {
            if (GetString(element, "kind") is not { } kind || !CodexKeys.IsKind(kind))
            {
                return "the kind is not a d47 kind key.";
            }

            entry = kind;
        }
        else if (TryGetInt64(element, "entryId", out var entryId) && entryId > 0)
        {
            entry = CodexKeys.Of(entryId);
        }
        else
        {
            return "the entryId is not a positive whole number.";
        }

        if (!TryGetInt64(element, "regionId", out var regionId)
            || regionId is < int.MinValue or > int.MaxValue
            || GalacticRegions.NameOf((int)regionId) is null)
        {
            return $"the region of entry {entry} is not a known region number.";
        }

        var tag = GetString(element, "tag");

        if (tag is null || !tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return $"the tag of entry {entry} does not start with \"{TagPrefix}\".";
        }

        var asset = GetString(element, "asset");

        if (!IsSafeAsset(asset))
        {
            return $"the asset of entry {entry} is not a safe .jpg name.";
        }

        var sha256 = GetString(element, "sha256");

        if (sha256 is null || !Sha256Hex().IsMatch(sha256))
        {
            return $"the sha256 of entry {entry} is not 64 lower-case hex digits.";
        }

        if (!TryGetInt64(element, "bytes", out var bytes) || bytes is < 1 or > MaxBytes)
        {
            return $"the size of entry {entry} is not a whole number of bytes from 1 to {MaxBytes}.";
        }

        row = new CodexPictureRow(entry, (int)regionId, tag, asset, sha256, bytes);
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

    private sealed record Dto(long? EntryId, string? Kind, int RegionId, string Tag, string Asset, string Sha256, long Bytes);

    [GeneratedRegex(@"^(?!.*\.\.)[a-z0-9][a-z0-9_.-]{0,127}\.jpg\z")]
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
    /// With no region only the other-region steps apply. Another key's picture is never used.
    /// </summary>
    public static CodexPictureChoice? Find(
        string entryKey,
        int? regionId,
        IReadOnlySet<CodexPictureKey> local,
        IReadOnlyList<CodexPictureRow> stock)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(stock);

        if (regionId is { } here)
        {
            var key = new CodexPictureKey(entryKey, here);

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
            .Where(key => key.Key == entryKey && key.RegionId != regionId)
            .OrderBy(key => key.RegionId)
            .Select(key => (CodexPictureKey?)key)
            .FirstOrDefault();

        if (otherLocal is { } localKey)
        {
            return new CodexPictureChoice(CodexPictureSource.Local, localKey, true, null);
        }

        var otherStock = stock
            .Where(row => row.EntryKey == entryKey && row.RegionId != regionId)
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
