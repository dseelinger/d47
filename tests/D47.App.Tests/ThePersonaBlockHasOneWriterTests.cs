using Xunit;

namespace D47.App.Tests;

public sealed class ThePersonaBlockHasOneWriterTests
{
    [Fact]
    public void ThePersonaBlockHasOneWriter()
    {
        var writes = AppSource.CodeLines("Turns.Persona =")
            .Where(line => line.Text.StartsWith("Turns.Persona =", StringComparison.Ordinal))
            .ToList();

        var writer = AppSource.Method("ApplyPersonaBlock").CodeLines();

        Assert.True(
            writes.Count == 1 && writer.Any(line => line.File == writes[0].File && line.Line == writes[0].Line),
            "Turns.Persona is written outside ApplyPersonaBlock:\n" + string.Join(Environment.NewLine, writes));
    }
}
