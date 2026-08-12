namespace D47.Core;

/// <summary>
/// Marker for the Core assembly. Exists so the dependency-boundary test has a stable
/// handle on it without reaching for a real type that may move.
/// </summary>
public static class CoreAssembly
{
    public static System.Reflection.Assembly Reference => typeof(CoreAssembly).Assembly;
}
