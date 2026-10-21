using System.Text.Json;

namespace D47.Core.Interface;

/// <summary>The corner a square is resized from.</summary>
public enum CropCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>A square cut from a picture, in the picture's own pixels. Every operation keeps it square and inside the picture.</summary>
public readonly record struct CropSquare(int X, int Y, int Side)
{
    /// <summary>The smallest side, in pixels of the picture.</summary>
    public const int Floor = 384;

    /// <summary>The side of the picture the square is scaled to when saved.</summary>
    public const int SavedSide = 768;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The largest square, centred.</summary>
    public static CropSquare Start(int width, int height)
    {
        var side = Math.Max(1, Math.Min(width, height));

        return new CropSquare((width - side) / 2, (height - side) / 2, side);
    }

    /// <summary>This square moved by a distance, held inside the picture.</summary>
    public CropSquare Move(int dx, int dy, int width, int height)
    {
        var side = Math.Min(Side, Math.Min(width, height));

        return new CropSquare(
            Math.Clamp(X + dx, 0, width - side),
            Math.Clamp(Y + dy, 0, height - side),
            side);
    }

    /// <summary>
    /// This square with <paramref name="corner"/> dragged by a distance. The opposite corner stays where it is;
    /// the side is held between <see cref="Floor"/> (or the picture, if smaller) and the room the picture leaves.
    /// </summary>
    public CropSquare Resize(CropCorner corner, int dx, int dy, int width, int height)
    {
        var right = corner is CropCorner.TopRight or CropCorner.BottomRight;
        var bottom = corner is CropCorner.BottomLeft or CropCorner.BottomRight;
        var sx = right ? 1 : -1;
        var sy = bottom ? 1 : -1;
        var anchorX = right ? X : X + Side;
        var anchorY = bottom ? Y : Y + Side;
        var most = Math.Max(1, Math.Min(right ? width - anchorX : anchorX, bottom ? height - anchorY : anchorY));
        var wanted = (int)Math.Floor(Side + ((sx * dx) + (sy * dy)) / 2.0 + 0.5);
        var side = Math.Clamp(wanted, Math.Min(Floor, most), most);

        return new CropSquare(right ? anchorX : anchorX - side, bottom ? anchorY : anchorY - side, side);
    }

    /// <summary>This square fitted into a picture: shrunk if larger, floored, and moved inside.</summary>
    public CropSquare Fit(int width, int height)
    {
        var largest = Math.Max(1, Math.Min(width, height));
        var side = Math.Clamp(Side, Math.Min(Floor, largest), largest);

        return new CropSquare(Math.Clamp(X, 0, width - side), Math.Clamp(Y, 0, height - side), side);
    }

    public string Write() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a square <see cref="Write"/> produced; null for text that is not one.</summary>
    public static CropSquare? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CropSquare>(json, Options) is { Side: > 0 } square ? square : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
