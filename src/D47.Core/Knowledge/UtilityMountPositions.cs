namespace D47.Core.Knowledge;

public enum MountHeight
{
    Top,
    Bottom,
}

public enum MountLength
{
    Fore,
    Aft,
}

/// <summary>Where one utility mount sits on the hull; either part is null where the outfitting view shows neither.</summary>
public sealed record MountPosition(string Hull, string Slot, MountHeight? Height, MountLength? Length);

/// <summary>
/// Hand-kept utility mount positions, recorded from the in-game outfitting view. No data source d47
/// reads carries them, and <c>tools/gen-elite-specs.py</c> would erase them from the generated table.
/// </summary>
public static class UtilityMountPositions
{
    /// <summary>Rows keyed by lower-case hull symbol and the journal's slot name.</summary>
    public static IReadOnlyList<MountPosition> All { get; } = [];

    public static MountPosition? Of(string hull, string slot) =>
        All.FirstOrDefault(row =>
            string.Equals(row.Hull, hull, StringComparison.Ordinal)
            && string.Equals(row.Slot, slot, StringComparison.Ordinal));
}
