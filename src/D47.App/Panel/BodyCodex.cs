using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The Codex tiles beside a body or a genus: a square picture with CODEX under it. A body's tile opens the Codex
/// entry its sub-type names; a body type with no entry shows its family placeholder and opens nothing.
/// </summary>
public sealed class BodyCodex(
    CodexTable table,
    CodexPictureSet? pictures = null,
    Action<CodexEntry>? openEntry = null,
    Action<string>? openGenus = null)
{
    public const string TileName = "CodexTile";

    public const string PictureName = "CodexPicture";

    public const string PlaceholderName = "CodexPlaceholder";

    /// <summary>Opens nothing and draws no picture.</summary>
    public static readonly BodyCodex None = new(CodexTable.Parse(string.Empty));

    private readonly Dictionary<string, Bitmap?> _families = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Astronomical Bodies entry named the same as the body's sub-type, or null.</summary>
    public CodexEntry? EntryOf(BodyProfile body) =>
        body.SubType is not { Length: > 0 } subType
            ? null
            : table.All.FirstOrDefault(entry =>
                string.Equals(entry.Category, "Astronomical Bodies", StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Name, subType, StringComparison.OrdinalIgnoreCase));

    /// <summary>The title block's picture side.</summary>
    public const double TitleSide = 88;

    /// <summary>A row tile's picture side.</summary>
    public const double RowSide = 48;

    /// <summary>A row tile's width, on a Tile ground beside the row.</summary>
    public const double RowWidth = 64;

    /// <summary>The tile for a body: in the title block, or framed beside a row.</summary>
    public Control Tile(BodyProfile body, bool framed)
    {
        var side = framed ? RowSide : TitleSide;
        var entry = EntryOf(body);
        var picture = entry is null ? null : EntryPicture(entry.EntryId);
        var image = picture is not null
            ? Picture(PictureName, picture, side)
            : Placeholder(CodexFamilies.FileOfBody(body.Type, body.SubType), side);

        return entry is not null && openEntry is { } open
            ? Pressable(image, framed, $"Codex, {entry.Name}", () => open(entry))
            : Still(image, framed);
    }

    /// <summary>The framed tile for a genus; it opens Commander › Codex filtered to the genus where this surface has the Codex.</summary>
    public Control GenusTile(string genus)
    {
        var image = Placeholder(CodexFamilies.FileOfGenus, RowSide);

        return openGenus is { } open
            ? Pressable(image, framed: true, $"Codex, {genus}", () => open(genus))
            : Still(image, framed: true);
    }

    private Bitmap? EntryPicture(long entryId)
    {
        if (pictures is null)
        {
            return null;
        }

        var choice = CodexPictures.Find(entryId, null, pictures.Store.LocalKeys(), pictures.Store.StockRows());

        return choice switch
        {
            null => null,
            { Source: CodexPictureSource.Local } => Decode(pictures.Store.Local(choice.Key)),
            _ => Decode(pictures.Store.Stock(choice.Row!)),
        };
    }

    private Control? Placeholder(string file, double side)
    {
        if (pictures is null)
        {
            return null;
        }

        if (!_families.TryGetValue(file, out var bitmap))
        {
            _families[file] = bitmap = Decode(pictures.Placeholder(file));
        }

        return bitmap is null ? null : Picture(PlaceholderName, bitmap, side);
    }

    private static Image Picture(string name, Bitmap bitmap, double side) => new()
    {
        Name = name,
        Source = bitmap,
        Width = side,
        Height = side,
        Stretch = Stretch.UniformToFill,
    };

    /// <summary>The picture over CODEX: in A with a › where the tile opens an entry, otherwise Grey.</summary>
    private static StackPanel Face(Control? image, bool framed, bool opens)
    {
        var side = framed ? RowSide : TitleSide;
        var square = new Border { Width = side, Height = side, ClipToBounds = true, Child = image };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        var size = framed ? TypeScale.MetaSmall : TypeScale.Meta;
        var caption = Typeset.Chrome(
            opens && !framed ? "CODEX ›" : "CODEX", size, opens ? ThemeManager.AKey : ThemeManager.GreyKey,
            weight: FontWeight.SemiBold, tracking: size * Fonts.ChromeTracking,
            horizontal: framed ? HorizontalAlignment.Center : HorizontalAlignment.Left);

        return new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = framed ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { square, caption },
        };
    }

    private static Border Still(Control? image, bool framed)
    {
        var tile = new Border
        {
            Name = TileName,
            Child = Face(image, framed, opens: false),
            Width = framed ? RowWidth : double.NaN,
            Padding = framed ? new Thickness(4, 6) : default,
            VerticalAlignment = framed ? VerticalAlignment.Stretch : VerticalAlignment.Top,
        };

        if (framed)
        {
            ThemeInk.Bind(tile, Border.BackgroundProperty, ThemeManager.TileKey);
        }

        Avalonia.Automation.AutomationProperties.SetName(tile, "Codex");

        return tile;
    }

    private static Button Pressable(Control? image, bool framed, string name, Action open)
    {
        var tile = new Button
        {
            Name = TileName,
            Content = Face(image, framed, opens: true),
            Width = framed ? RowWidth : double.NaN,
            Padding = framed ? new Thickness(4, 6) : default,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = framed ? VerticalAlignment.Stretch : VerticalAlignment.Top,
        };

        if (!framed)
        {
            tile.Background = Brushes.Transparent;
        }

        Avalonia.Automation.AutomationProperties.SetName(tile, name);
        tile.Click += (_, _) => open();

        return tile;
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

            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
