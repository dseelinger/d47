using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class NoCoreNamesAnotherTests
{
    /// <summary>The one exception, declared rather than tolerated.</summary>
    private static readonly (string Core, string May)[] Allowed = [("Analyst Prime", "Cora")];

    [Fact]
    public void NoShippedCoreNamesAnotherShippedCore()
    {
        var names = PersonaCatalog.Shipped.Select(core => core.Name).ToArray();
        var offences = new List<string>();

        foreach (var core in PersonaCatalog.Shipped)
        {
            // Everything the model is handed or the Commander is shown for this core.
            var said = string.Join(
                "\n",
                core.Tagline,
                core.Body,
                core.Intro,
                core.Return,
                core.VoiceHint.Description);

            foreach (var other in names.Where(name => name != core.Name))
            {
                if (!Mentions(said, other))
                {
                    continue;
                }

                if (Allowed.Any(pair => pair.Core == core.Name && pair.May == other))
                {
                    continue;
                }

                offences.Add($"{core.Name} names {other}");
            }
        }

        Assert.Empty(offences);
    }

    /// <summary>And the exception is real rather than a rule nobody exercises.</summary>
    [Fact]
    public void AnalystPrimeStillNamesCora()
    {
        var prime = PersonaCatalog.Shipped.Single(core => core.Name == "Analyst Prime");

        Assert.True(
            Mentions(prime.Body, "Cora") || Mentions(prime.Intro, "Cora"),
            "Analyst Prime is about Cora; without her there is no character left");
    }

    /// <summary>
    /// Warden in particular, by name, because he is the one that was reported and a general assertion
    /// passing tells you less than the specific one that failed.
    /// </summary>
    [Fact]
    public void WardenNamesNobody()
    {
        var warden = PersonaCatalog.Shipped.Single(core => core.Name == "Warden");
        var said = string.Join("\n", warden.Body, warden.Intro, warden.Return);

        foreach (var other in PersonaCatalog.Shipped.Where(core => core.Name != "Warden"))
        {
            Assert.False(Mentions(said, other.Name), $"Warden names {other.Name}");
        }
    }

    /// <summary>
    /// Whole word, so a core called Chart is not found inside "chart" and Kex is not found inside a
    /// longer word.
    /// </summary>
    private static bool Mentions(string text, string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"(?<![\w-])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\w-])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(2));
}
