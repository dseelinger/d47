using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The hull art the fleet carries: a card still, a 4K picture and a turntable, by hull symbol.</summary>
internal static class ShipArt
{
    /// <summary>How wide a card still is decoded, whatever the file holds.</summary>
    private const int CardWidth = 512;

    private static readonly Dictionary<string, Bitmap?> Known = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Bitmap?> Close = new(StringComparer.Ordinal);

    /// <summary>How many 4K decodes are held.</summary>
    internal const int CloseHeld = 2;

    private static string? _folder;
    private static string? _shipped;

    /// <summary>How many 4K pictures are held right now.</summary>
    internal static int Held
    {
        get
        {
            lock (Close)
            {
                return Close.Count;
            }
        }
    }

    /// <summary>Where art the Commander owns is read from — <c>AppPaths.Ships</c>, set once at startup.</summary>
    internal static string? Folder
    {
        get => _folder;
        set
        {
            Point(value, _shipped);
        }
    }

    /// <summary>Where art that came with the build is read from — <c>AppPaths.ShippedShips</c>.</summary>
    internal static string? Shipped
    {
        get => _shipped;
        set
        {
            Point(_folder, value);
        }
    }

    /// <summary>The resting drawing for a hull, or null when there is not one.</summary>
    /// <param name="hull">
    /// The hull, spelled any way Elite spells it: <c>CobraMkV</c>, <c>cobramkv</c> or <c>Cobra Mk V</c>
    /// all reach the same file.
    /// </param>
    internal static Bitmap? For(string? hull)
    {
        var symbol = Symbol(hull);

        if (symbol is null)
        {
            return null;
        }

        lock (Known)
        {
            if (Known.TryGetValue(symbol, out var held))
            {
                return held;
            }

            var art = Read(symbol + ".png", CardWidth);

            Known[symbol] = art;

            return art;
        }
    }

    /// <summary>The 4K picture for a hull, or null when it has not arrived (#289).</summary>
    internal static Bitmap? Close4K(string? hull)
    {
        var symbol = Symbol(hull);

        if (symbol is null)
        {
            return null;
        }

        lock (Close)
        {
            if (Close.TryGetValue(symbol, out var held))
            {
                return held;
            }

            // Full size, unlike the card: this is the picture the Commander zooms into, and one image pixel
            // to one screen pixel is what it is for.
            var art = Read(symbol + ".4k.png", width: 0);

            // Null is cached too: a hull whose picture has not been fetched must not be looked for on every
            // draw of the page.
            Close[symbol] = art;

            while (Close.Count > CloseHeld)
            {
                Close.Remove(Close.Keys.First(key => key != symbol));
            }

            return art;
        }
    }

    /// <summary>Where a hull's turntable is on disk, or null when it has not arrived.</summary>
    internal static string? SpinFile(string? hull)
    {
        var symbol = Symbol(hull);

        return symbol is null ? null : Find(symbol + ".spin.mp4");
    }

    /// <summary>Whether a hull's 4K picture is on disk, without decoding 33 MB to find out.</summary>
    internal static bool HasClose4K(string? hull)
    {
        var symbol = Symbol(hull);

        return symbol is not null && Find(symbol + ".4k.png") is not null;
    }

    /// <summary>Forgets what has been read, for a hull whose files have just changed on disk.</summary>
    internal static void Forget(string? hull)
    {
        var symbol = Symbol(hull);

        if (symbol is null)
        {
            return;
        }

        lock (Known)
        {
            Known.Remove(symbol);
        }

        lock (Close)
        {
            Close.Remove(symbol);
        }
    }

    /// <summary>Both folders at once, so setting either clears both caches exactly once.</summary>
    private static void Point(string? folder, string? shipped)
    {
        lock (Known)
        {
            _folder = folder;
            _shipped = shipped;
            Known.Clear();

            lock (Close)
            {
                Close.Clear();
            }
        }
    }

    /// <summary>The file name for a hull, however the journal spelled it.</summary>
    internal static string? Symbol(string? hull)
    {
        if (hull is not { Length: > 0 })
        {
            return null;
        }

        if (EliteSpecifications.HullSymbol(hull) is { Length: > 0 } known)
        {
            return known;
        }

        var symbol = hull.Trim().ToLowerInvariant();

        // A symbol reaches this class from the journal, which is untrusted, and it is about to become part of
        // a path.
        return symbol.Length == 0 || symbol.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')
            ? null
            : symbol;
    }

    /// <summary>Where a file is, searching the folder that owns that kind of file first.</summary>
    private static string? Find(string file)
    {
        var mine = file.EndsWith(".4k.png", StringComparison.Ordinal)
                   || file.EndsWith(".spin.mp4", StringComparison.Ordinal);

        foreach (var folder in mine ? new[] { _folder, _shipped } : [_shipped, _folder])
        {
            if (folder is not { Length: > 0 })
            {
                continue;
            }

            var path = Path.Combine(folder, file);

            try
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception)
            {
            // An unreadable folder is a miss, not a crash on the way to drawing a page.
            }
        }

        return null;
    }

    /// <summary><param name="width">The width to decode to, or 0 for whatever the file holds.</param></summary>
    /// <param name="width">The width to decode to, or 0 for whatever the file holds.</param>
    private static Bitmap? Read(string file, int width)
    {
        if (Find(file) is not { } path)
        {
            return null;
        }

        try
        {
            // Read through a stream that is closed straight after, so a drawing being replaced on disk — a
            // look still in flux, a fetch landing — is not blocked by the app holding it.
            using var stream = File.OpenRead(path);

            return width > 0
                ? Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality)
                : new Bitmap(stream);
        }
        catch (Exception)
        {
            // A half-written or corrupt PNG costs a card its picture, not the fleet page.
            return null;
        }
    }
}
