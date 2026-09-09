namespace D47.Core.Help;

/// <summary>The changelog, inside the binary (#50).</summary>
public static class Changelog
{
    private const string ResourceName = "D47.Core.Changelog";

    private static readonly Lazy<string> Loaded = new(Read);

    /// <summary>The whole file, newest release first, exactly as it shipped.</summary>
    public static string Text => Loaded.Value;

    /// <summary>Whether there is one at all.</summary>
    public static bool Exists => Text.Length > 0;

    private static string Read()
    {
        var assembly = typeof(Changelog).Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
