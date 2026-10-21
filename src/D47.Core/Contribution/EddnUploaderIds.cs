using System.Text.Json;
using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;

namespace D47.Core.Contribution;

/// <summary>One random uploader token per Commander, kept in a file keyed by Frontier ID.</summary>
public sealed class EddnUploaderIds(IFileSystem files, string file)
{
    private Dictionary<string, string>? _ids;

    /// <summary>The token for the Commander, minted and saved the first time it is asked for.</summary>
    public string For(string frontierId)
    {
        ArgumentException.ThrowIfNullOrEmpty(frontierId);

        _ids ??= Load();

        if (_ids.TryGetValue(frontierId, out var existing) && DonorToken.IsWellFormed(existing))
        {
            return existing;
        }

        var token = DonorToken.NewToken();
        _ids[frontierId] = token;
        files.CreateFolder(Path.GetDirectoryName(file)!);
        files.WriteText(file, JsonSerializer.Serialize(_ids));

        return token;
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            var text = files.ReadText(file);

            return string.IsNullOrWhiteSpace(text)
                ? new(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<Dictionary<string, string>>(text) is { } read
                    ? new(read, StringComparer.Ordinal)
                    : new(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(StringComparer.Ordinal);
        }
    }
}
