using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// RouteCallout carries a jump count and a system name in one sentence (#91), which is why #124 tests
/// against it: proof that the seam hands every provider the same words for a name, whichever one is
/// selected, and that a quantity in the same sentence stays a quantity.
/// </summary>
public class ARouteCalloutSoundsTheSameOnEveryProviderTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static NavRoute Route(params (string System, string Class, double X)[] hops) => new()
    {
        Hops = [.. hops.Select(hop => new RouteHop(hop.System, hop.Class) { Position = (hop.X, 0, 0) })],
        ReadAt = Start,
    };

    /// <summary>The sentence RouteCallout actually produces, with a jump count and the named system in it.</summary>
    private static string RouteProgress(string nextSystem)
    {
        var callout = new RouteCallout { EveryNJumps = 1 };
        var route = Route(("Departure", "K", 0), (nextSystem, "K", 10), ("Beyond", "K", 20));
        var jump = """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Departure","JumpDist":10}""";

        var context = new CalloutContext(
            Start.AddSeconds(1),
            false,
            StateFrom(jump),
            GameStatus.Unknown,
            route,
            [Event(jump)]);

        return callout.Examine(context).Single(announcement => announcement.Key == "route.progress").Text;
    }

    /// <summary>What SpeechPipeline hands every provider's <see cref="ITtsProvider.SynthesizeAsync"/>.</summary>
    private static string AtTheSeam(string sentence) => SpokenUnits.Rewrite(SpokenDesignations.Rewrite(sentence));

    private static ElevenLabsTtsProvider Elevenlabs() =>
        new(() => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance);

    [Fact]
    public void ACatalogueDesignationIsReadDigitByDigit()
    {
        var sentence = RouteProgress("HIP 3269");
        Assert.Contains("HIP 3269", sentence, StringComparison.Ordinal);

        var spoken = AtTheSeam(sentence);

        Assert.Contains("three two six nine", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("3269", spoken, StringComparison.Ordinal);

        // Nothing with no letters and no digits left for a provider to read its own way, so every
        // provider — including ElevenLabs, which puts its own spelling on the wire — is handed the same
        // words.
        Assert.Equal(spoken, Elevenlabs().Billable(spoken));
    }

    [Fact]
    public void AThreeDigitDesignationKeepsTheCasualReading()
    {
        var sentence = RouteProgress("Col 385");
        var spoken = AtTheSeam(sentence);

        Assert.Contains("three eighty-five", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("385", spoken, StringComparison.Ordinal);

        // The case #124 named directly: once the seam has turned "385" into words, ElevenLabs'
        // SpokenNumbers.Expand must leave it alone rather than giving it its own full reading ("three
        // hundred and eighty-five") for what is now a plain word, not a numeral.
        var billed = Elevenlabs().Billable(spoken);

        Assert.Equal(spoken, billed);
        Assert.DoesNotContain("three hundred", billed, StringComparison.Ordinal);
    }

    /// <summary>
    /// A decimal point or a grouping comma marks a measured quantity, so its digits stay put beside a
    /// unit spelled out in words — unlike the designation half of the same sentence, which the length
    /// rule has already turned into words.
    /// </summary>
    [Theory]
    [InlineData("1234.5 ly", "1234.5 light years")]
    [InlineData("12,000 cr", "12,000 credits")]
    public void AQuantityWithAPointOrAGroupingCommaKeepsItsDigits(string quantity, string expected)
    {
        var spoken = AtTheSeam($"{RouteProgress("HIP 3269")} {quantity} out.");

        Assert.Contains("three two six nine", spoken, StringComparison.Ordinal);
        Assert.Contains(expected, spoken, StringComparison.Ordinal);
    }

    /// <summary>
    /// A percentage has no unit SpokenUnits knows and no point or comma to guard it, so it takes the
    /// same casual reading a short designation does — which is still a quantity, not the digit-by-digit
    /// reading a four-digit run gets under the length rule.
    /// </summary>
    [Fact]
    public void APercentageIsReadCasuallyRatherThanDigitByDigit()
    {
        var spoken = AtTheSeam($"{RouteProgress("HIP 3269")} 75 percent out.");

        Assert.Contains("three two six nine", spoken, StringComparison.Ordinal);
        Assert.Contains("seventy-five percent", spoken, StringComparison.Ordinal);
    }
}
