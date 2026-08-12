namespace D47.Core.Configuration;

/// <summary>
/// Settings failing to load is loud. The companion secret store shrugs instead — a missing
/// secret is a capability being off, not an error (architecture.md §5 D6).
/// </summary>
public sealed class SettingsLoadException(string path, string detail, Exception? inner = null)
    : Exception($"Settings at '{path}' could not be loaded: {detail}", inner)
{
    public string Path { get; } = path;
}
