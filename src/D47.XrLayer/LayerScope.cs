namespace D47.XrLayer;

/// <summary>Decides whether the layer acts in the process that loaded it.</summary>
internal static class LayerScope
{
    public const string EliteExecutable = "EliteDangerous64.exe";

    /// <summary>True when the executable at <paramref name="processPath"/> is Elite's.</summary>
    public static bool AppliesTo(string? processPath) =>
        !string.IsNullOrEmpty(processPath)
        && string.Equals(Path.GetFileName(processPath), EliteExecutable, StringComparison.OrdinalIgnoreCase);
}
