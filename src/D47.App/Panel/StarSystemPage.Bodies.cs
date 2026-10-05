using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A body and how many levels below the main star it orbits.</summary>
public sealed record BodyLevel(BodyProfile Body, int Depth);

public sealed partial class StarSystemPage
{
    public const string BodiesNote = "bodies · each star, what orbits it, and moons under their planet";

    private const double BodyIndent = 28;

    /// <summary>Each star, then what orbits it, every body followed by its moons; siblings in <c>BodyId</c> order.</summary>
    public static IReadOnlyList<BodyLevel> BodyTree(IReadOnlyList<BodyProfile> bodies)
    {
        var ids = bodies.Select(body => body.BodyId).ToHashSet();
        var children = bodies
            .Where(body => body.ParentId is { } parent && ids.Contains(parent))
            .ToLookup(body => body.ParentId!.Value);
        var tree = new List<BodyLevel>(bodies.Count);

        void Walk(BodyProfile body, int depth)
        {
            tree.Add(new BodyLevel(body, depth));

            foreach (var child in children[body.BodyId].OrderBy(child => child.BodyId))
            {
                Walk(child, depth + 1);
            }
        }

        foreach (var root in bodies.Where(body => body.ParentId is not { } parent || !ids.Contains(parent)).OrderBy(body => body.BodyId))
        {
            Walk(root, 0);
        }

        return tree;
    }

    /// <summary>The second line of a body's slab.</summary>
    public static IReadOnlyList<string> BodyFacts(BodyProfile body)
    {
        var facts = new List<string>();

        if (body.Type == "Star")
        {
            if (body.SolarMasses is { } mass)
            {
                facts.Add($"{Decimals(mass)} solar masses");
            }

            if (body.SolarRadius is { } radius)
            {
                facts.Add($"{Decimals(radius)} solar radius");
            }

            if (body.SpectralClass is { } spectral)
            {
                facts.Add($"Class {spectral}{body.Luminosity}");
            }

            if (body.Scoopable is { } scoopable)
            {
                facts.Add(scoopable ? "Scoopable" : "Not scoopable");
            }

            return facts;
        }

        if (body.EarthMasses is { } earth)
        {
            facts.Add($"{Decimals(earth)} Earth masses");
        }

        if (body.Radius is { } km)
        {
            facts.Add($"{km.ToString("N0", CultureInfo.InvariantCulture)} km");
        }

        if (body.Gravity is { } gravity)
        {
            facts.Add($"{Decimals(gravity)} g");
        }

        if (body.SurfaceTemperature is { } kelvin)
        {
            facts.Add($"{kelvin.ToString("N0", CultureInfo.InvariantCulture)} K");
        }

        if (body.SurfacePressure is > 0 and { } atm)
        {
            facts.Add($"{Decimals(atm)} atm");
        }

        facts.Add(body.Volcanism ?? "No volcanism");
        facts.Add(body.Atmosphere ?? "No atmosphere");
        facts.Add(body.TerraformingState ?? "Not terraformable");

        foreach (var (name, count) in body.Signals)
        {
            facts.Add(Counted(count, SignalName(name), "signal"));
        }

        return facts;
    }

    /// <summary>A ring's line: its type, the body's reserve level and each hotspot.</summary>
    public static IReadOnlyList<string> RingFacts(BodyProfile body, RingProfile ring)
    {
        var facts = new List<string>();

        if (ring.Type is { } type)
        {
            facts.Add(type);
        }

        if (body.ReserveLevel is { } reserves)
        {
            facts.Add($"Reserves {reserves.ToLowerInvariant()}");
        }

        foreach (var (material, count) in ring.Signals)
        {
            facts.Add(Counted(count, material, "hotspot"));
        }

        return facts;
    }

    /// <summary>The ring's name without its body's: "A Ring" for "LTT 7786 3 A Ring".</summary>
    public static string RingName(BodyProfile body, RingProfile ring) =>
        ring.Name.StartsWith(body.Name + " ", StringComparison.Ordinal) ? ring.Name[(body.Name.Length + 1)..] : ring.Name;

    /// <summary>"Human" for the journal's <c>$SAA_SignalType_Human;</c>.</summary>
    public static string SignalName(string name) => name switch
    {
        _ when name.StartsWith("$SAA_SignalType_", StringComparison.Ordinal) => name["$SAA_SignalType_".Length..].TrimEnd(';'),
        "$PlanetaryMiningLocation_Name;" => "Mining location",
        _ => name.Trim('$', ';'),
    };

    /// <summary>"288 Ls", or a dash where Spansh gives no distance.</summary>
    public static string BodyDistanceText(double? lightSeconds) =>
        lightSeconds is { } ls ? ls.ToString("N0", CultureInfo.InvariantCulture) + " Ls" : Dash;

    private static string Decimals(double value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Counted(int count, string what, string noun) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {what} {noun}{(count == 1 ? string.Empty : "s")}";

    private StackPanel Bodies(StarSystemProfile profile)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        var note = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                Figure(profile.Bodies.Count, ThemeManager.WhiteKey),
                Caption(BodiesNote, ThemeManager.GreyKey),
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(note, $"{profile.Bodies.Count} bodies");
        list.Children.Add(note);

        foreach (var level in BodyTree(profile.Bodies))
        {
            list.Children.Add(BodySlab(level));
        }

        return list;
    }

    private static Border BodySlab(BodyLevel level)
    {
        var body = level.Body;

        var first = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,240,90"),
            ColumnSpacing = 16,
        };

        var name = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        name.Children.Add(Ellipsed(body.Name, TypeScale.Secondary, FontWeight.SemiBold, ThemeManager.WhiteKey));

        if (body.IsLandable)
        {
            var landable = RoutingKit.Tag("Landable", ThemeManager.AKey);
            landable.FontSize = TypeScale.MetaSmall;
            landable.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;
            name.Children.Add(landable);
        }

        var type = Ellipsed(body.SubType ?? body.Type, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey);
        var distance = Mono(BodyDistanceText(body.DistanceToArrival), ThemeManager.AKey);
        distance.HorizontalAlignment = HorizontalAlignment.Right;

        Cell(first, 0, name);
        Cell(first, 1, type);
        Cell(first, 2, distance);

        var stack = new StackPanel { Spacing = 5 };
        stack.Children.Add(first);
        stack.Children.Add(Facts(BodyFacts(body), ThemeManager.GreyKey));

        foreach (var ring in body.Rings)
        {
            var line = Facts(RingFacts(body, ring), ThemeManager.WhiteKey);
            var title = Caption(RingName(body, ring), ThemeManager.AKey);
            title.FontWeight = FontWeight.SemiBold;
            line.Children.Insert(0, title);
            stack.Children.Add(line);
        }

        var slab = new Border
        {
            Margin = new Thickness(level.Depth * BodyIndent, 0, 0, 0),
            Padding = new Thickness(14, 9, 14, 10),
            Child = stack,
        };
        RoutingKit.Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        Avalonia.Automation.AutomationProperties.SetName(slab, body.Name);

        return slab;
    }

    private static WrapPanel Facts(IReadOnlyList<string> facts, string key)
    {
        var line = new WrapPanel { ItemSpacing = 16, LineSpacing = 2 };

        foreach (var fact in facts)
        {
            var block = Mono(fact, key);
            block.FontSize = TypeScale.Meta;
            block.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(block);
        }

        return line;
    }
}
