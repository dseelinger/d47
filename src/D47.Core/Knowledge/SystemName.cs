using System.Globalization;
using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>
/// What a system's own name says about it (Phase 18, "Read a system name").
/// <para>
/// <b>This is the only question in the family that does not need somebody else to have been there
/// first.</b> Every index d47 can query knows only what has already been scanned and uploaded, so
/// the moment a system appears in one it is not undiscovered any more — which makes an index the
/// wrong tool for the half of exploration that pays a first-footfall multiple. A procedural name is
/// decodable from the string, offline, with no network call and no upload, whether the Commander is
/// standing in the system or reading it off the Galaxy Map.
/// </para>
/// <para>
/// <b>What the mass code means and what it is worth are two different claims, and only the first is
/// settled.</b> The ladder below is measured. Whether the heavier boxels actually pay better is
/// folklore that this repository tried to turn into a number and could not — 374 organic scans over
/// four mass codes, with the one a "more mass, more Stratum" claim leans hardest on sitting at n=5,
/// and a Commander's own route choices are not a sample of the galaxy. So this decodes and stops.
/// See <c>docs/spikes/exobiology-sources.md</c> §4.
/// </para>
/// </summary>
public sealed partial record SystemName(string Value)
{
    /// <summary>
    /// <c>Dryafea PO-X d2-0</c> — sector, boxel letters, mass code, boxel number, system number.
    /// <para>
    /// The boxel number is genuinely optional: it is written only once a sector's letters have been
    /// exhausted, so <c>Synuefe AA-A a0</c> is boxel 0 rather than a malformed name.
    /// </para>
    /// <para>
    /// Validated against <b>4,746 distinct real system names</b> from the corpus — 2,854 procedural
    /// and 1,892 hand-named — with <b>no name that looks procedural failing to parse</b>. That last
    /// count is the one worth keeping: a grammar that silently rejects a real name reports "this one
    /// has no mass code", which is a wrong answer wearing the shape of a right one.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"^(?<sector>.+?) (?<letters>[A-Z][A-Z]-[A-Z]) (?<mass>[a-h])(?:(?<boxel>\d+)-)?(?<system>\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Procedural { get; }

    /// <summary>
    /// The side of the cube a mass code's boxel occupies, in light years.
    /// <para>
    /// <b>Derived from the game's own coordinates rather than recited.</b> A name encodes a boxel
    /// index in its letters and boxel number; regressing that index against real <c>StarPos</c>
    /// values — with each sector's own origin cancelled, so no sector grid has to be assumed —
    /// recovers the box size as the slope. Over 2,854 procedurally-named systems:
    /// </para>
    /// <list type="table">
    /// <item><description><c>a</c> — 9.99 measured against 10, over 298 systems</description></item>
    /// <item><description><c>b</c> — 20.02 against 20, over 1,400 systems (r² 0.997)</description></item>
    /// <item><description><c>c</c> — 39.51 against 40, over 638 systems</description></item>
    /// <item><description><c>d</c> — 78.23 against 80, over 489 systems</description></item>
    /// <item><description><c>e</c> — 165.32 against 160, over 26 systems and thin</description></item>
    /// </list>
    /// <para>
    /// <b><c>f</c>, <c>g</c> and <c>h</c> are not measured here</b> — the corpus holds two, one and
    /// zero of them. They rest on the doubling that the five rungs above establish, closed at the
    /// far end by the published rule that <c>h</c> is the sector itself: 10 × 2⁷ is exactly 1,280,
    /// which is <see cref="SectorLightYears"/>. Corroborated at both ends by community documentation
    /// (mass code <c>a</c> within a 10 ly cube, <c>h</c> "somewhere in this 1280 ly cube", halving
    /// down through <c>g</c> at 640 and <c>f</c> at 320). Findings and the probe:
    /// <c>docs/spikes/exobiology-sources.md</c> §7.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Reads a name. Never fails and never throws: a hand-named system is a real answer rather than
    /// an error, since "this one has no mass code" is exactly what a Commander asking about Sol
    /// needs to hear.
    /// </summary>
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

    /// <summary>The sector, which is a 1,280 ly cube. Null for a hand-named system.</summary>
    public string? Sector { get; private init; }

    /// <summary>The three letters that pick the boxel out of its sector — <c>PO-X</c>.</summary>
    public string? Letters { get; private init; }

    /// <summary>
    /// The lone letter before the digits, <c>a</c> to <c>h</c>, least to most massive. The one piece
    /// of the name that says something about the system rather than merely locating it.
    /// </summary>
    public char? MassCode { get; private init; }

    /// <summary>
    /// Which boxel, where the sector's letters have run out and started again. Zero when the name
    /// omits it, which is the common case rather than a missing value.
    /// </summary>
    public int? BoxelNumber { get; private init; }

    /// <summary>Which system within the boxel. Says nothing about mass or anything else.</summary>
    public int? SystemNumber { get; private init; }

    /// <summary>The side of this system's boxel, in light years. See <see cref="BoxLadder"/>.</summary>
    public int? BoxLightYears { get; private init; }

    public bool IsProcedural => MassCode is not null;

    /// <summary>
    /// Whether the box size for this mass code was measured against real coordinates or follows from
    /// the doubling. Surfaced rather than hidden, because a figure that rests on an inference should
    /// say so where a Commander can see it.
    /// </summary>
    public bool BoxSizeMeasured => MassCode is { } mass && Measured.Contains(mass);

    /// <summary>
    /// How this mass code sits against the others — 1 for <c>a</c> through 8 for <c>h</c>. The
    /// ordering is the whole content of the letter, so it is stated as a position rather than left
    /// for the reader to work out from the alphabet.
    /// </summary>
    public int? MassRank => MassCode is { } mass ? mass - 'a' + 1 : null;

    /// <summary>The boxel as a Commander would write it — <c>PO-X d2</c>.</summary>
    public string? Boxel => IsProcedural
        ? $"{Letters} {MassCode}{(BoxelNumber is > 0 ? BoxelNumber.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}"
        : null;
}
