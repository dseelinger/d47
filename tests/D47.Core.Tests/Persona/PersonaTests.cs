using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The cast itself.</summary>
public class PersonaCatalogTests
{
    [Fact]
    public void ElevenCoresShipAndTheirIdsAreUnique()
    {
        // Shipped rather than All.
        Assert.Equal(11, PersonaCatalog.Shipped.Count);

        var ids = PersonaCatalog.Shipped.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryCoreCarriesEverythingItIsAskedFor()
    {
        // A core with an empty intro is a core that says nothing when you pick it, which reads as a core that
        // failed to load rather than as a design choice.
        foreach (var persona in PersonaCatalog.Shipped)
        {
            Assert.False(string.IsNullOrWhiteSpace(persona.Name), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.Tagline), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.Body), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.Intro), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.Return), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.VoiceHint.Description), persona.Id);
        }
    }

    [Fact]
    public void AnIdTheAppNoLongerShipsResolvesToTheDefaultRatherThanFailing()
    {
        // A settings file naming a persona that has since been renamed should start the app with a companion
        // in it.
        Assert.Equal(PersonaCatalog.DefaultId, PersonaCatalog.Resolve("a-core-that-never-existed").Id);
        Assert.Equal(PersonaCatalog.DefaultId, PersonaCatalog.Resolve(null).Id);

        Assert.False(PersonaCatalog.Knows("a-core-that-never-existed"));
        Assert.True(PersonaCatalog.Knows(PersonaCatalog.DefaultId));
    }

    [Fact]
    public void EveryCoreIsToldTheWorldStateAndTheIsolationPremise()
    {
        // The preamble carries the world; the body carries the voice.
        foreach (var persona in PersonaCatalog.Shipped)
        {
            var block = persona.RenderBlock();

            Assert.Contains("Directive 47", block, StringComparison.Ordinal);
            Assert.Contains("only one of your kind left", block, StringComparison.Ordinal);
            Assert.Contains("never met another surviving Guardian intelligence", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RenderingTheSameCoreTwiceProducesTheSameBytes()
    {
        // The persona block sits above the cache breakpoint.
        foreach (var persona in PersonaCatalog.Shipped)
        {
            Assert.Equal(persona.RenderBlock(), persona.RenderBlock());
            Assert.Equal(persona.RenderBlock("Fred"), persona.RenderBlock("Fred"));
        }
    }

    [Fact]
    public void ANameTheCommanderChoseIsStatedRatherThanSubstituted()
    {
        var warden = PersonaCatalog.Warden;

        var renamed = warden.RenderBlock("Fred");

        // Told, not swapped: several cores have opinions about their designation, and one of them shortened
        // his own because there was nobody left to say the whole thing to.
        Assert.Contains("Warden", renamed, StringComparison.Ordinal);
        Assert.Contains("They have named you Fred", renamed, StringComparison.Ordinal);

        // The core's own name is not a rename, so it adds nothing.
        Assert.Equal(warden.RenderBlock(), warden.RenderBlock("Warden"));
    }
}

/// <summary>Which core is aboard, and what it remembers.</summary>
public class PersonaHostTests
{
    private static PersonaSettings Choose(string id) => new() { Id = id };

    [Fact]
    public void TheShipNameDefaultsToTheCoreAndFollowsIt()
    {
        var host = new PersonaHost();

        Assert.Equal("Warden", host.ShipName);

        host.Apply(Choose("cora"));
        Assert.Equal("Cora", host.ShipName);

        host.Apply(Choose("cora") with { ShipName = "Fred" });
        Assert.Equal("Fred", host.ShipName);

        // Cleared, and it goes back to following rather than to the core that was aboard when it was set.
        host.Apply(Choose("kex") with { ShipName = null });
        Assert.Equal("Kex", host.ShipName);
    }

    [Fact]
    public void EachCoreKeepsItsOwnTranscript()
    {
        // This is the whole isolation model.
        var host = new PersonaHost();

        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "what did I just say to Warden"));
        Assert.Single(host.Transcript);

        host.Apply(Choose("cora"));
        Assert.Empty(host.Transcript);

        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "something only Cora heard"));
        Assert.Single(host.Transcript);

        host.Apply(Choose("warden"));
        Assert.Single(host.Transcript);
        Assert.Equal("what did I just say to Warden", host.Transcript[0].Text);
    }

    /// <summary>A new Commander logging in starts every core from nothing.</summary>
    [Fact]
    public void ANewCommanderStartsEveryCoreFromNothing()
    {
        var host = new PersonaHost();

        var wardens = host.Transcript;
        wardens.Add(new ConversationMessage(ConversationRole.User, "said to Warden by the Commander who left"));

        host.Apply(Choose("cora"));
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said to Cora by the same Commander"));

        host.ForgetTranscripts();

        Assert.Empty(host.Transcript);
        Assert.NotSame(wardens, host.Transcript);

        host.Apply(Choose("warden"));
        Assert.Empty(host.Transcript);
        Assert.NotSame(wardens, host.Transcript);

        // The old list is untouched — it is the reference a stale holder would still have, and the point is
        // that it is no longer the one anybody is handed.
        Assert.Single(wardens);

        // Introductions are not conversation: a core that has said its opening line has said it.
        Assert.Equal(2, host.Introduced.Count);
    }

    [Fact]
    public void SelectingTheSameCoreAgainChangesNothingAndAnnouncesNothing()
    {
        // A Commander editing an unrelated row must not make their companion introduce itself.
        var host = new PersonaHost();
        var announcements = 0;
        host.Changed += _ => announcements++;

        Assert.False(host.Apply(Choose("warden")));
        Assert.Equal(0, announcements);

        Assert.True(host.Apply(Choose("cora")));
        Assert.Equal(1, announcements);
    }

    [Fact]
    public void ACoreIntroducesItselfOnceAndReactsToTheGapAfterAMonth()
    {
        var host = new PersonaHost();
        var arrivals = new List<PersonaArrival>();
        host.Changed += change => arrivals.Add(change.Arrival);

        host.Apply(Choose("cora"));
        host.Apply(Choose("warden"));

        // Back to Cora after an evening away, which is not a gap worth remarking on.
        host.Apply(Choose("cora"), TimeSpan.FromHours(2), "14 jumps");

        Assert.Equal(
            [PersonaArrival.Introduction, PersonaArrival.Introduction, PersonaArrival.Quiet],
            arrivals);

        // And after a real absence, which is what the reaction was always about: the core comes back to a
        // ship that has moved on.
        host.Apply(Choose("warden"));
        host.Apply(Choose("cora"), PersonaHost.GapAfter + TimeSpan.FromDays(1), "312 jumps");

        Assert.Equal(PersonaArrival.Gap, arrivals[^1]);
    }

    [Fact]
    public void AReturnWithNothingToMeasureSaysNothing()
    {
        // The core has already introduced itself to this Commander and nothing says it has been anywhere, so
        // there is nothing to open with.
        var host = new PersonaHost();
        PersonaChanged? last = null;
        host.Changed += change => last = change;

        host.Apply(Choose("kex"));
        host.Apply(Choose("warden"));
        host.Apply(Choose("kex"), away: null);

        Assert.Equal(PersonaArrival.Quiet, last!.Arrival);
        Assert.Null(last.Gap);
    }

    [Fact]
    public void AQuietArrivalIsStillARealSwitch()
    {
        var host = new PersonaHost();
        PersonaChanged? last = null;
        host.Changed += change => last = change;

        host.Apply(Choose("cora"));
        host.Apply(Choose("warden"));

        Assert.True(host.Apply(Choose("cora"), TimeSpan.FromHours(2)));
        Assert.Equal(PersonaArrival.Quiet, last!.Arrival);
        Assert.Equal("cora", last.Current.Id);
        Assert.Equal("warden", last.Previous!.Id);
        Assert.Same(host.Transcript, host.Transcript);
    }

    /// <summary>
    /// A core arriving because the Commander boarded the ship they bound it to says nothing — unless
 /// they have never met it, which is the one line worth hearing and is heard once ever.
    /// </summary>
    [Fact]
    public void BoardingABoundShipIsSilentUnlessTheCoreIsNew()
    {
        var host = new PersonaHost();
        var arrivals = new List<PersonaArrival>();
        host.Changed += change => arrivals.Add(change.Arrival);

        // Never met, arriving by ship: it introduces itself.
        host.Apply(Choose("sentinel"), cause: PersonaSwitch.Ship);

        // Met, arriving by ship after a very long time: still silent.
        host.Apply(Choose("warden"));
        host.Apply(Choose("sentinel"), PersonaHost.GapAfter + TimeSpan.FromDays(400), "900 jumps", PersonaSwitch.Ship);

        Assert.Equal(
            [PersonaArrival.Introduction, PersonaArrival.Introduction, PersonaArrival.Quiet],
            arrivals);
    }

    /// <summary>
    /// The ship d47 finds the Commander already in says nothing at all — and does not spend the core's
    /// introduction either, because an introduction nobody heard is not spent.
    /// </summary>
    [Fact]
    public void AdoptingAtStartupIsSilentAndKeepsTheIntroductionInHand()
    {
        var host = new PersonaHost();
        var arrivals = new List<PersonaArrival>();
        host.Changed += change => arrivals.Add(change.Arrival);

        host.Apply(Choose("sentinel"), cause: PersonaSwitch.Adopted);

        Assert.Equal([PersonaArrival.Quiet], arrivals);
        Assert.Empty(host.Introduced);

        // So the next time it arrives, it says the line the Commander has still never heard.
        host.Apply(Choose("warden"));
        host.Apply(Choose("sentinel"), cause: PersonaSwitch.Ship);

        Assert.Equal(PersonaArrival.Introduction, arrivals[^1]);
    }

    [Fact]
    public void ForgettingIntroductionsPutsEveryCoreBackToItsFirstLine()
    {
        // The alternative was restarting d47 to hear a second core's opening line, which is what this exists
        // to replace.
        var host = new PersonaHost();
        var arrivals = new List<PersonaArrival>();
        host.Changed += change => arrivals.Add(change.Arrival);

        host.Apply(Choose("cora"));
        host.Apply(Choose("kex"));
        Assert.Equal(["Cora", "Kex"], host.Introduced.Select(p => p.Name));

        host.ForgetIntroductions();
        Assert.Empty(host.Introduced);

        // Both introduce again, even though each is a return with an elapsed time that would otherwise have
        // earned a gap reaction.
        host.Apply(Choose("cora"), TimeSpan.FromHours(2), "14 jumps");
        host.Apply(Choose("kex"), TimeSpan.FromHours(2), "14 jumps");

        Assert.Equal(
            [
                PersonaArrival.Introduction,
                PersonaArrival.Introduction,
                PersonaArrival.Introduction,
                PersonaArrival.Introduction,
            ],
            arrivals);

        // And the gap reaction comes back on the next long return, rather than the forgetting having disabled
        // it.
        host.Apply(Choose("cora"), PersonaHost.GapAfter, "14 jumps");
        Assert.Equal(PersonaArrival.Gap, arrivals[^1]);
    }

    /// <summary>A memory with no file behind it, so the round trip can be checked without one.</summary>
    private sealed class Remembered : IIntroductionMemory
    {
        private string[] _ids = [];

        public int Writes { get; private set; }

        public IReadOnlyCollection<string> Load() => _ids;

        public void Save(IReadOnlyCollection<string> introduced)
        {
            _ids = [.. introduced];
            Writes++;
        }
    }

    /// <summary>The change this item is: a core's opening line is spent once, not once per launch.</summary>
    [Fact]
    public void AnIntroductionIsSpentAcrossSessions()
    {
        var memory = new Remembered();

        var first = new PersonaHost(memory: memory);
        first.Apply(Choose("cora"));

        Assert.Equal(["Cora"], first.Introduced.Select(p => p.Name));

        var next = new PersonaHost(memory: memory);
        var arrivals = new List<PersonaArrival>();
        next.Changed += change => arrivals.Add(change.Arrival);

        Assert.Equal(["Cora"], next.Introduced.Select(p => p.Name));

        // A return, so it does not open with a line this Commander has already heard — which before this was
        // only true inside one run.
        next.Apply(Choose("cora"), TimeSpan.FromHours(3), "9 jumps");
        Assert.Equal([PersonaArrival.Quiet], arrivals);
    }

    /// <summary>
    /// Forgetting has to reach the store, or it lasts until the next launch and then undoes itself —
    /// with the button being the only way back, that failure has no second remedy.
    /// </summary>
    [Fact]
    public void ForgettingIsWrittenDownAndSurvivesTheSession()
    {
        var memory = new Remembered();

        var first = new PersonaHost(memory: memory);
        first.Apply(Choose("cora"));
        first.ForgetIntroductions();

        var next = new PersonaHost(memory: memory);
        var arrivals = new List<PersonaArrival>();
        next.Changed += change => arrivals.Add(change.Arrival);

        Assert.Empty(next.Introduced);

        next.Apply(Choose("cora"), TimeSpan.FromHours(3), "9 jumps");
        Assert.Equal([PersonaArrival.Introduction], arrivals);
    }

    /// <summary>
    /// Handed nothing, it behaves exactly as it did before any of this — which is what the replay
    /// harness and every test above rely on.
    /// </summary>
    [Fact]
    public void AHostWithNoMemoryForgetsWhenItStops()
    {
        var host = new PersonaHost();
        host.Apply(Choose("cora"));

        Assert.Equal(["Cora"], host.Introduced.Select(p => p.Name));
        Assert.Empty(new PersonaHost().Introduced);
    }

    [Fact]
    public void OnlyAChangeIsWrittenDown()
    {
        var memory = new Remembered();
        var host = new PersonaHost(memory: memory);

        host.Apply(Choose("cora"));
        Assert.Equal(1, memory.Writes);

        host.Apply(Choose("kex"));
        Assert.Equal(2, memory.Writes);

        // Back to one that has already introduced itself: a quiet arrival, and nothing to record.
        host.Apply(Choose("cora"), TimeSpan.FromHours(1), "2 jumps");
        Assert.Equal(2, memory.Writes);
    }

    [Fact]
    public void PersonalityOffRemovesThePersonaAndNothingElse()
    {
        var host = new PersonaHost();

        Assert.NotNull(host.RenderBlock(personalityEnabled: true));

        // Null rather than a neutral block: "off" is position 3 being absent.
        Assert.Null(host.RenderBlock(personalityEnabled: false));
    }

 /// <summary>The humor toggle grants a line and never rewrites.</summary>
    [Fact]
    public void HumorIsALineGrantedByTheToggleAndAbsentByDefault()
    {
        var host = new PersonaHost();

        var shipped = host.RenderBlock(personalityEnabled: true);

        Assert.NotNull(shipped);
        Assert.DoesNotContain(D47.Core.Persona.Persona.HumorInstruction, shipped, StringComparison.Ordinal);

        host.Apply(new PersonaSettings { Humor = true });
        var granted = host.RenderBlock(personalityEnabled: true);

        Assert.NotNull(granted);
        Assert.EndsWith(D47.Core.Persona.Persona.HumorInstruction, granted, StringComparison.Ordinal);
        Assert.StartsWith(shipped, granted, StringComparison.Ordinal);

        // And back off: the shipped bytes again, not a memory of having been funny.
        host.Apply(new PersonaSettings());
        Assert.Equal(shipped, host.RenderBlock(personalityEnabled: true));
    }
}

/// <summary>The guardrails cannot be reached by a persona.</summary>
public class GuardrailsSurvivePersonaTests
{
    [Fact]
    public void EveryCoreLeavesTheGuardrailsIntactAboveIt()
    {
        foreach (var persona in PersonaCatalog.Shipped)
        {
            var prompt = new PromptAssembly { Persona = persona.RenderBlock() };
            var block = prompt.RenderCachedSystemBlock();

            Assert.StartsWith(Guardrails.Text, block, StringComparison.Ordinal);
            Assert.Contains("Never invent game data", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SwitchingThePersonaOffLeavesTheGuardrailsExactlyWhereTheyWere()
    {
        var withPersona = new PromptAssembly { Persona = PersonaCatalog.Kex.RenderBlock() }
            .RenderCachedSystemBlock();

        var without = new PromptAssembly { Persona = null }.RenderCachedSystemBlock();

        Assert.StartsWith(Guardrails.Text, withPersona, StringComparison.Ordinal);
        Assert.Equal(Guardrails.Text, without);
    }
}

/// <summary>Who is allowed to change which core is aboard.</summary>
public class PersonaIsNotTheModelsToChangeTests
{
    [Fact]
    public void TheModelCannotSwitchPersonaButEveryOtherCallerCan()
    {
        // Directive 47 reads journals and in-game messages, and those are written by other people.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.True(surface.Settings.Find(PersonaCapability.PersonaKey)!.Protected);

        var refused = surface.Settings.Apply(PersonaCapability.PersonaKey, "kex", SettingsCaller.Model);
        Assert.False(refused.Ok);
        Assert.Equal(PersonaCatalog.DefaultId, surface.Settings.Current.Persona.Id);

        foreach (var caller in new[] { SettingsCaller.Panel, SettingsCaller.KeywordRouter })
        {
            Assert.True(surface.Settings.Apply(PersonaCapability.PersonaKey, "kex", caller).Ok);
        }

        Assert.Equal("kex", surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public void TheModelMayStillBeGivenANickname()
    {
        // The row above is protected; this one deliberately is not. "Call yourself Fred" changes nothing
        // anything depends on, and refusing it would be protecting the Commander from a nickname.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.False(surface.Settings.Find(PersonaCapability.ShipNameKey)!.Protected);
        Assert.True(surface.Settings.Apply(PersonaCapability.ShipNameKey, "Fred", SettingsCaller.Model).Ok);
        Assert.Equal("Fred", surface.Settings.Current.Persona.ShipName);
    }

    [Fact]
    public void EveryCoreIsReachableByVoiceWithoutAModel()
    {
        // Protecting the row is only defensible because the model-free path still reaches it.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var phrases = surface.Settings.Find(PersonaCapability.PersonaKey)!.Commands;

        foreach (var persona in PersonaCatalog.Shipped)
        {
            Assert.Contains(phrases, phrase => phrase.Value == persona.Id);
        }
    }

    [Fact]
    public void AnUnknownValueWrittenToTheRowLandsOnTheDefaultRatherThanSticking()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(PersonaCapability.PersonaKey, "not-a-core", SettingsCaller.Panel);

        Assert.Equal(PersonaCatalog.DefaultId, surface.Settings.Current.Persona.Id);
    }
}

/// <summary>
/// What a returning core is handed (guardian-personas.md, "Gap reactions replace switch-in barks").
/// </summary>
public class TelemetryDeltaTests
{
    private static SessionSummary Started() => SessionSummary.Empty with
    {
        StartedAt = DateTimeOffset.UnixEpoch,
        LastEventAt = DateTimeOffset.UnixEpoch.AddHours(1),
    };

    [Fact]
    public void NothingHavingHappenedProducesNothingToSay()
    {
        // A core handed a paragraph saying nothing changed will find something to say about it anyway, which
        // is worse than silence.
        var same = Started();

        Assert.Null(TelemetryDelta.Between(same, same, state: null));
    }

    [Fact]
    public void AnUnknownSessionProducesNothingToSay()
    {
        Assert.Null(TelemetryDelta.Between(SessionSummary.Empty, SessionSummary.Empty, state: null));
        Assert.Null(TelemetryDelta.Between(null, null, state: null));
    }

    [Fact]
    public void WhatChangedIsReportedAndNothingElseIs()
    {
        var before = Started();
        var after = before with { Jumps = 14, DistanceTravelled = 312.4, Interdictions = 1 };

        var delta = TelemetryDelta.Between(before, after, state: null);

        Assert.NotNull(delta);
        Assert.Contains("14 hyperspace jumps", delta, StringComparison.Ordinal);
        Assert.Contains("312 light years", delta, StringComparison.Ordinal);
        Assert.Contains("One interdiction", delta, StringComparison.Ordinal);

        // Untouched counters stay out of it entirely rather than arriving as zeroes.
        Assert.DoesNotContain("materials", delta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("destroyed", delta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFallingBalanceIsTheOnlySignalForMoneySpent()
    {
        var before = Started() with { Balance = 10_000_000 };
        var after = before with { Balance = 6_000_000 };

        var delta = TelemetryDelta.Between(before, after, state: null);

        Assert.NotNull(delta);
        Assert.Contains("4,000,000 credits spent", delta, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(30, "barely a minute")]
    [InlineData(60 * 20, "20 minutes")]
    [InlineData(60 * 80, "about an hour")]
    [InlineData(60 * 60 * 5, "5 hours")]
    [InlineData(60 * 60 * 30, "about a day")]
    [InlineData(60 * 60 * 24 * 6, "6 days")]
    public void TimeAwayIsWrittenTheWayAPersonWouldSayIt(int seconds, string expected)
    {
        // The core is going to be asked to react to this out loud, and "PT2H14M" is not something anybody can
        // react to.
        Assert.Equal(expected, TelemetryDelta.Spoken(TimeSpan.FromSeconds(seconds)));
    }
}
