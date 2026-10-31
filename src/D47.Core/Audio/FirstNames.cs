namespace D47.Core.Audio;

/// <summary>The sex of given names read from real journals, shipped with the app as <c>FirstNames.tsv</c>.</summary>
public static class FirstNames
{
    private const string ResourceName = "D47.Core.FirstNames";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Loaded = new(() =>
    {
        var assembly = typeof(FirstNames).Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} could not be opened.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    });

    /// <summary>The shipped table, by given name, ignoring case.</summary>
    public static IReadOnlyDictionary<string, string> Shipped => Loaded.Value;

    /// <summary>Reads <c>name TAB state</c> lines; blank lines and lines starting with # are skipped.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length == 2)
            {
                table[cells[0]] = cells[1];
            }
        }

        return table;
    }
}
