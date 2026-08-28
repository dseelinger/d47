using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Who is speaking, and who keeps their voice (Phase 11, "Voices stick").
/// </summary>
public class VoiceCastTests
{
    private static VoiceCast Cast() => new()
    {
        Pool = ["voice-a", "voice-b", "voice-c", "voice-d"],
        DefaultVoice = "ship-ai",
    };

    [Fact]
    public void ARoleWithNoVoiceOfItsOwnUsesTheShipAis()
    {
        var cast = Cast();

        Assert.Equal("ship-ai", cast.For(VoiceRole.CarrierCaptain).VoiceId);

        cast.Assign(VoiceRole.CarrierCaptain, "voice-b");
        Assert.Equal("voice-b", cast.For(VoiceRole.CarrierCaptain).VoiceId);

        // Cleared goes back to following rather than sticking on the last value.
        cast.Assign(VoiceRole.CarrierCaptain, null);
        Assert.Equal("ship-ai", cast.For(VoiceRole.CarrierCaptain).VoiceId);
    }

    [Fact]
    public void ASenderKeepsTheVoiceTheyWereFirstGiven()
    {
        var cast = Cast();

        var first = cast.ForSender("Commander Vex", isPlayer: true);
        var again = cast.ForSender("Commander Vex", isPlayer: true);

        Assert.Equal(first.VoiceId, again.VoiceId);
    }

    [Fact]
    public void DifferentSendersGetDifferentVoicesWhileThePoolLasts()
    {
        var cast = Cast();

        var assigned = new[] { "Vex", "Ilse", "Toma", "Bru" }
            .Select(name => cast.ForSender(name, isPlayer: true).VoiceId)
            .ToArray();

        Assert.Equal(4, assigned.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void APoolThatRunsOutSharesRatherThanFallingSilentOrReshuffling()
    {
        var cast = new VoiceCast { Pool = ["only-one"], DefaultVoice = "ship-ai" };

        var vex = cast.ForSender("Vex", isPlayer: true).VoiceId;
        var ilse = cast.ForSender("Ilse", isPlayer: true).VoiceId;

        Assert.Equal("only-one", vex);
        Assert.Equal("only-one", ilse);

        // And they still keep it. Sharing is tolerable; changing every time they speak is not.
        Assert.Equal(ilse, cast.ForSender("Ilse", isPlayer: true).VoiceId);
    }

    [Fact]
    public void AnEmptyPoolFallsBackToTheRoleRatherThanFailing()
    {
        var cast = new VoiceCast { DefaultVoice = "ship-ai" };

        Assert.Equal("ship-ai", cast.ForSender("Vex", isPlayer: true).VoiceId);
    }

    [Fact]
    public void NpcsTurnOverOnAJumpAndPlayersDoNot()
    {
        // The asymmetry is the item. The cast of NPCs changes when the system does; a wingmate
        // whose voice changed on every jump would read as a bug rather than as variety.
        var cast = Cast();

        var wingmate = cast.ForSender("Commander Vex", isPlayer: true).VoiceId;
        cast.ForSender("Pirate Lord", isPlayer: false);

        Assert.Equal((1, 1), cast.Assignments);

        cast.EnteredSystem();

        Assert.Equal((1, 0), cast.Assignments);
        Assert.Equal(wingmate, cast.ForSender("Commander Vex", isPlayer: true).VoiceId);
    }

    /// <summary>
    /// An NPC keeps their voice for as long as the Commander is in the system, which is the whole
    /// of what "remember which voice was used" asks for — and drops it on the jump out, which is
    /// the half the request explicitly does not need.
    /// </summary>
    [Fact]
    public void AnNpcKeepsOneVoiceForAsLongAsTheCommanderIsThere()
    {
        var cast = Cast();

        var first = cast.ForSender("Ilse Bruhn", isPlayer: false).VoiceId;

        Assert.Equal(first, cast.ForSender("Ilse Bruhn", isPlayer: false).VoiceId);
        Assert.Equal(first, cast.ForSender("Ilse Bruhn", isPlayer: false).VoiceId);

        // Somebody else in the same system is somebody else.
        Assert.NotEqual(first, cast.ForSender("ShipName Police Federation", isPlayer: false).VoiceId);
    }

    /// <summary>
    /// The crew are aboard, so their voices last the session (docs/plans/change-requests.md
    /// item 14).
    /// <para>
    /// They used to share the per-system table with the NPC comms traffic, which meant the gunner
    /// the Commander hired at a station changed voice on every jump — and could be handed the same
    /// voice as a pirate two systems later. Found while giving the crew the other half of being
    /// aboard, which is that they are not put through a radio.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCrewKeepTheirVoicesAcrossAJump()
    {
        var cast = Cast();

        var gunner = cast.ForSender("Ilse Bruhn", isPlayer: false, VoiceRole.Crew).VoiceId;
        cast.ForSender("Pirate Lord", isPlayer: false);

        cast.EnteredSystem();

        Assert.Equal(gunner, cast.ForSender("Ilse Bruhn", isPlayer: false, VoiceRole.Crew).VoiceId);

        // One assignment left, and it is the gunner's: the pirate was the only one in the table
        // the jump cleared.
        Assert.Equal((1, 0), cast.Assignments);
    }

    /// <summary>
    /// No stranger is handed a voice that already belongs to somebody in the ship.
    /// <para>
    /// The smaller half of "a voice appropriate for the NPC". Nothing used to stop the pool
    /// handing out the ship AI's own voice, and hearing d47's voice arrive from a pirate — through
    /// a radio — is worse than either of those on its own.
    /// </para>
    /// </summary>
    [Fact]
    public void NobodyOutsideTheShipIsGivenAVoiceFromInsideIt()
    {
        var cast = new VoiceCast
        {
            Pool = ["voice-a", "voice-b", "voice-c"],
            DefaultVoice = "voice-b",
        };

        cast.Assign(VoiceRole.TowerControl, "voice-c");

        // Which leaves exactly one voice that is nobody's, and every sender gets it rather than
        // one of the two that are spoken for.
        foreach (var name in new[] { "Vex", "Ilse", "Pirate Lord" })
        {
            Assert.Equal("voice-a", cast.ForSender(name, isPlayer: false).VoiceId);
        }
    }

    /// <summary>
    /// And where every voice in the pool is somebody aboard, the role is the answer rather than
    /// borrowing one. There is no voice left that would mean anything.
    /// </summary>
    [Fact]
    public void APoolThatIsEntirelyTheShipsOwnFallsBackToTheRole()
    {
        var cast = new VoiceCast { Pool = ["ship-ai"], DefaultVoice = "ship-ai" };

        Assert.Equal("ship-ai", cast.ForSender("Pirate Lord", isPlayer: false).VoiceId);
        Assert.Equal((0, 0), cast.Assignments);
    }

    [Fact]
    public void TheSameNameGetsTheSameVoiceInEveryRun()
    {
        // Seeded from the name rather than drawn at random, so a recorded session replays
        // identically. String.GetHashCode is randomised per process and would not do.
        var first = Cast().ForSender("Commander Vex", isPlayer: true).VoiceId;
        var second = Cast().ForSender("Commander Vex", isPlayer: true).VoiceId;

        Assert.Equal(first, second);
    }

    [Fact]
    public void AProviderChangeDropsEveryAssignment()
    {
        // A voice id belongs to the provider that issued it. Keeping the table across a switch
        // would keep ids that no longer resolve.
        var cast = Cast();

        cast.ForSender("Vex", isPlayer: true);
        cast.ForSender("Pirate Lord", isPlayer: false);
        cast.Assign(VoiceRole.TowerControl, "voice-c");

        cast.Reset();

        Assert.Equal((0, 0), cast.Assignments);
        Assert.Equal("ship-ai", cast.For(VoiceRole.TowerControl).VoiceId);
    }

    [Fact]
    public void TheRateAppliesToEveryoneSpeaking()
    {
        // How fast the Commander likes to be spoken to is a property of the Commander, not of
        // who happens to be talking.
        var cast = Cast();
        cast.Rate = 1.15;

        Assert.Equal(1.15, cast.For(VoiceRole.ShipAi).Rate);
        Assert.Equal(1.15, cast.For(VoiceRole.Comms).Rate);
        Assert.Equal(1.15, cast.ForSender("Vex", isPlayer: true).Rate);
    }
}

/// <summary>
/// Two providers, one settings surface (Phase 11, "ElevenLabs").
/// </summary>
public class TtsProviderCatalogTests
{
    [Fact]
    public void EveryProviderDisclosesWhatItReceives()
    {
        // The set is exhaustive by construction, and a provider with no disclosure is a
        // provider whose egress row would render blank.
        foreach (var provider in TtsProviderCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(provider.Egress), provider.Id);
            Assert.False(string.IsNullOrWhiteSpace(provider.Destination), provider.Id);
            Assert.False(string.IsNullOrWhiteSpace(provider.Label), provider.Id);
        }
    }

    [Fact]
    public void AProviderTheAppNoLongerShipsFallsBackToEdge()
    {
        Assert.Equal(TtsProviderCatalog.EdgeId, TtsProviderCatalog.Selected("a-provider-that-never-existed").Id);
        Assert.Equal(TtsProviderCatalog.EdgeId, TtsProviderCatalog.Selected(null).Id);
    }

    [Fact]
    public void OnlyTheSelectedProvidersKeyRowIsOnScreen()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var key = SpeechCapability.KeyRowFor(TtsProviderCatalog.ElevenLabs);
        var row = surface.Settings.Find(key);

        Assert.NotNull(row);
        Assert.Equal(SettingKind.Secret, row.Kind);

        Assert.False(row.Applies(new D47Settings()));
        Assert.True(row.Applies(new D47Settings
        {
            Speech = new SpeechSettings { Provider = TtsProviderCatalog.ElevenLabsId },
        }));
    }
}

/// <summary>
/// Speaking rate, remembered against the provider it was chosen for (Phase 11:
/// "Differences between providers, such as speed, is maintained on a per-provider basis").
/// </summary>
public class PerProviderRateTests
{
    private static D47Settings With(string provider, params (string Provider, double Rate)[] rates) => new()
    {
        Speech = new SpeechSettings
        {
            Provider = provider,
            ProviderRates = rates.ToDictionary(r => r.Provider, r => r.Rate, StringComparer.OrdinalIgnoreCase),
        },
    };

    [Fact]
    public void AProviderWithNoRateOfItsOwnUsesTheGeneralOne()
    {
        var settings = new D47Settings { Speech = new SpeechSettings { Rate = 1.15 } };

        Assert.Equal(1.15, SpeechCapability.RateFor(settings));
    }

    [Fact]
    public void EachProviderRemembersItsOwn()
    {
        // Normalising the units gets them agreeing about what 1.0 means. It does not make 1.15
        // sound the same on two synthesisers, and it cannot.
        var rates = new (string, double)[] { (TtsProviderCatalog.EdgeId, 1.2), (TtsProviderCatalog.ElevenLabsId, 0.9) };

        Assert.Equal(1.2, SpeechCapability.RateFor(With(TtsProviderCatalog.EdgeId, rates)));
        Assert.Equal(0.9, SpeechCapability.RateFor(With(TtsProviderCatalog.ElevenLabsId, rates)));
    }

    [Fact]
    public void ARateFromAWiderProviderIsClampedRatherThanRejected()
    {
        // ElevenLabs refuses a speed outside its range outright, and a rejected request arrives
        // as silence. Their fastest is a better answer than nothing.
        var settings = With(TtsProviderCatalog.ElevenLabsId, (TtsProviderCatalog.ElevenLabsId, 1.6));

        Assert.Equal(TtsProviderCatalog.ElevenLabs.MaximumRate, SpeechCapability.RateFor(settings));
    }

    [Fact]
    public void WritingTheRowStoresItAgainstTheSelectedProviderOnly()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);
        surface.Settings.Apply(SpeechCapability.RateKey, "0.8", SettingsCaller.Panel);

        Assert.Equal(0.8, surface.Settings.Current.Speech.ProviderRates[TtsProviderCatalog.ElevenLabsId]);

        // The general value is untouched, so clearing an override falls back to something
        // sensible rather than to the last provider's number.
        Assert.Equal(1.0, surface.Settings.Current.Speech.Rate);

        surface.Settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.EdgeId, SettingsCaller.Panel);
        Assert.Equal(1.0, SpeechCapability.RateFor(surface.Settings.Current));
    }
}

/// <summary>
/// The disclosure gap Phase 11 closed.
/// </summary>
public class TextToSpeechEgressTests
{
    [Fact]
    public void TheDefaultConfigurationAdmitsThatSpeechLeavesTheMachine()
    {
        // Edge Neural is free, not local. Until Phase 11 there was no text-to-speech entry in
        // the disclosure at all, so d47 could render "nothing is leaving this machine" while
        // sending every spoken word to Microsoft.
        var entry = EgressDisclosure.Entry(EgressDisclosure.TextToSpeech, new D47Settings(), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Contains("speech.platform.bing.com", entry.Destination, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEntrySpeaksForWhicheverProviderIsSelected()
    {
        var eleven = EgressDisclosure.Entry(
            EgressDisclosure.TextToSpeech,
            new D47Settings { Speech = new SpeechSettings { Provider = TtsProviderCatalog.ElevenLabsId } },
            llmKeyPresent: false);

        Assert.True(eleven.Active);
        Assert.Contains("api.elevenlabs.io", eleven.Destination, StringComparison.Ordinal);

        // And it says the part a Commander would not otherwise know: turning on re-voiced
        // messages sends text other players wrote.
        Assert.Contains("written by other players", eleven.What, StringComparison.Ordinal);
    }

    [Fact]
    public void NoVoiceProviderIsTheOnlySilentConfiguration()
    {
        var silent = EgressDisclosure.Entry(
            EgressDisclosure.TextToSpeech,
            new D47Settings { Speech = new SpeechSettings { Provider = TtsProviderCatalog.NoneId } },
            llmKeyPresent: false);

        Assert.False(silent.Active);
    }
}

/// <summary>
/// A woman on the radio sounds like one (remediation.md, "Named NPCs should each use a different
/// voice"). The name is all there is to go on: Elite records no sex for anybody.
/// </summary>
public class VoicesMatchTheNameTests
{
    private static VoiceCast Cast() => new()
    {
        DefaultVoice = "ship-ai",
        Pool = ["m1", "f1", "m2", "f2", "m3", "f3"],
        Feminine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "f1", "f2", "f3" },
    };

    private static bool IsFemale(string? voiceId) => voiceId is not null && voiceId.StartsWith('f');

    [Fact]
    public void AWomansNameGetsAWomansVoice()
    {
        var cast = Cast();

        foreach (var name in new[] { "Marianne Hobbs", "Ilse Bruhn", "Astrid Vahl" })
        {
            Assert.True(IsFemale(cast.ForSender(name, isPlayer: false).VoiceId), name);
        }
    }

    [Fact]
    public void EverybodyElseGetsAMans()
    {
        var cast = Cast();

        foreach (var name in new[] { "Mark Bennett", "John Tennyson", "Squidbrain" })
        {
            Assert.False(IsFemale(cast.ForSender(name, isPlayer: false).VoiceId), name);
        }
    }

    /// <summary>
    /// And they are still one voice each. Matching the sex narrows the pool, and narrowing it
    /// must not start handing the same voice to two people while unused ones are left.
    /// </summary>
    [Fact]
    public void TheyAreStillDistinctWithinTheSex()
    {
        var cast = Cast();

        var women = new[] { "Marianne Hobbs", "Ilse Bruhn", "Astrid Vahl" }
            .Select(name => cast.ForSender(name, isPlayer: false).VoiceId)
            .ToArray();

        Assert.Equal(3, women.Distinct(StringComparer.Ordinal).Count());
        Assert.All(women, voice => Assert.True(IsFemale(voice)));
    }

    /// <summary>
    /// A fourth woman with only three women's voices shares one rather than being given a man's.
    /// Running out is not a reason to start getting it wrong.
    /// </summary>
    [Fact]
    public void RunningOutOfWomensVoicesSharesRatherThanSwitchingSex()
    {
        var cast = Cast();

        var women = new[] { "Marianne Hobbs", "Ilse Bruhn", "Astrid Vahl", "Beatrice Kohl" }
            .Select(name => cast.ForSender(name, isPlayer: false).VoiceId)
            .ToArray();

        Assert.All(women, voice => Assert.True(IsFemale(voice)));
    }

    /// <summary>
    /// A pool nothing is tagged in still works, because most of what this has to survive is a
    /// provider that says nothing about its voices.
    /// </summary>
    [Fact]
    public void AnUntaggedPoolStillAssignsAVoiceEach()
    {
        var cast = new VoiceCast { DefaultVoice = "ship-ai", Pool = ["a", "b", "c"] };

        var assigned = new[] { "Marianne Hobbs", "Mark Bennett", "Ilse Bruhn" }
            .Select(name => cast.ForSender(name, isPlayer: false).VoiceId)
            .ToArray();

        Assert.Equal(3, assigned.Distinct(StringComparer.Ordinal).Count());
    }
}
