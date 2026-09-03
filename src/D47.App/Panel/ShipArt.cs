using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;

namespace D47.App.Panel;

/// <summary>
/// The hull drawings the fleet cards carry, read from <c>data/ships/</c> by hull symbol.
/// <para>
/// <b>Renders of Elite's own geometry, not artwork somebody drew.</b> Hand-authored outlines were
/// tried first and thrown out on sight; these come out of a RenderDoc capture of the shipyard
/// preview, posed and lit in Blender. The pipeline lives outside this repo — it runs once per
/// hull, not once per build.
/// </para>
/// <para>
/// <b>Files beside the executable, not resources inside it.</b> The first version compiled the
/// PNGs in as <c>AvaloniaResource</c>, which quietly made three ordinary things impossible: adding
/// a hull meant a rebuild, changing how hulls are drawn meant a rebuild, and a Commander with one
/// Sidewinder carried all forty-odd. On disk, a hull is a file that appears — dropped in by hand
/// today, fetched on demand later — and this class never has to know which.
/// </para>
/// <para>
/// <b>Decoded rather than drawn, which is the one place this departs from the avatar.</b> That
/// control makes its default out of vector geometry deliberately, to keep an image decoder out of
/// a dependency graph that has stayed clean of them, and because animation frames would need
/// something advancing them on two surfaces. The second reason does not reach here — Ships is
/// drawn on this window only (Phase 39), so there is one surface and one timer. The first is
/// answered by what the drawing has to be: hidden-line strokes over a solid hull, which path data
/// cannot carry, and a flat vector silhouette is the look that was already rejected on sight.
/// </para>
/// <para>
/// <b>A miss is ordinary and always will be.</b> Most symbols have no drawing yet, and a fresh
/// installation has none at all; the card falls back to the text it already was. Misses are
/// remembered so an absent hull is looked for once rather than on every layout pass.
/// </para>
/// </summary>
internal static class ShipArt
{
    private static readonly Dictionary<string, Bitmap?> Known = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<CroppedBitmap>?> Recent =
        new(StringComparer.Ordinal);

    private static string? _folder;

    /// <summary>
    /// Where the drawings are read from. Set once, at startup, from <c>AppPaths.Ships</c>.
    /// <para>
    /// A property rather than a constructor argument because the card primitive that needs it is a
    /// static helper several layers below anything holding an <c>AppPaths</c>, and threading one
    /// down to it would be a parameter on every page in the file. Setting it clears what was read
    /// under the old folder, so a test can point at its own without inheriting another's hulls.
    /// </para>
    /// </summary>
    internal static string? Folder
    {
        get => _folder;
        set
        {
            lock (Known)
            {
                _folder = value;
                Known.Clear();

                lock (Recent)
                {
                    Recent.Clear();
                }
            }
        }
    }

    /// <summary>The resting drawing for a hull, or null when there is not one.</summary>
    /// <param name="hull">
    /// The hull symbol as the journal writes it. Normalised the way
    /// <c>EliteSpecifications.HullName</c> normalises it, so <c>CobraMkV</c> and <c>cobramkv</c>
    /// reach the same file.
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

            var art = Read(symbol + ".png");

            Known[symbol] = art;

            return art;
        }
    }

    /// <summary>
    /// Every frame of a hull's turn, or null when it has no sheet.
    /// <para>
    /// <b>One decode, sliced into views.</b> The frames arrive as a single sheet and become
    /// <see cref="CroppedBitmap"/> windows onto it rather than 120 decoded bitmaps: the pixels are
    /// paid for once, and slicing costs nothing.
    /// </para>
    /// <para>
    /// <b>Held for the last two hulls only.</b> A sheet is around 20 MB decoded, so keeping every
    /// hull's would be most of a gigabyte across a full fleet. Only a pointed-at card spins and a
    /// pointer is in one place, so two is enough to make going back to the card you just left
    /// free.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<CroppedBitmap>? Frames(string? hull)
    {
        var symbol = Symbol(hull);

        if (symbol is null)
        {
            return null;
        }

        lock (Recent)
        {
            if (Recent.TryGetValue(symbol, out var held))
            {
                return held;
            }

            var frames = Slice(symbol);

            // Null is cached too: a hull with no sheet must not be looked for on every hover.
            Recent[symbol] = frames;

            while (Recent.Count > 2)
            {
                Recent.Remove(Recent.Keys.First(key => key != symbol));
            }

            return frames;
        }
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

        lock (Recent)
        {
            Recent.Remove(symbol);
        }
    }

    private static string? Symbol(string? hull)
    {
        if (hull is not { Length: > 0 })
        {
            return null;
        }

        var symbol = hull.Trim().ToLowerInvariant();

        // A symbol reaches this class from the journal, which is untrusted, and it is about to
        // become part of a path. Anything that is not a plain symbol is refused rather than
        // sanitised, because there is no such thing as a hull with a slash in its name.
        return symbol.Length == 0 || symbol.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')
            ? null
            : symbol;
    }

    private static Bitmap? Read(string file)
    {
        if (_folder is not { Length: > 0 })
        {
            return null;
        }

        var path = Path.Combine(_folder, file);

        try
        {
            // Read through a stream that is closed straight after, so a drawing being replaced on
            // disk — a look still in flux, a fetch landing — is not blocked by the app holding it.
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);

            return new Bitmap(stream);
        }
        catch (Exception)
        {
            // A half-written or corrupt PNG costs a card its picture, not the fleet page.
            return null;
        }
    }

    private static IReadOnlyList<CroppedBitmap>? Slice(string symbol)
    {
        var sheet = Read(symbol + ".spin.png");

        if (sheet is null)
        {
            return null;
        }

        // The grid is read off the sheet against the resting frame's size rather than stored
        // beside it, so a hull rendered at a different frame count needs no second fact kept in
        // step with the first.
        var cell = For(symbol);

        if (cell is null)
        {
            return null;
        }

        var w = (int)cell.Size.Width;
        var h = (int)cell.Size.Height;

        if (w <= 0 || h <= 0)
        {
            return null;
        }

        var columns = sheet.PixelSize.Width / w;
        var rows = sheet.PixelSize.Height / h;

        if (columns <= 0 || rows <= 0)
        {
            return null;
        }

        var frames = new List<CroppedBitmap>(columns * rows);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                frames.Add(new CroppedBitmap(sheet, new PixelRect(column * w, row * h, w, h)));
            }
        }

        return frames;
    }
}
