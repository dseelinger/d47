using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>A workbench aid: what the ladder makes of a line. Skipped, like the other harnesses.</summary>
public class LadderHarness
{
    [Fact(Skip = "Workbench aid. Remove the skip to read what the ladder says.")]
    public void PrintTheLadder()
    {
        var rules = new Phonemiser();

        var lines = new[]
        {
            "COL 385 SECTOR B0-GQPI",
            "Shinrarta Dezhra",
            "Docking permission granted at Deciat.",
            "well-known re-entry",
            "Kusauts",
            "Sol",
        };

        Assert.Fail(string.Join(
            "\n",
            lines.Select(line => $"{line,-40} -> {rules.ToPhonemes(line, "bm_george")}")));
    }
}
