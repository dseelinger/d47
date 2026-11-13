using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

public enum GuardianSiteKind
{
    Ruin,
    Structure,
}

/// <summary>One body that holds a Guardian ruin or structure, with the structure layouts the galaxy search host names.</summary>
public sealed record GuardianSite(string System, long Address, StarPosition At, string Body, GuardianSiteKind Kind, IReadOnlyList<string> Layouts);

/// <summary>The bodies with a Guardian ruin or structure, from the table tools/gen-guardian-sites.py writes.</summary>
public static class GuardianSites
{
    private const string ResourceName = "D47.Core.GuardianSites";

    private static readonly Lazy<IReadOnlyList<GuardianSite>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Every row, sorted by system name and then body name.</summary>
    public static IReadOnlyList<GuardianSite> All => Loaded.Value;

    /// <summary>How many of <paramref name="bodies"/> hold ruins and how many a structure, as the Galaxy map's strip lists them.</summary>
    public static IReadOnlyList<string> Counted(IEnumerable<GuardianSite> bodies)
    {
        var list = bodies.ToList();
        var ruins = list.Count(site => site.Kind == GuardianSiteKind.Ruin);
        var structures = list.Count - ruins;
        var said = new List<string>();

        if (ruins > 0)
        {
            said.Add($"{ruins} {(ruins == 1 ? "body" : "bodies")} with ruins");
        }

        if (structures > 0)
        {
            said.Add(ruins > 0
                ? $"{structures} with {(structures == 1 ? "a structure" : "structures")}"
                : $"{structures} {(structures == 1 ? "body with a structure" : "bodies with structures")}");
        }

        return said;
    }

    public static IReadOnlyList<GuardianSite> Parse(TextReader reader)
    {
        var sites = new List<GuardianSite>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("system\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 7
                || !long.TryParse(cells[1], CultureInfo.InvariantCulture, out var address)
                || !double.TryParse(cells[2], CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(cells[3], CultureInfo.InvariantCulture, out var y)
                || !double.TryParse(cells[4], CultureInfo.InvariantCulture, out var z))
            {
                continue;
            }

            var kind = cells[6] == "structure" ? GuardianSiteKind.Structure : GuardianSiteKind.Ruin;
            var layouts = cells.Length > 7 ? cells[7].Split(';', StringSplitOptions.RemoveEmptyEntries) : [];

            sites.Add(new GuardianSite(cells[0], address, new StarPosition(x, y, z), cells[5], kind, layouts));
        }

        return sites;
    }

    private static IReadOnlyList<GuardianSite> Load()
    {
        using var stream = typeof(GuardianSites).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);

        return Parse(reader);
    }
}
