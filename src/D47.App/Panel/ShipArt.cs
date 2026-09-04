using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The hull art the fleet carries: a card still, a 4K picture and a turntable, by hull symbol.
/// <para>
/// <b>Renders of Elite's own geometry, not artwork somebody drew.</b> Hand-authored outlines were
/// tried first and thrown out on sight; these come out of a RenderDoc capture of the shipyard
/// preview, posed and lit in Blender. The pipeline lives outside this repo — it runs once per
/// hull, not once per build — and <c>tools\ship-art.ps1</c> is the step that brings what it made
/// into <c>assets\ships\</c>.
/// </para>
/// <para>
/// <b>Three files per hull, and they reach a Commander two different ways.</b> The card still
/// (<c>&lt;hull&gt;.png</c>, 1280x720) ships inside the download, so a fresh installation has a
/// fleet with pictures on it. The 4K picture (<c>&lt;hull&gt;.4k.png</c>) and the turntable
/// (<c>&lt;hull&gt;.spin.mp4</c>) are 260 MB between them and do not: they are fetched for the
/// hull being looked at (<see cref="ShipArtStore"/>) and kept in <c>data\ships\</c>.
/// </para>
/// <para>
/// <b>Files beside the executable, not resources inside it.</b> The first version compiled the
/// PNGs in as <c>AvaloniaResource</c>, which quietly made three ordinary things impossible: adding
/// a hull meant a rebuild, changing how hulls are drawn meant a rebuild, and a Commander with one
/// Sidewinder carried all forty-odd. On disk, a hull is a file that appears — dropped in by hand,
/// or fetched when a fleet turns out to need it — and this class never has to know which.
/// </para>
/// <para>
/// <b>Two folders, each asked first for what it owns.</b> The build owns the card still — it ships
/// one per hull and replaces it on every update — so <c>ships\</c> is asked for that first. The
/// Commander owns the large art, which is fetched into theirs, so <c>data\ships\</c> is asked for
/// that first. Both folders are searched either way, so a hull the build has no still for still
/// draws from one dropped in by hand. See <see cref="Find"/> for the relic that made the rule
/// worth writing down.
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
/// <b>A miss is ordinary and always will be.</b> A hull whose art has not been fetched yet, or
/// captured yet, falls back to the card it already was. Misses are remembered so an absent hull is
/// looked for once rather than on every layout pass — which is also what makes
/// <see cref="Forget"/> load-bearing rather than tidy: a fetch that lands has to say so.
/// </para>
/// </summary>
internal static class ShipArt
{
    /// <summary>
    /// How wide a card still is decoded, whatever the file holds.
    /// <para>
    /// <b>Because every card is on screen at once.</b> The stills are 1280x720, which is 3.7 MB of
    /// pixels each and 170 MB across a full fleet — held, not passed through, since a fleet page
    /// draws every card together and a cache that evicted would thrash on every layout pass. A
    /// card is 210 to about 400 logical pixels wide, so 512 covers the widest column at the
    /// deepest zoom and costs 590 KB a hull. The 4K picture is what a Commander looks at closely;
    /// this one is a thumbnail and is decoded like one.
    /// </para>
    /// </summary>
    private const int CardWidth = 512;

    private static readonly Dictionary<string, Bitmap?> Known = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Bitmap?> Close = new(StringComparer.Ordinal);

    /// <summary>
    /// How many 4K decodes are held. <b>Two, and this is a memory budget rather than a
    /// preference</b>: a 3840x2160 picture is 33 MB of pixels, so the one being looked at and the
    /// one just left is 66 MB and a third would be a hundred. Two is what makes stepping back to
    /// the ship you were just on free, which is the only repeat that happens.
    /// </summary>
    internal const int CloseHeld = 2;

    private static string? _folder;
    private static string? _shipped;

    /// <summary>
    /// How many 4K pictures are held right now. Here so the ceiling can be asserted rather than
    /// intended: reading them back to count would not do it, because a read that misses caches its
    /// own miss and evicts one of the answers it was asking about.
    /// </summary>
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

    /// <summary>
    /// Where art the Commander owns is read from — <c>AppPaths.Ships</c>, set once at startup.
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
            Point(value, _shipped);
        }
    }

    /// <summary>
    /// Where art that came with the build is read from — <c>AppPaths.ShippedShips</c>. Asked first
    /// for the card still, which the build owns, and second for everything else. See
    /// <see cref="Find"/>.
    /// </summary>
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
    /// The hull, spelled any way Elite spells it: <c>CobraMkV</c>, <c>cobramkv</c> or
    /// <c>Cobra Mk V</c> all reach the same file. See <see cref="Symbol"/>.
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

    /// <summary>
    /// The 4K picture for a hull, or null when it has not arrived
    /// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
    /// <para>
    /// <b>Held for the last two hulls only</b>, and that ceiling is the reason this is a separate
    /// cache rather than a second entry in the first: see <see cref="CloseHeld"/>.
    /// </para>
    /// </summary>
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

            // Full size, unlike the card: this is the picture the Commander zooms into, and one
            // image pixel to one screen pixel is what it is for.
            var art = Read(symbol + ".4k.png", width: 0);

            // Null is cached too: a hull whose picture has not been fetched must not be looked for
            // on every draw of the page.
            Close[symbol] = art;

            while (Close.Count > CloseHeld)
            {
                Close.Remove(Close.Keys.First(key => key != symbol));
            }

            return art;
        }
    }

    /// <summary>
    /// Where a hull's turntable is on disk, or null when it has not arrived. A path rather than
    /// anything decoded, because the decoder that plays it reads a file (<see cref="Turntable"/>).
    /// </summary>
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

    /// <summary>
    /// Forgets what has been read, for a hull whose files have just changed on disk.
    /// <para>
    /// Called when a fetch lands (<see cref="ShipArtStore"/>). Without it a hull looked at once
    /// before its picture arrived would keep the cached miss for the rest of the session.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The file name for a hull, however the journal spelled it.
    /// <para>
    /// <b>Through <c>EliteSpecifications</c> first, and that is not politeness.</b> A stored ship
    /// reaches here as <i>Type-8 Transporter</i> and a planned one as <c>type8</c>, because
    /// <c>StoredShips</c> carries a localised spelling and <c>JournalJson.Named</c> prefers it.
    /// Taking the string as given drew nine of a fleet of twelve and left the rest blank, with
    /// nothing failing anywhere — the cards were simply empty.
    /// </para>
    /// <para>
    /// <b>And a hull nothing knows still works.</b> The fallback is the string itself, sanitised,
    /// so art dropped in for a ship Frontier shipped this morning draws before d47's own tables
    /// have heard of it. That is the case the folder exists for.
    /// </para>
    /// </summary>
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

        // A symbol reaches this class from the journal, which is untrusted, and it is about to
        // become part of a path. Anything that is not a plain symbol is refused rather than
        // sanitised, because there is no such thing as a hull with a slash in its name.
        return symbol.Length == 0 || symbol.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')
            ? null
            : symbol;
    }

    /// <summary>
    /// Where a file is, searching the folder that owns that kind of file first.
    /// <para>
    /// <b>Each folder is asked first for what it owns</b>, which is the rule that stops a relic
    /// winning for ever. The build owns the card still: it ships one for every hull and replaces
    /// it on every update, so a copy in <c>data\ships\</c> can only be older — and one was, on the
    /// machine this was written on, where 0.103's hand-dropped 280x158 preview of the Corsair went
    /// on being drawn beside forty-six 1280x720 renders. The Commander owns the large art, which
    /// is fetched into their folder, so that is asked first for a <c>.4k.png</c> or a turntable.
    /// </para>
    /// <para>
    /// Both folders are searched either way, so a hull the build has no still for still draws from
    /// one dropped in by hand — which is the case the folder exists for.
    /// </para>
    /// </summary>
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

    /// <param name="width">The width to decode to, or 0 for whatever the file holds.</param>
    private static Bitmap? Read(string file, int width)
    {
        if (Find(file) is not { } path)
        {
            return null;
        }

        try
        {
            // Read through a stream that is closed straight after, so a drawing being replaced on
            // disk — a look still in flux, a fetch landing — is not blocked by the app holding it.
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
