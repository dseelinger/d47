using System.Globalization;
using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>What a system's own name says about it (Phase 18, "Read a system name").</summary>
public sealed partial record SystemName(string Value)
{
    /// <summary>
    /// <c>Dryafea PO-X d2-0</c> — sector, boxel letters, mass code, boxel number, system number.
    /// </summary>
    [GeneratedRegex(
        @"^(?<sector>.+?) (?<letters>[A-Z][A-Z]-[A-Z]) (?<mass>[a-h])(?:(?<boxel>\d+)-)?(?<system>\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Procedural { get; }

    /// <summary>The side of the cube a mass code's boxel occupies, in light years.</summary>
    private static readonly IReadOnlyDictionary<char, int> BoxLadder = new Dictionary<char, int>
    {
        ['a'] = 10,
        ['b'] = 20,
        ['c'] = 40,
        ['d'] = 80,
        ['e'] = 160,
        ['f'] = 320,
        ['g'] = 640,
        ['h'] = 1280,
    };

    /// <summary>The side of a sector, which is also mass code <c>h</c>'s boxel.</summary>
    public const int SectorLightYears = 1280;

    /// <summary>Mass codes d47 has measured a box size for, as against inferred one.</summary>
    private static readonly HashSet<char> Measured = ['a', 'b', 'c', 'd', 'e'];

    /// <summary>Reads a name.</summary>
    public static SystemName Read(string? name)
    {
        var value = name?.Trim() ?? string.Empty;

        if (Procedural.Match(value) is not { Success: true } match)
        {
            return new SystemName(value);
        }

        var mass = match.Groups["mass"].Value[0];

        return new SystemName(value)
        {
            Sector = match.Groups["sector"].Value,
            Letters = match.Groups["letters"].Value,
            MassCode = mass,
            BoxelNumber = match.Groups["boxel"].Success
                ? int.Parse(match.Groups["boxel"].Value, CultureInfo.InvariantCulture)
                : 0,
            SystemNumber = int.Parse(match.Groups["system"].Value, CultureInfo.InvariantCulture),
            BoxLightYears = BoxLadder[mass],
        };
    }

    /// <summary>The sector, which is a 1,280 ly cube.</summary>
    public string? Sector { get; private init; }

    /// <summary>The three letters that pick the boxel out of its sector — <c>PO-X</c>.</summary>
    public string? Letters { get; private init; }

    /// <summary>The lone letter before the digits, <c>a</c> to <c>h</c>, least to most massive.</summary>
    public char? MassCode { get; private init; }

    /// <summary>Which boxel, where the sector's letters have run out and started again.</summary>
    public int? BoxelNumber { get; private init; }

    /// <summary>Which system within the boxel.</summary>
    public int? SystemNumber { get; private init; }

    /// <summary>The side of this system's boxel, in light years.</summary>
    public int? BoxLightYears { get; private init; }

    public bool IsProcedural => MassCode is not null;

    /// <summary>
    /// Whether the box size for this mass code was measured against real coordinates or follows from
    /// the doubling.
    /// </summary>
    public bool BoxSizeMeasured => MassCode is { } mass && Measured.Contains(mass);

    /// <summary>How this mass code sits against the others — 1 for <c>a</c> through 8 for <c>h</c>.</summary>
    public int? MassRank => MassCode is { } mass ? mass - 'a' + 1 : null;

    /// <summary>The boxel as a Commander would write it — <c>PO-X d2</c>.</summary>
    public string? Boxel => IsProcedural
        ? $"{Letters} {MassCode}{(BoxelNumber is > 0 ? BoxelNumber.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}"
        : null;
}
