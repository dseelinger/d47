using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Frontier writes <c>$Key;</c> into <c>_Localised</c> for some names; d47 names those from the raw symbol.</summary>
public sealed class APlaceholderIsNeverSpokenAsANameTests
{
    private const string DominatorLoad =
        """{"timestamp":"2026-10-01T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Quill","Ship":"TacticalSuit_Class3","Ship_Localised":"$TacticalSuit_Class1_Name;","ShipID":4293000001,"ShipName":"","Credits":1000}""";

    private const string DominatorFields =
        """
        "Name":"Quill","FID":"F1234567","Ship":"TacticalSuit_Class3","Ship_Localised":"$TacticalSuit_Class1_Name;","ShipID":4293000001,"ShipName":"","Credits":1000
        """;

    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static JournalEvent Event(string text)
    {
        Assert.True(JournalEvent.TryParse(text, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static string Say(string text) => JournalSentence.For(Event(text));

    private static LogDigest PreviousSession(JournalCorpus corpus, DateTimeOffset next)
    {
        var range = LogRanges.PreviousSession(corpus.FileSystem, next, corpus.Files, NullLogger.Instance)!;

        return new LogDigestBuilder(corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(corpus.Files, range);
    }

    [Theory]
    [InlineData("""{"Ship":"viper","Ship_Localised":"$VIPER_NAME;"}""", "viper")]
    [InlineData("""{"Ship":"viper","Ship_Localised":"  $VIPER_NAME;"}""", "viper")]
    [InlineData("""{"Ship":"viper","Ship_Localised":"Viper Mk III"}""", "Viper Mk III")]
    [InlineData("""{"Ship":"viper"}""", "viper")]
    public void JournalJsonNamedSkipsAPlaceholder(string json, string expected) =>
        Assert.Equal(expected, Json(json).Named("Ship"));

    [Theory]
    [InlineData("""{"Ship":"viper","Ship_Localised":"$VIPER_NAME;"}""", "viper")]
    [InlineData("""{"Ship":"viper","Ship_Localised":"Viper Mk III"}""", "Viper Mk III")]
    [InlineData("""{"Ship":"viper"}""", "viper")]
    public void JournalTextNamedSkipsAPlaceholder(string json, string expected) =>
        Assert.Equal(expected, JournalText.Named(Json(json), "Ship"));

    [Fact]
    public void TheRecapNamesASuitTheCommanderFinishedIn()
    {
        var corpus = new JournalCorpus();
        var next = Evening.AddDays(1);

        corpus.Journal(
            Evening,
            JournalCorpus.Event(Evening, "LoadGame", DominatorFields),
            JournalCorpus.Event(Evening.AddMinutes(30), "Docked", "\"StationName\":\"Garay Terminal\",\"StarSystem\":\"Deciat\""));
        corpus.Journal(next, JournalCorpus.LoadGame(next), JournalCorpus.Jump(next.AddMinutes(5), "Sol", 12));

        var line = RecapCallout.Compose(PreviousSession(corpus, next));

        Assert.NotNull(line);
        Assert.Contains("finished docked at Garay Terminal, Deciat in a Dominator Suit, grade 3", line);
        Assert.DoesNotContain('$', line);
    }

    [Fact]
    public void TheRecapSaysWoreWhenNothingClosedTheSession()
    {
        var corpus = new JournalCorpus();
        var next = Evening.AddDays(1);

        corpus.Journal(
            Evening,
            JournalCorpus.Event(Evening, "LoadGame", DominatorFields),
            JournalCorpus.Event(Evening.AddMinutes(20), "Promotion", "\"Combat\":5"));
        corpus.Journal(next, JournalCorpus.LoadGame(next), JournalCorpus.Jump(next.AddMinutes(5), "Sol", 12));

        Assert.Contains("you wore a Dominator Suit, grade 3", RecapCallout.Compose(PreviousSession(corpus, next)));
    }

    [Fact]
    public void TheLogbookOpensOnFootForASuitAndFlyingForAShip()
    {
        var corpus = new JournalCorpus();
        var next = Evening.AddDays(1);

        corpus.Journal(
            Evening,
            JournalCorpus.Event(Evening, "LoadGame", DominatorFields),
            JournalCorpus.Jump(Evening.AddMinutes(5), "Sol", 12));
        corpus.Journal(next, JournalCorpus.LoadGame(next), JournalCorpus.Jump(next.AddMinutes(5), "Sol", 12));

        Assert.Contains(
            PreviousSession(corpus, next).Facts,
            fact => fact.Statement.Contains("Entered the game on foot in a Dominator Suit, grade 3", StringComparison.Ordinal));

        var ships = new JournalCorpus();

        ships.Journal(Evening, JournalCorpus.LoadGame(Evening), JournalCorpus.Jump(Evening.AddMinutes(5), "Sol", 12));
        ships.Journal(next, JournalCorpus.LoadGame(next), JournalCorpus.Jump(next.AddMinutes(5), "Sol", 12));

        Assert.Contains(
            PreviousSession(ships, next).Facts,
            fact => fact.Statement.StartsWith("Entered the game flying the Python", StringComparison.Ordinal));
    }

    [Fact]
    public void ALoadGameOnFootReadsAsASuit() =>
        Assert.Equal("Loaded, on foot in the Dominator Suit, grade 3", Say(DominatorLoad));

    [Fact]
    public void ALoadGameInAShipReadsAsItDid() =>
        Assert.Equal(
            "Loaded, flying the Python",
            Say("""{"timestamp":"2026-10-01T10:00:00Z","event":"LoadGame","Ship":"Python","Ship_Localised":"Python","ShipID":4}"""));

    [Theory]
    [InlineData("TacticalSuit_Class1", "Dominator Suit", "Dominator Suit, grade 1")]
    [InlineData("TacticalSuit_Class3", "$TacticalSuit_Class1_Name;", "Dominator Suit, grade 3")]
    [InlineData("FlightSuit", "Flight Suit", "Flight Suit")]
    public void ASuitIsNamedByFamilyAndGradeFromItsSymbol(string symbol, string localised, string expected)
    {
        Assert.Equal(
            $"Wearing the {expected}",
            Say($$"""{"timestamp":"2026-10-01T10:00:00Z","event":"SuitLoadout","SuitName":"{{symbol}}","SuitName_Localised":"{{localised}}"}"""));
        Assert.Equal(
            $"Changed into the {expected}",
            Say($$"""{"timestamp":"2026-10-01T10:00:00Z","event":"SwitchSuitLoadout","SuitName":"{{symbol}}","SuitName_Localised":"{{localised}}"}"""));
        Assert.Equal(
            $"Bought the {expected}",
            Say($$"""{"timestamp":"2026-10-01T10:00:00Z","event":"BuySuit","Name":"{{symbol}}","Name_Localised":"{{localised}}"}"""));
    }

    [Fact]
    public void ATargetedShipIsNamedFromItsHullNotItsPlaceholder()
    {
        const string line = """{"timestamp":"2026-10-01T10:00:00Z","event":"ShipTargeted","TargetLocked":true,"Ship":"viper","Ship_Localised":"$VIPER_NAME;","ScanStage":1,"PilotName":"$npc_name_decorate:#name=Doug Quiad;","PilotName_Localised":"Doug Quiad"}""";
        var hull = EliteSpecifications.HullSaid("viper");

        Assert.Equal($"Targeted a {hull}", Say(line));
        Assert.Equal(hull, TargetedShip.From(Event(line))!.Ship);
        Assert.DoesNotContain('$', hull);
    }

    [Fact]
    public void ABountyOnASuitAiIsClaimedOnASoldier()
    {
        foreach (var target in new[] { "assaultsuitai_class1", "lightassaultsuitai_class1", "closesuitai_class1", "rangedsuitai_class1" })
        {
            var line = $$"""{"timestamp":"2026-10-01T10:00:00Z","event":"Bounty","Target":"{{target}}","Target_Localised":"$AssaultSuitAI_Class1_Name;","TotalReward":5000}""";

            Assert.Equal("Bounty claimed on a soldier — 5,000 Cr", Say(line));
            Assert.Equal(
                "Their largest bounty was 5,000 credits for a soldier.",
                ChapterNotables.Of(Event(line))!.Text);
        }
    }

    [Fact]
    public void AnExperimentalEffectWithAPlaceholderIsNamedFromTheBlueprintTable()
    {
        var module = Json("""{"Slot":"PrimaryWeapon1","Item":"hpt_plasmaaccelerator_fixed_huge","On":true,"Priority":1,"Engineering":{"Engineer":"Etienne Dorn","BlueprintName":"Weapon_Overcharged","Level":5,"Quality":1.0,"ExperimentalEffect":"special_plasma_slug","ExperimentalEffect_Localised":"$special_plasma_slug_name;"}}""");

        Assert.Equal("Plasma Slug", ShipModule.From(module).Experimental);
    }
}
