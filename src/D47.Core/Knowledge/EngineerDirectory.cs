using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>One thing an engineer works on, and how far they take it.</summary>
public sealed record Speciality(string Kind, int MaxGrade);

/// <summary>
/// One engineer: who they are, where they work, what they grade, and what their invitation
/// costs where that is a delivery.
/// </summary>
public sealed record Engineer
{
    /// <summary>The id the journal's <c>EngineerProgress</c> writes. The stable key; names drift.</summary>
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? System { get; init; }

    /// <summary>Their base. Named rather than left as a market id, resolved when the table was built.</summary>
    public string? Station { get; init; }

    /// <summary>
    /// The tribute their invitation task asks for, where it is a delivery. Absent for the ones
    /// unlocked by a rank, a permit or a mission — which is most of them, and exactly the part of
    /// the chain that has no source (see <c>tools/gen-engineers.py</c>).
    /// </summary>
    public string? UnlockCost { get; init; }

    /// <summary>What they grade, best grade first. Empty for an engineer nobody has blueprints for.</summary>
    public IReadOnlyList<Speciality> Specialities { get; init; } = [];

    /// <summary>
    /// Who recommends them. Empty for the ones anybody can walk up to.
    /// <para>
    /// More than one name means <em>any</em> of them will do — Yi Shen is reached through three
    /// people. Their spelling is the directory's own, so each is a name <see cref="ByName"/> finds.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ReferredBy { get; init; } = [];

    /// <summary>
    /// The grade needed with the referrer, or null when no source states one.
    /// <para>
    /// Null is the honest answer for the whole on-foot chain: Odyssey engineers unlock on a count
    /// of modifications rather than on a grade, so a 3 here would be a requirement d47 invented.
    /// Where it is stated it is always <see cref="EngineeringRules.ReferralGrade"/>, plus roughly
    /// half the bar to the next — which is the part nobody published as a number.
    /// </para>
    /// </summary>
    public int? ReferralGrade { get; init; }

    /// <summary>The body their base orbits, as the game labels it — "6 A".</summary>
    public string? Body { get; init; }

    /// <summary>How the Commander learns they exist, in prose.</summary>
    public string? Discovery { get; init; }

    /// <summary>What earns the invitation — a rank, a distance, a reputation.</summary>
    public string? Meeting { get; init; }

    /// <summary>What the invitation itself asks for, in prose. <see cref="UnlockCost"/> is the
    /// same thing as a material list, where it is a delivery.</summary>
    public string? Unlock { get; init; }

    /// <summary>How reputation with them is raised fastest, in prose.</summary>
    public string? Reputation { get; init; }

    /// <summary>Whether anybody has to recommend them first.</summary>
    public bool NeedsReferral => ReferredBy.Count > 0;

    /// <summary>The best grade they reach on anything, or null when nothing is known of their work.</summary>
    public int? TopGrade => Specialities.Count == 0 ? null : Specialities.Max(speciality => speciality.MaxGrade);

    public string Where => (System, Station) switch
    {
        (null, _) => "somewhere I don't have on record",
        ({ } system, null) => system,
        ({ } system, { } station) => $"{station} in {system}",
    };
}

/// <summary>
/// Every engineer, where they are, and what they grade (list.md Phase 14, "Engineers").
/// <para>
/// <b>Derived, and read on first use</b>, like <see cref="EliteSpecifications"/> and for the same
/// reasons. Identity comes from EDCD/FDevIDs, which carries the id the journal writes and the two
/// ids naming where they work; those two were resolved to a system and a station name through
/// spansh.co.uk <em>when the table was built</em>, so asking "where is Farseer" reaches no network
/// at all. What they grade comes from msarilar/EDEngineer's blueprint list.
/// </para>
/// <para>
/// <b>The referral chain is here</b>, and was once recorded as unobtainable — a correct
/// measurement of three files (FDevIDs, coriolis-data, EDEngineer's ship rows) turned into a
/// wrong claim about the world. It comes from EDDiscovery's <c>EliteDangerousCore</c>
/// (Apache-2.0), which names the referring engineer and the grade needed with them. The two
/// sources that have it agree on 37 of 38; the conflict is Bill Turner, settled by journal in
/// the wiki's favour — see <c>docs/spikes/journal-corpus-engineering.md</c> §4.
/// /// </para>
/// </summary>
public static class EngineerDirectory
{
    private const string ResourceName = "D47.Core.Engineers";

    private static readonly Lazy<IReadOnlyList<Engineer>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<Engineer> All => Loaded.Value;

    /// <summary>An engineer by the id the journal writes.</summary>
    public static Engineer? ById(int id) => All.FirstOrDefault(engineer => engineer.Id == id);

    /// <summary>
    /// An engineer by name, through the same matcher every other name in this namespace uses —
    /// so "Farseer" finds Felicity Farseer and "Tod McQuinn" finds the one with the nickname in
    /// the middle of it.
    /// </summary>
    public static Engineer? ByName(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var names = All.Select(engineer => engineer.Name).ToArray();

        return Catalogue.Match(names, spoken) is { } name
            ? All.First(engineer => engineer.Name == name)
            : null;
    }

    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near([.. All.Select(engineer => engineer.Name)], spoken);

    /// <summary>
    /// Who grades a named thing, best grade first.
    /// <para>
    /// The kind is matched against the speciality vocabulary rather than compared, because it
    /// arrives spoken: "FSD" and "frame shift drive" and "drive" are the same question, and only
    /// one of them is how the blueprint list spells it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(Engineer Engineer, Speciality Speciality)> Grading(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var kind = Catalogue.Match(Kinds, spoken);

        if (kind is null)
        {
            return [];
        }

        return
        [
            .. All
                .SelectMany(engineer => engineer.Specialities
                    .Where(speciality => speciality.Kind == kind)
                    .Select(speciality => (Engineer: engineer, Speciality: speciality)))
                .OrderByDescending(match => match.Speciality.MaxGrade)
                .ThenBy(match => match.Engineer.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>Everything any engineer works on, each named once.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
        [.. Loaded.Value
            .SelectMany(engineer => engineer.Specialities.Select(speciality => speciality.Kind))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    public static IReadOnlyList<string> NearKinds(string spoken) => Catalogue.Near(Kinds, spoken);

    private static IReadOnlyList<Engineer> Load()
    {
        using var stream = typeof(EngineerDirectory).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);

        var engineers = new List<Engineer>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("id\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 2 || !int.TryParse(cells[0], CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            engineers.Add(new Engineer
            {
                Id = id,
                Name = cells[1],
                System = Text(cells, 2),
                Station = Text(cells, 3),
                UnlockCost = Text(cells, 4),
                Specialities = ReadSpecialities(Text(cells, 5)),
                ReferredBy = ReadNames(Text(cells, 6)),
                ReferralGrade = ReadGrade(Text(cells, 7)),
                Body = Text(cells, 8),
                Discovery = Text(cells, 9),
                Meeting = Text(cells, 10),
                Unlock = Text(cells, 11),
                Reputation = Text(cells, 12),
            });
        }

        return engineers;
    }

    private static IReadOnlyList<string> ReadNames(string? cell) =>
        cell is null
            ? []
            : [.. cell.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0)];

    private static int? ReadGrade(string? cell) =>
        cell is not null && int.TryParse(cell, CultureInfo.InvariantCulture, out var grade)
            ? grade
            : null;

    private static IReadOnlyList<Speciality> ReadSpecialities(string? cell)
    {
        if (cell is null)
        {
            return [];
        }

        var read = new List<Speciality>();

        foreach (var entry in cell.Split(','))
        {
            var split = entry.LastIndexOf(':');

            // A malformed entry is dropped rather than read as grade 0. "Farseer grades frame
            // shift drives to grade 0" is a worse answer than not mentioning it.
            if (split > 0
                && int.TryParse(entry[(split + 1)..], CultureInfo.InvariantCulture, out var grade))
            {
                read.Add(new Speciality(entry[..split], grade));
            }
        }

        return read;
    }

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
}
