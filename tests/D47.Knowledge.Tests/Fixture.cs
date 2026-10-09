using System.Reflection;
using System.Text.Json;

namespace D47.Knowledge.Tests;

/// <summary>Reads the JSON files under <c>Fixtures</c> from the test assembly's embedded resources.</summary>
internal static class Fixture
{
    public static string Text(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Fixtures.{name}")
            ?? throw new FileNotFoundException($"No embedded fixture {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static JsonDocument Json(string name) => JsonDocument.Parse(Text(name));
}
