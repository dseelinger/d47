using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Humor is a level and a frequency for each of three groups, rolled by d47 line by line (#416).</summary>
public class HumorIsALevelAndAFrequencyPerGroupTests
{
    /// <summary>A roll that always comes up the same number.</summary>
    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int maxValue) => Math.Min(value, maxValue - 1);
    }

    private static HumorRoll Rolling(int value) => new(new FixedRandom(value));

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    public void AGroupAtLevelZeroNeverGetsTheInstruction(int roll)
    {
        Assert.Null(Rolling(roll).ForLine(new HumorDial(0, 100), canBeDirected: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    public void AGroupAtOneHundredPercentAlwaysDoes(int roll)
    {
        Assert.NotNull(Rolling(roll).ForLine(new HumorDial(4, 100), canBeDirected: false));
    }

    [Fact]
    public void TheRollDecidesAgainstTheFrequency()
    {
        var dial = new HumorDial(4, 25);

        Assert.NotNull(Rolling(24).ForLine(dial, canBeDirected: false));
        Assert.Null(Rolling(25).ForLine(dial, canBeDirected: false));
    }

    [Fact]
    public void TheBansHoldUpToSixAndLiftAtSeven()
    {
        Assert.Contains(Humor.Bans, Humor.ForLine(6, canBeDirected: false)!, StringComparison.Ordinal);
        Assert.DoesNotContain(Humor.Bans, Humor.ForLine(7, canBeDirected: false)!, StringComparison.Ordinal);
        Assert.NotEqual(Humor.Describe(6, false), Humor.Describe(7, false));
    }

    [Fact]
    public void LaughterIsOfferedOnlyOnAHitToAVoiceThatCanBeDirected()
    {
        var dial = new HumorDial(5, 100);

        Assert.Contains(Humor.Laughter, Rolling(0).ForLine(dial, canBeDirected: true)!, StringComparison.Ordinal);

        // A provider whose ReadsAudioTags is false is one that cannot be directed.
        Assert.DoesNotContain(Humor.Laughter, Rolling(0).ForLine(dial, canBeDirected: false)!, StringComparison.Ordinal);

        // A miss carries nothing at all.
        Assert.Null(Rolling(0).ForLine(new HumorDial(5, 0), canBeDirected: true));
    }

    [Fact]
    public void TheInstructionRidesAfterTheHistoryAndNotInTheCachedBlock()
    {
        var plain = new PromptAssembly { Persona = "persona", LiveGameState = "state" };
        var funny = plain with { Humor = Humor.ForLine(5, canBeDirected: false) };

        Assert.Equal(plain.RenderCachedSystemBlock(), funny.RenderCachedSystemBlock());
        Assert.Equal("state", plain.TrailingState);
        Assert.EndsWith(funny.Humor!, funny.TrailingState, StringComparison.Ordinal);
        Assert.StartsWith("state", funny.TrailingState, StringComparison.Ordinal);
    }

    [Fact]
    public void AWarningNeverCarriesTheInstruction()
    {
        var brief = new FlavourBrief
        {
            Instruction = "say it",
            NeedsPersona = true,
            NeedsGameState = false,
            NeedsAboutMe = false,
        };

        Assert.Null(FlavourBriefs.HumorGroupOf(new Announcement("any", "Hull at ten percent", CalloutUrgency.Urgent), brief));
        Assert.Null(FlavourBriefs.HumorGroupOf(new Announcement(AnnouncedAttackCallout.HuntedKey, "Someone is hunting you"), brief));
        Assert.Equal(HumorGroup.Cores, FlavourBriefs.HumorGroupOf(new Announcement("any", "Docked"), brief));
    }

    [Fact]
    public void ALineFollowsTheGroupThatSpeaksIt()
    {
        var briefed = new FlavourBrief
        {
            Instruction = "say it",
            NeedsPersona = false,
            NeedsGameState = false,
            NeedsAboutMe = false,
        };

        Assert.Equal(
            HumorGroup.Carrier,
            FlavourBriefs.HumorGroupOf(new Announcement("any", "Jump in five") { Voice = VoiceRole.CarrierCaptain }, briefed));
        Assert.Equal(
            HumorGroup.Carrier,
            FlavourBriefs.HumorGroupOf(new Announcement("any", "Pad four") { Voice = VoiceRole.TowerControl }, briefed));
        Assert.Equal(
            HumorGroup.Npcs,
            FlavourBriefs.HumorGroupOf(new Announcement("any", "Cargo scan") { Voice = VoiceRole.Comms }, briefed));
    }

    [Fact]
    public void ChatterRollsForEachGroupPresentAndSaysWhichIsWhich()
    {
        var settings = new PersonaSettings
        {
            NpcHumor = 8,
            NpcHumorPercent = 100,
            CarrierHumor = 2,
            CarrierHumorPercent = 100,
        };

        var aboard = new NpcChatterCarrier { Owned = true, Present = true };
        var text = NpcChatter.WithHumor("Write it.", aboard, settings, Rolling(0), canBeDirected: false);

        Assert.Contains("Humor for the invented speakers, level 8 of 10", text, StringComparison.Ordinal);
        Assert.Contains($"Humor for {NpcChatter.TowerName} and {NpcChatter.CaptainName}, level 2 of 10", text, StringComparison.Ordinal);

        // Away from the carrier its crew are not in the scene and get no line.
        var away = NpcChatter.WithHumor("Write it.", NpcChatterCarrier.None, settings, Rolling(0), canBeDirected: false);
        Assert.DoesNotContain(NpcChatter.CaptainName, away, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatterWithNoHitIsTheInstructionUnchanged()
    {
        var aboard = new NpcChatterCarrier { Owned = true, Present = true };

        Assert.Equal(
            "Write it.",
            NpcChatter.WithHumor("Write it.", aboard, new PersonaSettings(), Rolling(0), canBeDirected: true));
    }

    [Fact]
    public void AGroupThatMissesBesideOneThatHitsPlaysItStraight()
    {
        var settings = new PersonaSettings { CarrierHumor = 5, CarrierHumorPercent = 100 };
        var aboard = new NpcChatterCarrier { Owned = true, Present = true };

        var text = NpcChatter.WithHumor("Write it.", aboard, settings, Rolling(0), canBeDirected: false);

        Assert.Contains("No humor for the invented speakers", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("true", 3)]
    [InlineData("false", 0)]
    public void AFileWithTheOldToggleLoadsAsCoresAtThreeAndTwentyFive(string humor, int level)
    {
        using var install = new TempInstall();

        File.WriteAllText(install.Paths.SettingsFile, $$"""{ "persona": { "humor": {{humor}} } }""");

        var loaded = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();

        Assert.Equal(level, loaded.Persona.CoreHumor);
        Assert.Equal(25, loaded.Persona.CoreHumorPercent);
        Assert.Equal(0, loaded.Persona.NpcHumor);
        Assert.Equal(0, loaded.Persona.CarrierHumor);
        Assert.Null(loaded.Persona.Humor);
    }

    [Theory]
    [InlineData("persona.coreHumor", "persona.coreHumorPercent")]
    [InlineData("persona.npcHumor", "persona.npcHumorPercent")]
    [InlineData("persona.carrierHumor", "persona.carrierHumorPercent")]
    public void TheFrequencyIsDisabledWhileItsLevelIsZero(string level, string percent)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var levelRow = Row(surface, level);
        var percentRow = Row(surface, percent);

        var off = new D47Settings();
        var on = levelRow.Binding!.Write!(off, "4");

        Assert.True(percentRow.DisabledWhen!(off));
        Assert.False(percentRow.DisabledWhen!(on));
    }

    [Theory]
    [InlineData("11", "10")]
    [InlineData("-3", "0")]
    [InlineData("7", "7")]
    public void ALevelIsHeldBetweenZeroAndTen(string written, string read)
    {
        using var install = new TempInstall();
        var row = Row(TestSurface.For(install), Capabilities.Builtin.PersonaCapability.CoreHumorKey);

        Assert.Equal(read, row.Binding!.Read(row.Binding.Write!(new D47Settings(), written)));
    }

    [Fact]
    public void TheToggleIsGone()
    {
        using var install = new TempInstall();

        Assert.DoesNotContain(
            TestSurface.For(install).Settings.Sections.SelectMany(section => section.Rows),
            row => row.Key == "persona.humor");
    }

    private static SettingRow Row(TestSurface surface, string key) =>
        surface.Settings.Sections.SelectMany(section => section.Rows).Single(row => row.Key == key);
}
