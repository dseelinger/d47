using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Which callouts a model is allowed to rewrite (list.md Phase 11; list.md Phase 8).
/// <para>
/// The safety property is the exclusion, not the inclusion: <b>a danger callout is never
/// rewritten</b>. Those fire on the event and say exactly what happened, and "shields are down"
/// is not a line that benefits from personality — nor one that benefits from a three-second wait
/// on a network round trip.
/// </para>
/// <para>
/// It was the default arm of a switch inside <c>AppHost.VaryAsync</c>, where nothing could assert
/// it and where adding one case above it was the entire cost of losing it (list.md Phase 19).
/// </para>
/// </summary>
public class OnlySomeLinesAreSaidInCharacterTests
{
    private static Announcement Danger() =>
        new("danger.shields", "Shields are down.", CalloutUrgency.Urgent);

    [Fact]
    public void ADangerCalloutIsSaidExactlyAsWritten()
    {
        Assert.Null(FlavourBriefs.For(Danger(), personalityEnabled: true));
    }

    /// <summary>
    /// Every routine callout that is not the carrier's and not ambient goes out as authored too.
    /// Named individually rather than as a wildcard, because each of these is a line somebody
    /// wrote on purpose.
    /// </summary>
    [Theory]
    [InlineData("fuel.low")]
    [InlineData("route.progress")]
    [InlineData("arrival.home")]
    [InlineData("materials.full")]
    [InlineData("prospector.content")]
    [InlineData("checklist.step")]
    public void TheAuthoredCalloutsAreNotRewritten(string key)
    {
        Assert.Null(FlavourBriefs.For(new Announcement(key, "As written."), personalityEnabled: true));
    }

    /// <summary>
    /// Personality off silences all of it, including the carrier — a captain improvising is
    /// personality by any reading.
    /// </summary>
    [Fact]
    public void PersonalityOffLeavesEveryLineAlone()
    {
        var ambient = new Announcement($"{AmbientCallout.KeyPrefix}Docked", "Quiet out here.");
        var carrier = new Announcement("carrier.jump", "Jump complete.") { Voice = VoiceRole.CarrierCaptain };

        Assert.Null(FlavourBriefs.For(ambient, personalityEnabled: false));
        Assert.Null(FlavourBriefs.For(carrier, personalityEnabled: false));
    }

    /// <summary>
    /// An ambient remark is the core's own, so it gets the persona block and the live game state:
    /// it is about where the Commander actually is.
    /// </summary>
    [Fact]
    public void AnAmbientRemarkIsTheCoresOwnAndKnowsWhereItIs()
    {
        var brief = FlavourBriefs.For(
            new Announcement($"{AmbientCallout.KeyPrefix}Docked", "Quiet out here."), personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);
        Assert.True(brief.NeedsGameState);
        Assert.Null(brief.Speaker);
    }

    /// <summary>
    /// The carrier's two roles are other people entirely. No persona block — handing a Guardian
    /// core the captain's lines would put one of them in two places at once, which is the one
    /// thing the isolation model cannot survive — and no game state.
    /// </summary>
    [Theory]
    [InlineData(VoiceRole.CarrierCaptain)]
    [InlineData(VoiceRole.TowerControl)]
    public void TheCarriersRolesAreNotTheShipsAi(VoiceRole role)
    {
        var brief = FlavourBriefs.For(
            new Announcement("carrier.jump", "Jump complete.") { Voice = role }, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.False(brief.NeedsPersona);
        Assert.False(brief.NeedsGameState);

        // A brief of its own instead, so the slot the persona block would occupy is not empty.
        Assert.NotNull(brief.Speaker);
        Assert.Contains("Never mention being an AI", brief.Speaker, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCaptainAndTheTowerAreDescribedAsDifferentPeople()
    {
        var captain = FlavourBriefs.For(
            new Announcement("carrier.jump", "x") { Voice = VoiceRole.CarrierCaptain }, personalityEnabled: true);

        var tower = FlavourBriefs.For(
            new Announcement("carrier.jump", "x") { Voice = VoiceRole.TowerControl }, personalityEnabled: true);

        Assert.NotEqual(captain!.Speaker, tower!.Speaker);
    }

    /// <summary>
    /// The carrier is asked to reword the authored line, so the authored text has to reach the
    /// instruction. Without it the model is improvising an announcement rather than rephrasing
    /// one, which is how a jump confirmation becomes a jump that did not happen.
    /// </summary>
    [Fact]
    public void TheCarriersBriefCarriesTheLineItIsRewording()
    {
        var brief = FlavourBriefs.For(
            new Announcement("carrier.jump", "Jump complete.") { Voice = VoiceRole.CarrierCaptain },
            personalityEnabled: true);

        Assert.Contains("Jump complete.", brief!.Instruction, StringComparison.Ordinal);
    }

    /// <summary>
    /// The situation is read back off the key, because a batch may hold more than one ambient
    /// callout and the callout itself only remembers the last.
    /// </summary>
    [Fact]
    public void TheSituationIsReadBackOffTheKey()
    {
        Assert.Equal(
            AmbientSituation.Docked, FlavourBriefs.SituationOf($"{AmbientCallout.KeyPrefix}Docked"));
    }

    /// <summary>A key naming a situation this build does not have is None rather than a throw.</summary>
    [Fact]
    public void AnUnknownSituationIsNoneRatherThanAFailure()
    {
        Assert.Equal(
            AmbientSituation.None, FlavourBriefs.SituationOf($"{AmbientCallout.KeyPrefix}Spelunking"));
    }
    /// <summary>
    /// A core's first line goes through the model too, as a rewording.
    /// <para>
    /// <c>guardian-personas.md</c> files these under <em>sample lines, not intended to be
    /// verbatim</em>. They were spoken exactly as authored on the argument that putting written
    /// material through a model returns it slightly worse — right about finished writing, wrong
    /// about a sample, and it meant every Commander who ever picked Warden heard the same
    /// paragraph word for word every time.
    /// </para>
    /// </summary>
    [Fact]
    public void ACoresFirstLineIsRewordedRatherThanRead()
    {
        var brief = FlavourBriefs.Introducing("Systems answering, Commander.", personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);

        // The authored line is handed over, because this is a rewording and not a composition:
        // a first line carries things a Commander needs, and a model asked to improvise a
        // greeting would lose them.
        Assert.Contains("Systems answering, Commander.", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("not the same sentences", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("add nothing you were not given", brief.Instruction, StringComparison.Ordinal);

        // And nothing about where the Commander is: the line is about the core.
        Assert.False(brief.NeedsGameState);
    }

    /// <summary>
    /// Personality off means said exactly as written — the same rule every announcement follows,
    /// and the reason a Commander who has turned it off hears the authored line unchanged.
    /// </summary>
    [Fact]
    public void WithPersonalityOffTheAuthoredFirstLineIsSaidAsWritten()
    {
        Assert.Null(FlavourBriefs.Introducing("Systems answering, Commander.", personalityEnabled: false));
    }

    /// <summary>A core with no authored line has nothing to reword, which is not an error.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ACoreWithNothingAuthoredIsNotAsked(string? intro)
    {
        Assert.Null(FlavourBriefs.Introducing(intro, personalityEnabled: true));
    }

    /// <summary>
    /// Every line the ship's AI says to the Commander knows who the Commander is (list.md Phase
    /// 43): the ambient remarks, the opening line and a core's first words all carry the sheet.
    /// </summary>
    [Fact]
    public void TheShipsAiKnowsWhoItIsFlyingWith()
    {
        var ambient = FlavourBriefs.For(
            new Announcement($"{AmbientCallout.KeyPrefix}Docked", "Quiet out here.") { Variant = 1 },
            personalityEnabled: true);
        var greeting = FlavourBriefs.For(
            new Announcement(ContinuityCallout.Key, "Good evening, Commander. Ready to go."),
            personalityEnabled: true);
        var intro = FlavourBriefs.Introducing("Systems answering, Commander.", personalityEnabled: true);

        Assert.True(ambient!.NeedsAboutMe);
        Assert.True(greeting!.NeedsAboutMe);
        Assert.True(intro!.NeedsAboutMe);
    }

    /// <summary>
    /// The carrier's tower does not, for the reason it gets no persona: a stranger on a comms
    /// channel does not know the Commander's history.
    /// </summary>
    [Theory]
    [InlineData(VoiceRole.CarrierCaptain)]
    [InlineData(VoiceRole.TowerControl)]
    public void AStrangerDoesNotKnowTheCommandersHistory(VoiceRole role)
    {
        var brief = FlavourBriefs.For(
            new Announcement("carrier.jump", "Jump complete.") { Voice = role }, personalityEnabled: true);

        Assert.False(brief!.NeedsAboutMe);
        Assert.False(brief.NeedsStory);
    }

    /// <summary>
    /// The two flags correlate and are not the same flag. They answer different questions —
    /// whose voice this is, and who it is speaking to — and a brief could one day want one
    /// without the other, which a merged flag could not express.
    /// </summary>
    [Fact]
    public void WhoseVoiceAndWhoItKnowsAreSeparateFlags()
    {
        var persona = typeof(FlavourBrief).GetProperty(nameof(FlavourBrief.NeedsPersona));
        var aboutMe = typeof(FlavourBrief).GetProperty(nameof(FlavourBrief.NeedsAboutMe));

        Assert.NotNull(persona);
        Assert.NotNull(aboutMe);
        Assert.NotEqual(persona, aboutMe);
    }

    /// <summary>
    /// The sheet always, the story sometimes — and which remark carries the story is read off the
    /// announcement's index, so it is decided by the same count that picked the stock line and
    /// never by a clock.
    /// </summary>
    [Fact]
    public void TheStoryRidesOnOneAmbientRemarkInEvery()
    {
        var carried = Enumerable.Range(0, CommanderStory.StoryEvery)
            .Select(variant => FlavourBriefs.For(
                new Announcement($"{AmbientCallout.KeyPrefix}Docked", "Quiet out here.") { Variant = variant },
                personalityEnabled: true)!)
            .Count(brief => brief.NeedsStory);

        Assert.Equal(1, carried);
    }

    /// <summary>Only the ambient remarks are written often enough for "sometimes" to mean anything.</summary>
    [Fact]
    public void TheGreetingAndTheIntroductionCarryTheSheetAndNotTheStory()
    {
        var greeting = FlavourBriefs.For(
            new Announcement(ContinuityCallout.Key, "Good evening, Commander. Ready to go."),
            personalityEnabled: true);
        var intro = FlavourBriefs.Introducing("Systems answering, Commander.", personalityEnabled: true);

        Assert.False(greeting!.NeedsStory);
        Assert.False(intro!.NeedsStory);
    }
}
