namespace D47.Core.Knowledge;

/// <summary>What a body or barycentre orbits, read from a <c>Parents</c> chain written nearest first.</summary>
public static class BodyParents
{
    /// <summary>The first entry that is a star or planet, skipping barycentres; null for none.</summary>
    public static int? Nearest(IReadOnlyList<(string Kind, int BodyId)> parents)
    {
        ArgumentNullException.ThrowIfNull(parents);

        foreach (var (kind, bodyId) in parents)
        {
            if (kind != "Null")
            {
                return bodyId;
            }
        }

        return null;
    }

    /// <summary>What the barycentre orbits: the rest of the first chain that passes through it; empty when none does.</summary>
    public static IReadOnlyList<(string Kind, int BodyId)> OfBarycentre(
        int bodyId,
        IEnumerable<IReadOnlyList<(string Kind, int BodyId)>> chains)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (var chain in chains)
        {
            for (var index = 0; index < chain.Count; index++)
            {
                if (chain[index] == ("Null", bodyId))
                {
                    return [.. chain.Skip(index + 1)];
                }
            }
        }

        return [];
    }
}
