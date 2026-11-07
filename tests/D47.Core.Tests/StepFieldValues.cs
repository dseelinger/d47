using D47.Core.Knowledge;

namespace D47.Core.Tests;

/// <summary>Reads the value rows of a step-fields table, for tests that need the whole list a field allows.</summary>
internal static class StepFieldValues
{
    public static IReadOnlyList<string> Shipped(string name, string field)
    {
        using var stream = typeof(StepEventCatalogue).Assembly.GetManifestResourceStream("D47.Core.StepFields")
            ?? throw new InvalidOperationException("StepFields is not embedded.");
        using var reader = new StreamReader(stream);

        return Of(reader.ReadToEnd(), name, field);
    }

    public static IReadOnlyList<string> Of(string fields, string name, string field)
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var line in fields.Split('\n'))
        {
            var cells = line.TrimEnd('\r').Split('\t');

            if (cells.Length >= 4 && cells[0] == name && cells[1] == field && cells[3].Length > 0)
            {
                values.Add(cells[3]);
            }
        }

        return [.. values];
    }
}
