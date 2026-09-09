namespace D47.Core.Knowledge;

/// <summary>One system as the candidate scan can see it (Phase 18, "Find somewhere worth colonising").</summary>
public sealed record ColonisationSystem
{
    public required string Name { get; init; }

    /// <summary>Light years from the reference the scan measured out from.</summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Frontier's own test for whether a system can be claimed at all is that it is unpopulated, and a
    /// completed colony becomes "an Uncontrolled Populated star system" in their words — so zero here
    /// is the offline half of the rule.
    /// </summary>
    public long Population { get; init; }

    /// <summary>Somebody has already begun building here.</summary>
    public bool BeingColonised { get; init; }

    /// <summary>Already colonised by an Architect.</summary>
    public bool Colonised { get; init; }

    public int BodyCount { get; init; }

    /// <summary>How many of the planets are terraforming candidates.</summary>
    public int Terraformable { get; init; }

    /// <summary>The planets by kind, in the game's own vocabulary, commonest first.</summary>
    public IReadOnlyList<(string Subtype, int Count)> Planets { get; init; } = [];

    /// <summary>How many planets are known, which is not how many there are.</summary>
    public int KnownPlanets => Planets.Sum(planet => planet.Count);

    /// <summary>Light seconds from the entry point to the nearest and furthest body.</summary>
    public double? NearestBody { get; init; }

    public double? FurthestBody { get; init; }

    /// <summary>Whether this holds at least one planet of the named kind.</summary>
    public bool Holds(string subtype) =>
        Planets.Any(planet => string.Equals(planet.Subtype, subtype, StringComparison.OrdinalIgnoreCase));
}

/// <summary>What the scan saw, before the objective narrowed it.</summary>
/// <param name="Reference">
/// The system distances were measured from, as the service resolved it.
/// </param>
/// <param name="Total">How many systems are within range in all.</param>
/// <param name="Systems">What came back, least populated first.</param>
public sealed record ColonisationScan(
    string? Reference,
    int Total,
    IReadOnlyList<ColonisationSystem> Systems);

/// <summary>A validated search for somewhere to colonise.</summary>
public sealed record ColonisationQuery
{
    private ColonisationQuery()
    {
    }

    /// <summary>
    /// Frontier's number, in their own words: the System Colonisation Contact offers "a nearby
    /// unpopulated star system (within a max distance of 15ly)".
    /// </summary>
    public const double ClaimRange = 15;

    public required string ReferenceSystem { get; init; }

    public double MaxDistance { get; init; } = ClaimRange;

    /// <summary>A kind of planet that must be present, in the catalogue's spelling.</summary>
    public string? Subtype { get; init; }

    /// <summary>At least one terraforming candidate.</summary>
    public bool Terraformable { get; init; }

    /// <summary>At least one body with a ring or a belt.</summary>
    public bool Rings { get; init; }

    /// <summary>How many landable bodies at least.</summary>
    public int MinimumLandable { get; init; }

    /// <summary>How many candidates to report.</summary>
    public int Size { get; init; } = 3;

    /// <summary>Whether anything beyond the claim rule was asked for.</summary>
    public bool HasObjective => Subtype is not null || Terraformable || Rings || MinimumLandable > 0;

    /// <summary>Whether the answer needs the body search as well as the scan.</summary>
    public bool NeedsBodyDetail => Rings || MinimumLandable > 0;

    /// <summary>How many systems the scan pulls.</summary>
    public const int ScanSize = 120;

    public static bool TryParse(
        string? referenceSystem,
        string? subtype,
        bool terraformable,
        bool rings,
        int? minimumLandable,
        double? maxDistance,
        int size,
        out ColonisationQuery query,
        out string failure)
    {
        query = new ColonisationQuery { ReferenceSystem = string.Empty };
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(referenceSystem))
        {
            failure =
                "I don't know where the Commander is right now, so I need a system to search out from — "
                + "the one they will be docked in when they claim.";
            return false;
        }

        string? matched = null;

        if (!string.IsNullOrWhiteSpace(subtype))
        {
            matched = BodyCatalogue.MatchSubtype(subtype);

            if (matched is null)
            {
                failure = Catalogue.Unknown("body type", subtype.Trim(), Catalogue.Near(BodyCatalogue.Subtypes, subtype));
                return false;
            }
        }

        query = new ColonisationQuery
        {
            ReferenceSystem = referenceSystem.Trim(),
            Subtype = matched,
            Terraformable = terraformable,
            Rings = rings,
            MinimumLandable = Math.Clamp(minimumLandable ?? 0, 0, 50),

            // Clamped to the rule rather than refused above it.
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : ClaimRange, 1, ClaimRange),

            // Five is the ceiling rather than the twenty the other searches allow, because each candidate
            // here is four lines rather than one and this is an answer somebody is about to hear read aloud.
            Size = Math.Clamp(size <= 0 ? 3 : size, 1, 5),
        };

        return true;
    }
}
