using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>One mark on a body's row: words, the value after them, and whether it is done rather than still worth doing.</summary>
public sealed record BodyMark(string Words, string? Value = null, bool Done = false);

/// <summary>One slab of a body page's Your record group.</summary>
/// <param name="Lines">Further lines in the slab's ink; Still worth doing puts each item here.</param>
/// <param name="Note">A grey line under the value.</param>
/// <param name="Idle">Drawn grey: nothing is left to do and nothing was done.</param>
public sealed record RecordSlab(
    string Label,
    string? Text,
    string? Value = null,
    bool Done = false,
    bool Idle = false,
    string? Note = null,
    IReadOnlyList<string>? Lines = null);

/// <summary>
/// What the Commander has done on a body and what is still worth doing there, from <see cref="OwnSystem"/>,
/// <see cref="BiologyInSystem"/> and <see cref="SystemWorth"/>. Does no I/O.
/// </summary>
public static class BodyRecord
{
    public const string MappedByYou = "✓ Mapped by you";

    public const string RecordHint = "The Biology section lists each genus and what you have taken from it.";

    public const string NothingHere = "Nothing on this body";

    /// <summary>The body's marks: mapping, biology, then the other items <paramref name="worth"/> lists for it.</summary>
    public static IReadOnlyList<BodyMark> Marks(BodyProfile body, OwnSystem? own, SystemWorthAnswer? worth, SystemBiologyAnswer? biology)
    {
        ArgumentNullException.ThrowIfNull(body);

        var marks = new List<BodyMark>();
        var items = ItemsFor(body, worth);

        if (Mapping(body, own, items) is { } mapping)
        {
            marks.Add(mapping);
        }

        if (biology?.Bodies.FirstOrDefault(line => line.BodyId == body.BodyId) is { } line && Genera(line) is { } genera)
        {
            marks.Add(genera.Done == genera.Known && line.Genera.Count > 0
                ? new BodyMark($"✓ Biology {Count(genera.Known)} of {Count(genera.Known)}", Done: true)
                : new BodyMark(
                    $"Biology {Count(genera.Done)} of {Count(genera.Known)}",
                    items.FirstOrDefault(item => item.Kind == WorthKind.Biology) is { } item ? Range(item.Low, item.High) : null));
        }

        foreach (var item in items.Where(item => item.Kind is WorthKind.Unusual or WorthKind.Geology))
        {
            marks.AddRange(OtherWords(item).Select(words => new BodyMark(words)));
        }

        return marks;
    }

    /// <summary>
    /// The Mapping, Biology and Still worth doing slabs for a planet or moon the Commander has scanned; empty for a star
    /// or a body with no scan of theirs.
    /// </summary>
    public static IReadOnlyList<RecordSlab> Slabs(BodyProfile body, OwnSystem? own, SystemWorthAnswer? worth, SystemBiologyAnswer? biology)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.Type == "Star" || own?.Own.GetValueOrDefault(body.BodyId) is not { } scanned)
        {
            return [];
        }

        var items = ItemsFor(body, worth);
        var listed = items.FirstOrDefault(item => item.Kind == WorthKind.Mapping);
        var slabs = new List<RecordSlab>
        {
            scanned.MappedByCommander
                ? new RecordSlab("Mapping", MappedByYou, Done: true)
                : listed is not null
                    ? new RecordSlab("Mapping", listed.FirstMapping ? "First mapping available" : "Not mapped by you", Credits(listed.High))
                    : scanned.WasMapped == false
                        ? new RecordSlab("Mapping", "First mapping available")
                        : new RecordSlab(
                            "Mapping",
                            "Not mapped by you",
                            Idle: true,
                            Note: scanned.WasMapped == true
                                ? "Another Commander mapped it first, and D47 does not list it as worth mapping."
                                : "D47 does not list it as worth mapping."),
        };

        if (biology?.Bodies.FirstOrDefault(line => line.BodyId == body.BodyId) is { } line && Genera(line) is { } genera)
        {
            var range = items.FirstOrDefault(item => item.Kind == WorthKind.Biology) is { } item ? Range(item.Low, item.High) : null;

            slabs.Add(line.Genera.Count == 0
                ? new RecordSlab("Biology", $"0 of {Count(genera.Known)} · genera not yet named", range)
                : genera.Done == genera.Known
                    ? new RecordSlab("Biology", $"✓ {Count(genera.Known)} of {Count(genera.Known)} genera done", Done: true)
                    : new RecordSlab("Biology", $"{Count(genera.Done)} of {Count(genera.Known)} genera done", range));
        }

        var worthDoing = items.SelectMany(ItemWords).ToList();

        slabs.Add(worthDoing.Count > 0
            ? new RecordSlab("Still worth doing", null, Lines: worthDoing)
            : new RecordSlab("Still worth doing", NothingHere, Idle: true));

        return slabs;
    }

    /// <summary>"4.2M CR"; below 100,000 the whole figure, "48,000 CR".</summary>
    public static string Credits(long? value) => value switch
    {
        null => string.Empty,
        >= 100_000 => Millions(value.Value) + "M CR",
        _ => value.Value.ToString("N0", CultureInfo.InvariantCulture) + " CR",
    };

    /// <summary>"4.1–19.0M CR", or one figure where the two are the same or only one is known; null with neither.</summary>
    public static string? Range(long? low, long? high) => (low, high) switch
    {
        (null, null) => null,
        ({ } lowOnly, null) => Credits(lowOnly),
        (null, { } highOnly) => Credits(highOnly),
        ({ } from, { } to) when from == to => Credits(from),
        ({ } from and >= 100_000, { } to and >= 100_000) => $"{Millions(from)}–{Millions(to)}M CR",
        ({ } from, { } to) => $"{Credits(from)}–{Credits(to)}",
    };

    /// <summary>A mark in its ink, with its value in mono after it.</summary>
    public static Control Mark(BodyMark mark)
    {
        var key = mark.Done ? ThemeManager.CyanKey : ThemeManager.AKey;

        var words = RoutingKit.Tag(mark.Words, key);
        words.FontSize = TypeScale.MetaSmall;
        words.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;

        if (mark.Value is not { } value)
        {
            return words;
        }

        var figure = Typeset.Mono(value, TypeScale.MetaSmall, key);
        figure.VerticalAlignment = VerticalAlignment.Center;

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { words, figure } };
    }

    /// <summary>The 1px line drawn before a row's first mark.</summary>
    public static Border Separator()
    {
        var line = new Border { Width = 1, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(line, Border.BackgroundProperty, ThemeManager.LineKey);

        return line;
    }

    /// <summary>A Your record slab as a data tile.</summary>
    public static Border Slab(RecordSlab slab)
    {
        var stack = new StackPanel { Spacing = 2 };

        var label = Typeset.Chrome(
            slab.Label.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey,
            weight: FontWeight.Medium, tracking: TypeScale.Meta * Fonts.ChromeTracking);
        stack.Children.Add(label);

        var key = slab.Done ? ThemeManager.CyanKey : slab.Idle ? ThemeManager.GreyKey : ThemeManager.AKey;

        if (slab.Text is { } text)
        {
            var value = Typeset.Chrome(text, slab.Idle ? TypeScale.Body : StatTile.ValueSize, key, weight: FontWeight.Medium);
            value.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(value);
        }

        if (slab.Value is { } figure)
        {
            stack.Children.Add(Typeset.Mono(figure, TypeScale.Control, key));
        }

        foreach (var line in slab.Lines ?? [])
        {
            stack.Children.Add(Typeset.Chrome(
                line.ToUpperInvariant(), TypeScale.Control, key,
                weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking));
        }

        if (slab.Note is { } note)
        {
            var hint = RoutingKit.Ink(note, TypeScale.Control, ThemeManager.GreyKey, wrap: true);
            stack.Children.Add(hint);
        }

        var tile = new Border { Padding = new Thickness(14, 10), Child = stack };
        ThemeInk.Bind(tile, Border.BackgroundProperty, ThemeManager.SlabKey);
        Avalonia.Automation.AutomationProperties.SetName(tile, slab.Label);

        return tile;
    }

    private static List<WorthItem> ItemsFor(BodyProfile body, SystemWorthAnswer? worth) =>
        [.. worth?.Items.Where(item => item.BodyId == body.BodyId) ?? []];

    private static BodyMark? Mapping(BodyProfile body, OwnSystem? own, IReadOnlyList<WorthItem> items)
    {
        if (body.Type == "Star")
        {
            return null;
        }

        var scanned = own?.Own.GetValueOrDefault(body.BodyId);

        if (scanned is { MappedByCommander: true })
        {
            return new BodyMark(MappedByYou, Done: true);
        }

        if (items.FirstOrDefault(item => item.Kind == WorthKind.Mapping) is { } listed)
        {
            return new BodyMark(listed.FirstMapping ? "First mapping" : "Map", Credits(listed.High));
        }

        return scanned is { WasMapped: false } ? new BodyMark("First mapping") : null;
    }

    /// <summary>Genera done and genera known; the signal count stands for the known while no genus is named. Null with no biology.</summary>
    private static (int Done, int Known)? Genera(BodyLine line) =>
        line.Genera.Count > 0 ? (line.Done, line.Genera.Count)
        : line.BiologicalCount > 0 ? (0, line.BiologicalCount)
        : null;

    private static IEnumerable<string> OtherWords(WorthItem item) =>
        item.Kind == WorthKind.Unusual ? item.Names.Select(name => $"{name} signals") : ["Geology"];

    private static IEnumerable<string> ItemWords(WorthItem item) => item.Kind switch
    {
        WorthKind.Biology => ["Biology"],
        WorthKind.Mapping => [item.FirstMapping ? "First mapping" : "Mapping"],
        _ => OtherWords(item),
    };

    private static string Millions(long value) =>
        (value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture);

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);
}
