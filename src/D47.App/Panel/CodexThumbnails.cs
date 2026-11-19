using Avalonia.Media.Imaging;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The 44px squares of the codex lists: the picture the entry page would show, read from disk only, and the family
/// placeholders. Drawing a list never starts a stock fetch.
/// </summary>
internal sealed class CodexThumbnails(CodexPictureSet pictures)
{
    private const int Pixels = 88;

    private readonly Dictionary<CodexPictureKey, Bitmap?> _decoded = [];
    private readonly Dictionary<string, Bitmap?> _families = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlySet<CodexPictureKey> _local = new HashSet<CodexPictureKey>();
    private IReadOnlyList<CodexPictureRow> _stock = [];

    /// <summary>Re-reads which pictures exist and drops the pictures decoded before.</summary>
    public void Refresh()
    {
        _decoded.Clear();
        _local = pictures.Store.LocalKeys();
        _stock = pictures.Store.StockRows();
    }

    /// <summary>The picture chosen for the entry in the region and its bitmap; null when there is none on disk.</summary>
    public (CodexPictureChoice Choice, Bitmap Bitmap)? Picture(string key, int? regionId)
    {
        if (CodexPictures.Find(key, regionId, _local, _stock) is not { } choice)
        {
            return null;
        }

        if (!_decoded.TryGetValue(choice.Key, out var bitmap))
        {
            var store = pictures.Store;
            _decoded[choice.Key] = bitmap = Decode(
                choice.Source == CodexPictureSource.Local ? store.Local(choice.Key) : store.StockOnDisk(choice.Row!));
        }

        return bitmap is null ? null : (choice, bitmap);
    }

    /// <summary>The family placeholder of <paramref name="file"/>; null when there is none.</summary>
    public Bitmap? Family(string? file)
    {
        if (file is null)
        {
            return null;
        }

        if (!_families.TryGetValue(file, out var bitmap))
        {
            _families[file] = bitmap = Decode(pictures.Placeholder(file));
        }

        return bitmap;
    }

    private static Bitmap? Decode(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);

            return Bitmap.DecodeToWidth(stream, Pixels);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
