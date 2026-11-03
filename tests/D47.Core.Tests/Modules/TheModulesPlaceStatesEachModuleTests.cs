using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class TheModulesPlaceStatesEachModuleTests
{
    private static ModuleManifest Manifest(int interval = 120, ModuleCharacter? character = null) =>
        new(1, "ledger", "Bounty ledger", "Ana Vey", "1.4.0", "ledger.exe", [], interval, [], character);

    private static readonly (string, string)[] Cores = [("warden", "Warden"), ("cora", "Cora")];
    private static readonly (string, string)[] YourCores = [("vesper", "Vesper")];
    private static readonly (string, string)[] Characters = [("0a1b2c3d", "Haruka Senda")];

    [Fact]
    public void SpeaksAsOffersTheCoreAboardThenItsOwnCharacterThenCoresThenYours()
    {
        var speakers = ModulePlace.Speakers(Manifest(character: new("Tally", [])), Cores, YourCores, Characters);

        Assert.Equal(
            [
                "The core aboard",
                "Tally · its own character",
                "Warden · core",
                "Cora · core",
                "Vesper · your core",
                "Haruka Senda · character",
            ],
            speakers.Select(s => s.Label));
    }

    [Fact]
    public void AModuleWithNoCharacterOffersNoOwnCharacter()
    {
        var speakers = ModulePlace.Speakers(Manifest(), Cores, YourCores, Characters);

        Assert.DoesNotContain(speakers, s => s.Value == ModulePlace.OwnCharacter);
    }

    [Fact]
    public void NothingChosenSpeaksAsItsOwnCharacterElseTheCoreAboard()
    {
        var withCharacter = Manifest(character: new("Tally", []));
        var without = Manifest();

        Assert.Equal(
            ModulePlace.OwnCharacter,
            ModulePlace.Chosen(withCharacter, new ModuleSettings(), ModulePlace.Speakers(withCharacter, Cores, YourCores, Characters)).Value);

        Assert.Equal(
            ModulePlace.Aboard,
            ModulePlace.Chosen(without, new ModuleSettings(), ModulePlace.Speakers(without, Cores, YourCores, Characters)).Value);
    }

    [Fact]
    public void AChoiceThatIsNoLongerOfferedFallsBackToTheDefault()
    {
        var speakers = ModulePlace.Speakers(Manifest(), Cores, YourCores, Characters);

        var chosen = ModulePlace.Chosen(Manifest(), new ModuleSettings { SpeaksAs = ModulePlace.CharacterValue + "gone" }, speakers);

        Assert.Equal(ModulePlace.Aboard, chosen.Value);
    }

    [Fact]
    public void AnIntervalShorterThanTheModulesMinimumIsNotAllowed()
    {
        var fifteenMinutes = Manifest(interval: 900);

        Assert.False(ModulePlace.IntervalAllowed(ModuleInterval.TenMinutes, fifteenMinutes));
        Assert.True(ModulePlace.IntervalAllowed(ModuleInterval.ThirtyMinutes, fifteenMinutes));
        Assert.True(ModulePlace.IntervalAllowed(ModuleInterval.Own, fifteenMinutes));
    }

    [Fact]
    public void ItsOwnLimitShowsTheMinimumInMinutes()
    {
        Assert.Equal("2 min apart", ModulePlace.Apart(120));
        Assert.Equal("15 min apart", ModulePlace.Apart(900));
        Assert.Equal("150 sec apart", ModulePlace.Apart(150));
    }

    [Fact]
    public void EachStatusIsStatedAsTheDesignWords()
    {
        Assert.Equal("Running. Its pages are on the Modules tab.", Say(ModuleStatus.Running, pages: true));
        Assert.Equal("Running. It has no page.", Say(ModuleStatus.Running, pages: false));
        Assert.Equal("Starting.", Say(ModuleStatus.Starting));
        Assert.Equal("Off.", Say(ModuleStatus.Off));
        Assert.Equal("Refused. Not started.", Say(ModuleStatus.Refused));
        Assert.Equal("Stopped after repeated crashes.", Say(ModuleStatus.StoppedAfterRepeatedCrashes));
    }

    private static string Say(ModuleStatus status, bool pages = false)
    {
        var line = ModulePlace.StatusLine(status, pages);
        return line.Lead + line.Rest;
    }

    [Fact]
    public void WhatLeavesIsNothingWhileOffAndTheDisclosureWhileOn()
    {
        var manifest = Manifest();
        var module = new ReadyModule("ledger", manifest, "ledger.exe");

        var off = new D47Settings();
        var on = off with { Modules = new Dictionary<string, ModuleSettings> { ["ledger"] = new() { Enabled = true } } };

        Assert.Equal("Nothing while it is off.", ModulePlace.Leaves(off, module));
        Assert.Equal(
            "Bounty ledger runs as its own program on this PC. D47 does not see what it sends or where.",
            ModulePlace.Leaves(on, module));
    }

    [Fact]
    public void SettingAModuleRaisesChangedAndKeepsTheOtherModules()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var heard = new List<SettingsChanged>();
        settings.Changed += heard.Add;

        ModulePlace.Set(settings, "ledger", "module ledger turned on", m => m with { Enabled = true });
        ModulePlace.Set(settings, "other", "module other muted", m => m with { Muted = true });

        Assert.Equal(2, heard.Count);
        Assert.True(settings.Current.ModuleFor("ledger").Enabled);
        Assert.True(settings.Current.ModuleFor("other").Muted);
    }
}
