using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// No core names another core (remediation.md 14, item 4).
/// <para>
/// Reported from a running d47: Warden said <em>"Cora used to count like that when she was
/// checking a relay — she'd have called this sloppy work if I missed one."</em> The Commander
/// heard one AI talking about another AI in the same application, which is what it is: Cora is a
/// core they can select from a list.
/// </para>
/// <para>
/// <b>It was in the shipped text on purpose, which is why nothing caught it.</b>
/// <c>guardian-personas.md</c> prescribed it — the isolation model there is not that a core never
/// names another, it is that no core knows another is <em>present</em>, and Warden believed Cora
/// dead. That reads well on the page and badly in a cockpit, because the reader can see her in
/// the picker. The pack was rewritten to drop it and the shipped text has followed.
/// </para>
/// <para>
/// So this is the test that would have caught it, and the one that catches the next edit. The
/// property is deliberately about the <b>name as the Commander sees it</b>: the picker lists
/// <see cref="D47.Core.Persona.Persona.Name"/>, and it is that string appearing in somebody
/// else's mouth that gives the game away. An unnamed reference — "my secondary core", "a
/// preservation core from a rival clan" — is the pack working as designed and stays.
/// </para>
/// </summary>
public class NoCoreNamesAnotherTests
{
    /// <summary>
    /// The one exception, declared rather than tolerated.
    /// <para>
    /// Analyst Prime is <em>about</em> Cora. The pack's hard rule for him is that he leaks it
    /// through "unnecessary comparisons to Cora, unprompted mentions of her, and defensive
    /// rebuttals to criticism she is not present to make" — and the pack's most recent revision
    /// strengthened that rather than removing it. Take this out and there is no character left.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// And the exception is real rather than a rule nobody exercises. A test that permits
    /// something nothing does is a test that will be deleted as dead the first time somebody
    /// tidies, taking the permission with it.
    /// </summary>
    [Fact]
    public void AnalystPrimeStillNamesCora()
    {
        var prime = PersonaCatalog.Shipped.Single(core => core.Name == "Analyst Prime");

        Assert.True(
            Mentions(prime.Body, "Cora") || Mentions(prime.Intro, "Cora"),
            "Analyst Prime is about Cora; without her there is no character left");
    }

    /// <summary>
    /// Warden in particular, by name, because he is the one that was reported and a general
    /// assertion passing tells you less than the specific one that failed.
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
    /// Whole word, so a core called Chart is not found inside "chart" and Kex is not found
    /// inside a longer word. Hyphens count as part of a word for the same reason.
    /// <para>
    /// Case-insensitive, because "the Heretic" mid-sentence is the same giveaway as "The
    /// Heretic" and a rule that only caught the capitalised form would be a rule anybody could
    /// walk past without noticing.
    /// </para>
    /// </summary>
    private static bool Mentions(string text, string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"(?<![\w-])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\w-])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(2));
}
