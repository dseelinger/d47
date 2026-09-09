namespace D47.Core.Configuration;

/// <summary>Settings failing to load is loud.</summary>
public sealed class SettingsLoadException(string path, string detail, Exception? inner = null)
    : Exception($"Settings at '{path}' could not be loaded: {detail}", inner)
{
    public string Path { get; } = path;
}
