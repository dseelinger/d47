using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class FullAndReverseThrottleAreSpokenTests
{
    private static string? Route(string phrase) =>
        GameActions.All
            .FirstOrDefault(action => action.Phrases.Any(p => string.Equals(p.Phrase, phrase, StringComparison.OrdinalIgnoreCase)))
            ?.Id;

    [Theory]
    [InlineData("full throttle", "throttle_full")]
    [InlineData("full speed", "throttle_full")]
    [InlineData("throttle to a hundred", "throttle_full")]
    [InlineData("throttle to minus twenty-five", "throttle_reverse_25")]
    [InlineData("throttle to minus fifty", "throttle_reverse_50")]
    [InlineData("half reverse", "throttle_reverse_50")]
    [InlineData("throttle to minus seventy-five", "throttle_reverse_75")]
    [InlineData("full reverse", "throttle_reverse_full")]
    [InlineData("throttle to minus a hundred", "throttle_reverse_full")]
    [InlineData("reverse thrust", "reverse_thrust")]
    [InlineData("toggle reverse thrust", "reverse_thrust")]
    public void EachPhraseReachesItsActionWithoutTheModel(string phrase, string id) =>
        Assert.Equal(id, Route(phrase));

    [Fact]
    public void TheReverseSettingsAreNormalSpaceOnly()
    {
        var reverse = GameActions.All.Where(a => a.Id.StartsWith("throttle_reverse_", StringComparison.Ordinal) || a.Id == "reverse_thrust");

        Assert.All(reverse, action => Assert.Equal(ControlContext.NormalSpace, action.Contexts));
    }

    [Fact]
    public void FullThrottleIsInTheFlightGroup() =>
        Assert.Equal(GameActions.Flight, GameActions.All.Single(a => a.Id == "throttle_full").Group);
}
