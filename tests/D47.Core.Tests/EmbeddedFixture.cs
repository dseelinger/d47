using System.Reflection;
using System.Text.Json;

namespace D47.Core.Tests;

/// <summary>Reads fixtures embedded in the test assembly by their <c>folder.file</c> name.</summary>
internal static class EmbeddedFixture
{
    public static string Text(string name)
    {
        using var reader = new StreamReader(Open(name));
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<string> Lines(string name) =>
        [.. Text(name).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    public static JsonDocument Json(string name) => JsonDocument.Parse(Text(name));

    private static Stream Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"Fixtures.{name}")
            ?? throw new FileNotFoundException($"No embedded fixture {name}.");
}
