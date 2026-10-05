using System.Reflection;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class PowerplayMeritsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string Pledge =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""";

    private const string Quiet = ",\"PowerplayStateReinforcement\":801,\"PowerplayStateUndermining\":0";
    private const string Undermined = ",\"PowerplayStateReinforcement\":801,\"PowerplayStateUndermining\":6977";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(string system, string power, string state, string counts = "") =>
        "{\"timestamp\":\"3311-01-01T00:01:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system
        + "\",\"ControllingPower\":\"" + power + "\",\"PowerplayState\":\"" + state + "\"" + counts + "}";

    private static string Exit(string system) =>
        "{\"timestamp\":\"3311-01-01T00:02:00Z\",\"event\":\"SupercruiseExit\",\"StarSystem\":\"" + system
        + "\",\"Body\":\"" + system + " 1\"}";

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

    private static CalloutContext Context(CommanderGameState state, bool priming = false) =>
        new(Start, priming, state, GameStatus.Unknown, NavRoute.None, []);

    [Fact]
    public void ALocationHoldsThePowerplayStateAndBothScores()
    {
        var state = StateFrom(Pledge, Jump("Deciat", "Edmund Mahon", "Fortified", Undermined));

        Assert.Equal("Fortified", state.Location.PowerplayState);
        Assert.Equal(801, state.Location.PowerplayStateReinforcement);
        Assert.Equal(6977, state.Location.PowerplayStateUndermining);
    }

    [Fact]
    public void YourOwnQuietSystemSaysReinforcingEarnsLess()
    {
        var state = StateFrom(Pledge, Jump("Deciat", "Edmund Mahon", "Fortified", Quiet), Exit("Deciat"));

        var spoken = Assert.Single(new PowerplayMeritsCallout().Examine(Context(state)));

        Assert.Equal("Reinforcing here earns reduced merits. Nobody has undermined this system this cycle.", spoken.Text);
    }

    [Fact]
    public void YourOwnUnderminedSystemSaysReinforcingPaysMore()
    {
        var state = StateFrom(Pledge, Jump("Deciat", "edmund mahon", "Stronghold", Undermined), Exit("Deciat"));

        var spoken = Assert.Single(new PowerplayMeritsCallout().Examine(Context(state)));

        Assert.Equal("Rivals have undermined this system this cycle. Reinforcing pays more here than in a quiet system, while that lasts.", spoken.Text);
    }

    [Fact]
    public void ARivalsSystemSaysUnderminingEarnsFifteenPercentMore()
    {
        var state = StateFrom(Pledge, Jump("Cubeo", "Yuri Grom", "Exploited", Undermined), Exit("Cubeo"));

        var spoken = Assert.Single(new PowerplayMeritsCallout().Examine(Context(state)));

        Assert.Equal("Undermining here earns fifteen percent more merits.", spoken.Text);
        Assert.Equal(15, PowerplayRules.RivalUndermining.Percent);
    }

    [Fact]
    public void AnUnoccupiedSystemSaysNothing()
    {
        var state = StateFrom(
            Pledge,
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Wolf 359","PowerplayState":"Unoccupied"}""",
            Exit("Wolf 359"));

        Assert.Empty(new PowerplayMeritsCallout().Examine(Context(state)));
        Assert.Null(PowerplayRules.SituationLine(state.Pledge, state.Location));
    }

    [Fact]
    public void AStateTheRulesDoNotCoverSaysNothing()
    {
        var state = StateFrom(Pledge, Jump("Deciat", "Edmund Mahon", "HomeSystem", Quiet), Exit("Deciat"));

        Assert.Empty(new PowerplayMeritsCallout().Examine(Context(state)));
        Assert.Null(PowerplayRules.SituationLine(state.Pledge, state.Location));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var state = StateFrom(Pledge, Jump("Cubeo", "Yuri Grom", "Exploited"), Exit("Cubeo"));
        var callout = new PowerplayMeritsCallout();

        Assert.Empty(callout.Examine(Context(state, priming: true)));
        Assert.Empty(callout.Examine(Context(state)));
    }

    [Fact]
    public void NothingIsSaidWithoutAPledge()
    {
        var state = StateFrom(Jump("Cubeo", "Yuri Grom", "Exploited"), Exit("Cubeo"));

        Assert.Empty(new PowerplayMeritsCallout().Examine(Context(state)));
    }

    [Fact]
    public void ASystemIsSaidOncePerSession()
    {
        var callout = new PowerplayMeritsCallout();
        var cubeo = StateFrom(Pledge, Jump("Cubeo", "Yuri Grom", "Exploited"), Exit("Cubeo"));

        Assert.Single(callout.Examine(Context(cubeo)));
        Assert.Empty(callout.Examine(Context(cubeo)));

        var deciat = StateFrom(Pledge, Jump("Deciat", "Yuri Grom", "Exploited"), Exit("Deciat"));
        Assert.Single(callout.Examine(Context(deciat)));

        Assert.Empty(callout.Examine(Context(cubeo)));
    }

    [Fact]
    public void ArrivingInSupercruiseSaysNothingYet()
    {
        var state = StateFrom(Pledge, Jump("Cubeo", "Yuri Grom", "Exploited"));

        Assert.Empty(new PowerplayMeritsCallout().Examine(Context(state)));
    }

    [Fact]
    public void TheSituationLineCarriesTheModifierInYourOwnSystem()
    {
        var state = StateFrom(Pledge, Jump("Deciat", "Edmund Mahon", "Fortified", Quiet), Exit("Deciat"));

        Assert.Contains(
            "Merits here: reinforcing −35%, and −20% more because nobody has undermined this system this cycle; +35% more if the galaxy map marks it for reinforcement.",
            Situation.Describe(state),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSituationLineCarriesTheModifierInARivalsSystem()
    {
        var state = StateFrom(Pledge, Jump("Cubeo", "Yuri Grom", "Exploited", Undermined), Exit("Cubeo"));

        Assert.Contains(
            "Merits here: undermining +15%; +25% more if the galaxy map marks it for undermining.",
            Situation.Describe(state),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRuleNamesItsGameVersionAndCheckDate()
    {
        var rules = typeof(PowerplayRules)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PowerplayRule))
            .Select(f => (PowerplayRule)f.GetValue(null)!)
            .ToList();

        Assert.Equal(PowerplayRules.All.Count, rules.Count);

        foreach (var rule in rules)
        {
            Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", rule.Since);
            Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", rule.CheckedAgainst);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", rule.CheckedOn);
            Assert.Contains(rule, PowerplayRules.All);
        }
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().PowerplayMerits);
}
