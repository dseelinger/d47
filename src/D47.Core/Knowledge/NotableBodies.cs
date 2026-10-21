using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>Traits ordered rarest first.</summary>
public enum NotableKind
{
    CollidingBinary,
    ShepherdMoon,
    NestedMoon,
    EccentricOrbit,
    FastRotator,
    WideRings,
    PremiumFsdInjection,
    LandableAtmosphere,
}

/// <summary>One notable trait of a body; <paramref name="Detail"/> is the partner, period or material the kind names.</summary>
public sealed record NotableTrait(NotableKind Kind, string? Detail = null);

/// <summary>Which notable traits a scanned body has, given the other bodies scanned in its system.</summary>
public static class NotableBodies
{
    public const double EccentricityThreshold = 0.8;
    public const double FastRotationSeconds = 6 * 3600;
    public const double WideRingRadii = 10;
    public const int PremiumIngredientsNeeded = 5;

    private const string PremiumModule = "FSD Injection";
    private const int PremiumGrade = 3;

    public static IReadOnlyList<NotableTrait> Of(BodyScan body, IReadOnlyList<BodyScan> system)
    {
        var traits = new List<NotableTrait>();

        if (CollidingPartner(body, system) is { } partner)
        {
            traits.Add(new NotableTrait(NotableKind.CollidingBinary, partner.BodyName));
        }

        if (IsShepherd(body, system))
        {
            traits.Add(new NotableTrait(NotableKind.ShepherdMoon));
        }

        if (body.Parents.Count(parent => parent.Kind == "Planet") >= 2)
        {
            traits.Add(new NotableTrait(NotableKind.NestedMoon));
        }

        if (body.Eccentricity >= EccentricityThreshold)
        {
            traits.Add(new NotableTrait(NotableKind.EccentricOrbit));
        }

        if (!body.TidalLock && body.RotationPeriod is { } rotation && Math.Abs(rotation) < FastRotationSeconds)
        {
            traits.Add(new NotableTrait(
                NotableKind.FastRotator,
                ((int)Math.Round(Math.Abs(rotation) / 3600)).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (body.Radius is { } radius &&
            body.Rings.Any(ring => IsRing(ring.Name) && ring.OuterRad - ring.InnerRad > WideRingRadii * radius))
        {
            traits.Add(new NotableTrait(NotableKind.WideRings));
        }

        if (body.Landable && PremiumMissing(body) is { } missing)
        {
            traits.Add(new NotableTrait(NotableKind.PremiumFsdInjection, missing.Length == 0 ? null : missing));
        }

        if (body.Landable && !string.IsNullOrWhiteSpace(body.Atmosphere))
        {
            traits.Add(new NotableTrait(NotableKind.LandableAtmosphere));
        }

        return traits;
    }

    private static bool IsRing(string name) => name.EndsWith("Ring", StringComparison.OrdinalIgnoreCase);

    private static BodyScan? CollidingPartner(BodyScan body, IReadOnlyList<BodyScan> system)
    {
        if (body.Parents.Count == 0 || body.Parents[0].Kind != "Null")
        {
            return null;
        }

        var barycentre = body.Parents[0].BodyId;
        var others = system
            .Where(other => other.BodyName != body.BodyName && other.Parents.Count > 0 && other.Parents[0] == ("Null", barycentre))
            .ToList();

        if (others.Count != 1 ||
            others[0] is not { } partner ||
            body.SemiMajorAxis is not { } a1 || partner.SemiMajorAxis is not { } a2 ||
            body.Radius is not { } r1 || partner.Radius is not { } r2 ||
            body.Eccentricity is not { } e)
        {
            return null;
        }

        return (a1 + a2) * (1 - e) < r1 + r2 ? partner : null;
    }

    private static bool IsShepherd(BodyScan body, IReadOnlyList<BodyScan> system)
    {
        if (body.Parents.Count == 0 || body.Parents[0].Kind != "Planet" || body.SemiMajorAxis is not { } axis)
        {
            return false;
        }

        var parentId = body.Parents[0].BodyId;
        var parent = system.FirstOrDefault(other => other.BodyId == parentId && other.SystemAddress == body.SystemAddress);

        return parent is not null && parent.Rings.Any(ring => IsRing(ring.Name) && axis < ring.OuterRad);
    }

    /// <summary>The missing ingredient's display name, empty when none is missing, null when too few are present.</summary>
    private static string? PremiumMissing(BodyScan body)
    {
        var recipe = BlueprintCatalogue.All
            .FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.Synthesis &&
                blueprint.Module == PremiumModule &&
                blueprint.Grade == PremiumGrade);

        if (recipe is null)
        {
            return null;
        }

        var missing = recipe.Ingredients
            .Where(ingredient => !body.Materials.Any(material =>
                string.Equals(material.Name, ingredient.Symbol, StringComparison.OrdinalIgnoreCase)))
            .Select(ingredient => MaterialCatalogue.Find(ingredient.Symbol)?.Name ?? ingredient.Symbol)
            .ToList();

        return recipe.Ingredients.Count - missing.Count >= PremiumIngredientsNeeded
            ? missing.FirstOrDefault() ?? ""
            : null;
    }
}
