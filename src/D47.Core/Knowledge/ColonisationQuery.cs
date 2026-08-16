namespace D47.Core.Knowledge;

/// <summary>
/// One system as the candidate scan can see it (list.md Phase 18, "Find somewhere worth
/// colonising").
/// <para>
/// <b>Aggregates rather than bodies, and that is a fact about the index rather than a design
/// preference.</b> The systems search embeds each system's bodies, and those embedded records
/// carry twelve fields of which four are useful here — name, subtype, type and
/// <c>terraforming_state</c>, plus the arrival distance. They do <em>not</em> carry
/// <c>is_landable</c>, <c>rings</c> or <c>atmosphere</c>, measured across 2,355 embedded bodies in
/// 180 systems. Projecting them into <see cref="BodySummary"/> would therefore ship
/// <see cref="BodySummary.IsLandable"/> as false on every body in the galaxy, which is a wrong
/// answer wearing the shape of a right one. The landable and ringed counts come from the body
/// search instead, for the shortlist only — see <see cref="BodyQuery.ForSystems"/>.
/// </para>
/// </summary>
public sealed record ColonisationSystem
{
    public required string Name { get; init; }

    /// <summary>Light years from the reference the scan measured out from.</summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Frontier's own test for whether a system can be claimed at all is that it is unpopulated,
    /// and a completed colony becomes "an Uncontrolled Populated star system" in their words — so
    /// zero here is the offline half of the rule. It is read from the record rather than filtered
    /// on, because the index will not filter it (see <see cref="ColonisationQuery"/>).
    /// </summary>
    public long Population { get; init; }

    /// <summary>
    /// Somebody has already begun building here. Positive evidence only: the flag is absent from
    /// 79 of 84 unpopulated systems measured, so true means "taken", and false or absent means
    /// nothing at all.
    /// </summary>
    public bool BeingColonised { get; init; }

    /// <summary>Already colonised by an Architect. The same one-way reading as <see cref="BeingColonised"/>.</summary>
    public bool Colonised { get; init; }

    public int BodyCount { get; init; }

    /// <summary>How many of the planets are terraforming candidates.</summary>
    public int Terraformable { get; init; }

    /// <summary>The planets by kind, in the game's own vocabulary, commonest first.</summary>
    public IReadOnlyList<(string Subtype, int Count)> Planets { get; init; } = [];

    /// <summary>
    /// How many planets are <em>known</em>, which is not how many there are.
    /// <para>
    /// The index holds what somebody has scanned and uploaded, so a system nobody has honked reads
    /// as a star and nothing else — 23 of the 109 systems within 15 light years of Ratraii, a
    /// fifth of them. Those are not empty systems and must never be reported as small ones. They
    /// are systems with nothing to say about them, which is a different sentence and a useful one:
    /// on a frontier, an unsurveyed neighbour is where a Commander might go and look.
    /// </para>
    /// </summary>
    public int KnownPlanets => Planets.Sum(planet => planet.Count);

    /// <summary>
    /// Light seconds from the entry point to the nearest and furthest body. The spread is the
    /// half of "worth colonising" a Commander feels every trip: a system whose facilities would be
    /// two hundred thousand light seconds apart is a miserable build however good the bodies are.
    /// </summary>
    public double? NearestBody { get; init; }

    public double? FurthestBody { get; init; }

    /// <summary>Whether this holds at least one planet of the named kind.</summary>
    public bool Holds(string subtype) =>
        Planets.Any(planet => string.Equals(planet.Subtype, subtype, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What the scan saw, before the objective narrowed it.
/// </summary>
/// <param name="Reference">The system distances were measured from, as the service resolved it.</param>
/// <param name="Total">
/// How many systems are within range in all. The difference between this and
/// <see cref="Systems"/> is what was not looked at, and an answer that hides it reads as "there
/// are four" when there are a hundred and six.
/// </param>
/// <param name="Systems">
/// What came back, <b>least populated first</b>. The order is load-bearing: the index will not
/// filter on population, so the only way to be sure the unpopulated ones are in the page is to
/// sort them to the front of it.
/// </param>
public sealed record ColonisationScan(
    string? Reference,
    int Total,
    IReadOnlyList<ColonisationSystem> Systems);

/// <summary>
/// A validated search for somewhere to colonise. Like every other query in this namespace,
/// existing is the evidence that it is valid.
/// <para>
/// <b>Two things the index does that this class exists to work around, both measured against the
/// live service on 2026-08-16.</b> The <c>population</c> range filter is <em>silently ignored</em>
/// — 0-0, 1 upwards, string bounds and numeric bounds all return the unfiltered count, identical
/// to a key that does not exist — so the one criterion Frontier's rule actually turns on cannot be
/// asked for. It can, however, be <em>sorted</em> on, and the record carries the number, so the
/// filter is remote for distance and local for population. And <c>is_colonised</c> is a presence
/// flag whose value is discarded: asking for "false" returns the sixteen systems that are
/// colonised, exactly as "true" and "banana" do, which is the worst shape of silent lie in this
/// index and is why neither flag is ever sent.
/// </para>
/// </summary>
public sealed record ColonisationQuery
{
    private ColonisationQuery()
    {
    }

    /// <summary>
    /// Frontier's number, in their own words: the System Colonisation Contact offers "a nearby
    /// unpopulated star system (within a max distance of 15ly)". Measured from the starport the
    /// claim is made at, which is why the reference defaults to where the Commander is rather than
    /// to anywhere they might be going.
    /// </summary>
    public const double ClaimRange = 15;

    public required string ReferenceSystem { get; init; }

    public double MaxDistance { get; init; } = ClaimRange;

    /// <summary>A kind of planet that must be present, in the catalogue's spelling.</summary>
    public string? Subtype { get; init; }

    /// <summary>At least one terraforming candidate.</summary>
    public bool Terraformable { get; init; }

    /// <summary>At least one body with a ring or a belt. Decided by the body search, not the scan.</summary>
    public bool Rings { get; init; }

    /// <summary>How many landable bodies at least. Decided by the body search, not the scan.</summary>
    public int MinimumLandable { get; init; }

    /// <summary>How many candidates to report.</summary>
    public int Size { get; init; } = 3;

    /// <summary>Whether anything beyond the claim rule was asked for.</summary>
    public bool HasObjective => Subtype is not null || Terraformable || Rings || MinimumLandable > 0;

    /// <summary>Whether the answer needs the body search as well as the scan.</summary>
    public bool NeedsBodyDetail => Rings || MinimumLandable > 0;

    /// <summary>
    /// How many systems the scan pulls. Below the 500 the service caps a page at — asking for 501
    /// silently returns 25 rather than erroring — and above the 106 systems the densest 15 light
    /// years measured holds, so the unpopulated ones are in the page wherever it is asked from.
    /// </summary>
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

            // Clamped to the rule rather than refused above it. A Commander asking for 30 light
            // years is not making an error worth an error message — they are asking for systems
            // they cannot claim, and the answer to that is the fifteen they can.
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : ClaimRange, 1, ClaimRange),

            // Five is the ceiling rather than the twenty the other searches allow, because each
            // candidate here is four lines rather than one and this is an answer somebody is about
            // to hear read aloud.
            Size = Math.Clamp(size <= 0 ? 3 : size, 1, 5),
        };

        return true;
    }
}
