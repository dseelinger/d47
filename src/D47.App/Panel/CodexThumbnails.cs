using Avalonia.Controls;
using Avalonia.Media.Imaging;
using D47.App.Theming;
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

    /// <summary>Re-reads which pictures exist and drops the pictures decoded before; true when the pictures on disk are not the ones read last.</summary>
    public bool Refresh()
    {
        var local = pictures.Store.LocalKeys();
        var stock = pictures.Store.StockRows();
        var changed = !local.SetEquals(_local) || !stock.SequenceEqual(_stock);

        _decoded.Clear();
        _local = local;
        _stock = stock;

        return changed;
    }

    /// <summary>The entry's picture in the region, else its family placeholder; null when there is neither.</summary>
    public Bitmap? Entry(string key, int? regionId, string? placeholderFile) =>
        Picture(key, regionId)?.Bitmap ?? Family(placeholderFile);

    /// <summary>A list row's square: the bitmap on the Slab ground, or the ground alone.</summary>
    public static Border Square(Bitmap? bitmap)
    {
        var side = TypeScale.MinimumTarget;
        var square = new Border { Width = side, Height = side };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (bitmap is not null)
        {
            square.Child = new Image
            {
                Name = "CodexThumb",
                Source = bitmap,
                Stretch = Avalonia.Media.Stretch.Uniform,
                Width = side,
                Height = side,
            };
        }

        return square;
    }

    /// <summary>The leading Picture column of a codex list.</summary>
    public static TableField<TRow> Column<TRow>(Func<TRow, Control> square) =>
        LedgerTables.Control(new TableColumn("Picture", ColumnFit.Fixed) { Fixed = TypeScale.MinimumTarget }, square);

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
