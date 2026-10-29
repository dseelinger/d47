using System.Globalization;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Saving points on the body the Commander is at and giving the bearing and distance back to them.</summary>
public static class SurfaceGuidanceCapability
{
    public const string Id = "surface-guidance";

    public const string MarkTool = "mark_surface_point";

    public const string BearingTool = "get_surface_bearing";

    public const string NoPosition = "I have no position for you. Marking a spot needs you on or near the surface.";

    private const string NoPositionToGuide =
        "I have no position for you. Finding the way needs you on or near the surface.";

    private const string NothingSaved = "Nothing is saved on this body to guide you to.";

    private const int SpokenListLimit = 3;

    private static readonly string[] Reserved = ["ship", "sample", "mark"];

    private static readonly string[] Compass =
        ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];

    private static readonly string[] Sectors =
    [
        "ahead", "ahead on your right", "on your right", "behind you on your right",
        "behind you", "behind you on your left", "on your left", "ahead on your left",
    ];

    public static CapabilityDescriptor Create(
        SurfaceMarks marks,
        Func<CommanderGameState?> commander,
        Func<GameStatus>? status,
        Func<DateTimeOffset> now,
        EarlierFinds? earlier = null) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Surface guidance",
        Summary = "Mark where you stand on a body, and hear which way your ship, a mark or your last sample lies.",
        Examples = ["mark this spot", "where is my ship", "which way to my last sample"],
        Display = new CapabilityDisplay { PanelTitle = "Surface guidance", Order = 68, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = MarkTool,
                Exercise =
                    "Land or drive on a planet, then say \"mark this spot\". The reply confirms a mark, named "
                    + "\"mark 1\" if you gave no name. It lasts until you leave the body.",
                Description =
                    "Save the Commander's live position on this body as a mark. Without a name it is called "
                    + "\"mark 1\", \"mark 2\" and so on. Marks last until the Commander leaves the body.",
                Parameters =
                [
                    ToolParameter.Text("name", "What to call the mark. Optional; omit to number it."),
                ],
                Commands =
                [
                    new ToolCommandPhrase("mark this spot", NoArguments()),
                    new ToolCommandPhrase("mark this location", NoArguments()),
                    new ToolCommandPhrase("mark my position", NoArguments()),
                ],
                Handler = (arguments, _) => Task.FromResult(Mark(marks, commander(), status, now(), arguments)),
            },

            new ToolDefinition
            {
                Name = BearingTool,
                Exercise =
                    "After marking a spot, move away and ask D47 for the bearing to the ship. The reply gives the "
                    + "bearing and the distance from where you are.",
                Description =
                    "Give the bearing and distance from the Commander's live position to a saved point on this "
                    + "body: the ship, a mark, or a genus or species being sampled. \"nearest <name>\" gives the "
                    + "nearest earlier find on this body whose variant name contains <name>, from other Commanders' logged finds; "
                    + "\"nearest\" alone matches any variant. Without a target it uses the ship, then the newest "
                    + "mark, then the last specimen of the newest open set.",
                Parameters =
                [
                    ToolParameter.Text(
                        "to",
                        "ship, the name of a mark, a genus or species being sampled, or \"nearest <name>\" for the "
                            + "nearest earlier find. Optional."),
                ],
                Commands =
                [
                    Way("where is my ship", "ship"),
                    Way("which way to my ship", "ship"),
                    Way("guide me to my ship", "ship"),
                    Way("which way to my last sample", "sample"),
                    Way("which way to my mark", "mark"),
                    Way("which way to the nearest earlier find", "nearest"),
                    Way("guide me to the nearest earlier find", "nearest"),
                ],
                Handler = (arguments, _) => Task.FromResult(Bearing(marks, commander(), status, earlier, arguments)),
            },
        ],
    };

    /// <summary>"Which way to {name}", one per mark valid for the body the Commander is at.</summary>
    public static IEnumerable<DynamicCommand> Phrases(SurfaceMarks marks, Func<CommanderGameState?> commander)
    {
        if (commander() is not { } state)
        {
            yield break;
        }

        foreach (var mark in marks.Valid(state.Surface))
        {
            yield return new DynamicCommand(
                $"which way to {mark.Name}",
                Id,
                BearingTool,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["to"] = mark.Name });
        }
    }

    private static Dictionary<string, string> NoArguments() => new(StringComparer.Ordinal);

    private static ToolCommandPhrase Way(string phrase, string to) =>
        new(phrase, new Dictionary<string, string>(StringComparer.Ordinal) { ["to"] = to });

    private static ToolResult Mark(
        SurfaceMarks marks,
        CommanderGameState? commander,
        Func<GameStatus>? status,
        DateTimeOffset at,
        ToolArguments arguments)
    {
        if (commander is null || status?.Invoke() is not { HasPosition: true } live)
        {
            return ToolResult.Error(NoPosition);
        }

        var surface = commander.Surface;

        if (surface.Body is null)
        {
            return ToolResult.Error(NoPosition);
        }

        var name = arguments.TryGetString("name", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given.Trim()
            : null;

        if (name is not null && Reserved.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return ToolResult.Error($"\"{name}\" is a word I use for something else. Pick another name.");
        }

        var fix = new SurfaceFix(live.Latitude!.Value, live.Longitude!.Value, live.PlanetRadius ?? 0);

        return marks.Add(surface, name, fix, at) is { } added
            ? ToolResult.Ok($"Marked as {added.Name}.")
            : ToolResult.Error($"You already have a mark called \"{name}\".");
    }

    private static ToolResult Bearing(
        SurfaceMarks marks,
        CommanderGameState? commander,
        Func<GameStatus>? status,
        EarlierFinds? earlier,
        ToolArguments arguments)
    {
        var surface = commander?.Surface ?? SurfacePoints.None;
        var valid = marks.Valid(surface);
        var points = Points(commander, valid);

        var to = arguments.TryGetString("to", out var asked) && !string.IsNullOrWhiteSpace(asked)
            ? asked.Trim()
            : null;

        if (to is not null && NearestWanted(to, points) is { } variant)
        {
            return NearestFind(commander, status, earlier, variant);
        }

        if (points.Count == 0)
        {
            return ToolResult.Ok(NothingSaved);
        }

        if (status?.Invoke() is not { HasPosition: true, PlanetRadius: > 0 } live)
        {
            return ToolResult.Error(NoPositionToGuide);
        }

        var target = Resolve(to, points);

        if (target is null)
        {
            return ToolResult.Ok($"I have nothing called \"{to}\" on this body. {Available(points)}");
        }

        var here = new SurfaceFix(live.Latitude!.Value, live.Longitude!.Value, live.PlanetRadius!.Value);

        return ToolResult.Ok(Describe(target, here, live.Heading));
    }

    /// <summary>The variant name after "nearest" (empty for any), or null when <paramref name="to"/> is not a nearest request.</summary>
    private static string? NearestWanted(string to, List<Point> points)
    {
        if (points.Any(point => point.Kind == PointKind.Mark && Same(point.Name, to)))
        {
            return null;
        }

        const string Word = "nearest";

        if (!to.StartsWith(Word, StringComparison.OrdinalIgnoreCase)
            || (to.Length > Word.Length && !char.IsWhiteSpace(to[Word.Length])))
        {
            return null;
        }

        var name = to[Word.Length..].Trim();

        return name.ToLowerInvariant() is "earlier find" or "earlier finds" or "find" or "finds" or "earlier"
            ? string.Empty
            : name;
    }

    private static ToolResult NearestFind(
        CommanderGameState? commander,
        Func<GameStatus>? status,
        EarlierFinds? earlier,
        string variant)
    {
        var label = variant.Length > 0 ? $"{variant} " : string.Empty;
        var none = ToolResult.Ok($"No Commander has logged an earlier {label}find on this body.");

        if (commander is null || commander.Surface.Body is not { } body || earlier is null
            || !earlier.TryGet(body.SystemAddress, out var finds)
            || BodyName(commander, body) is not { } bodyName)
        {
            return none;
        }

        if (status?.Invoke() is not { HasPosition: true, PlanetRadius: > 0 } live)
        {
            return ToolResult.Error(NoPositionToGuide);
        }

        var here = new SurfaceFix(live.Latitude!.Value, live.Longitude!.Value, live.PlanetRadius!.Value);

        var nearest = finds
            .Where(find => Same(find.Body, bodyName)
                           && find.Name.Contains(variant, StringComparison.OrdinalIgnoreCase))
            .Select(find => (Find: find, At: new SurfaceFix(find.Latitude, find.Longitude, here.RadiusMetres)))
            .OrderBy(candidate => here.MetresTo(candidate.At))
            .FirstOrDefault();

        if (nearest.Find is null)
        {
            return none;
        }

        var target = new Point(
            PointKind.Find, "that earlier find", $"The nearest earlier {nearest.Find.Name} find", nearest.At);

        return ToolResult.Ok(Describe(target, here, live.Heading));
    }

    private static string? BodyName(CommanderGameState commander, (long SystemAddress, int BodyId) body)
    {
        if (commander.Location.Body is { } current && commander.Location.BodyId == body.BodyId
            && commander.Location.SystemAddress == body.SystemAddress)
        {
            return current;
        }

        return commander.Bodies.All
            .FirstOrDefault(known => known.SystemAddress == body.SystemAddress && known.BodyId == body.BodyId)
            ?.BodyName;
    }

    /// <summary>Every saved point on the body, in the order the default answer prefers them.</summary>
    private static List<Point> Points(CommanderGameState? commander, IReadOnlyList<SurfaceMark> marks)
    {
        var points = new List<Point>();

        if (commander?.Surface.Ship is { } ship)
        {
            points.Add(new Point(PointKind.Ship, "your ship", "Your ship", ship));
        }

        foreach (var mark in marks.Reverse())
        {
            points.Add(new Point(PointKind.Mark, mark.Name, Capitalised(mark.Name), mark.At));
        }

        if (commander is not null && SetsOnBody(commander) is { } sets)
        {
            foreach (var set in sets)
            {
                if (set.Specimens.Count > 0)
                {
                    var label = set.Species ?? set.Genus;
                    points.Add(
                        new Point(PointKind.Sample, label, $"Your last {set.Genus} sample", set.Specimens[^1], set));
                }
            }
        }

        return points;
    }

    private static IEnumerable<GenusProgress>? SetsOnBody(CommanderGameState commander) =>
        commander.Surface.Body is { } body ? commander.Sampling.On(body.SystemAddress, body.BodyId)?.InProgress : null;

    private static Point? Resolve(string? to, List<Point> points)
    {
        if (to is null)
        {
            return points[0];
        }

        var marks = points.Where(point => point.Kind == PointKind.Mark).ToList();

        if (marks.FirstOrDefault(point => Same(point.Name, to)) is { } named)
        {
            return named;
        }

        var samples = points.Where(point => point.Kind == PointKind.Sample).ToList();

        return to.ToLowerInvariant() switch
        {
            "ship" or "my ship" or "the ship" => points.FirstOrDefault(point => point.Kind == PointKind.Ship),
            "mark" or "my mark" or "last mark" or "my last mark" => marks.FirstOrDefault(),
            "sample" or "my sample" or "last sample" or "my last sample" => samples.FirstOrDefault(),
            _ => samples.FirstOrDefault(point =>
                point.Set is { } set
                && (Same(set.Genus, to) || (set.Species is { } species && Same(species, to)))),
        };
    }

    private static string Available(List<Point> points)
    {
        var names = points.Select(point => point.Name).ToList();
        var shown = string.Join(", ", names.Take(SpokenListLimit));
        var more = names.Count - SpokenListLimit;

        return more > 0 ? $"I can guide you to {shown} and {more} more." : $"I can guide you to {shown}.";
    }

    private static string Describe(Point target, SurfaceFix here, double? heading)
    {
        var metres = here.MetresTo(target.At);

        if (metres < 2)
        {
            return $"You are at {target.Name}.";
        }

        var bearing = here.BearingTo(target.At);
        var degrees = (int)Math.Round(bearing) % 360;

        var text = $"{target.Subject} is {Distance(metres)} away, bearing {degrees}, {CompassPoint(bearing)}.";

        return heading is { } facing ? $"{text} It is {Relative(bearing, facing)}." : text;
    }

    /// <summary>Metres below a kilometre, otherwise kilometres to one decimal.</summary>
    public static string Distance(double metres) =>
        metres < 1000
            ? $"{Math.Round(metres).ToString("0", CultureInfo.InvariantCulture)} metres"
            : $"{(metres / 1000).ToString("0.0", CultureInfo.InvariantCulture)} kilometres";

    /// <summary>The eight-point compass name for a bearing in degrees.</summary>
    public static string CompassPoint(double bearing) =>
        Compass[(int)Math.Round(((bearing % 360) + 360) % 360 / 45) % 8];

    /// <summary>Where a bearing lies relative to the way the Commander faces, in 45-degree sectors centred on straight ahead.</summary>
    public static string Relative(double bearing, double heading)
    {
        var offset = (((bearing - heading) % 360) + 360) % 360;

        return Sectors[(int)Math.Floor((offset + 22.5) % 360 / 45)];
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private enum PointKind
    {
        Ship,
        Mark,
        Sample,
        Find,
    }

    private sealed record Point(PointKind Kind, string Name, string Subject, SurfaceFix At, GenusProgress? Set = null);
}
