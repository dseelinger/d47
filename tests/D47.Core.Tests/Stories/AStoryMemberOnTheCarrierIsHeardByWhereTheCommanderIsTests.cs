using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A cast member whose span is on the Commander's fleet carrier is heard in person while the Commander is docked there, over a
/// link that fades with distance while they are away, and through their own link when the distance is unknown (#1739).
/// </summary>
public sealed class AStoryMemberOnTheCarrierIsHeardByWhereTheCommanderIsTests
{
    private static readonly StorySpeaker Quartermaster = new()
    {
        Id = "quartermaster",
        Name = "Ines Harrow",
        Who = "The carrier's quartermaster, brisk and exact.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bf_emma",
        Link = 0.6,
        Aboard = [new("setUp", "midpoint", StorySpeakerAboard.Carrier)],
    };

    private static readonly StorySecret Crewed = Secret with { Cast = [.. Secret.Cast, Quartermaster] };

    private static readonly Story Picked = new() { Id = Id, Title = Card.Title, PublicLayer = "public" };

    private static readonly CarrierState Own = new() { CallSign = "BNH-T2F", StarSystem = "Shinrarta Dezhra" };

    private static readonly StarPosition Home = new(55.7, 17.6, 27.2);

    [Fact]
    public void OnTheShipTheMemberIsHeardInTheCockpitWhateverTheCarrier()
    {
        Assert.Null(CastLink.Of(Quartermaster, onShip: true, onCarrier: false, new CarrierFix(false, 300)));
        Assert.Null(CastLink.Of(Quartermaster, onShip: true, onCarrier: true, new CarrierFix(false, 300)));
        Assert.Null(CastLink.Away(onShip: true, onCarrier: true, new CarrierFix(false, 600)));
    }

    [Fact]
    public void OnTheCarrierWithTheCommanderDockedThereTheMemberIsHeardInPerson()
    {
        Assert.Null(CastLink.Of(Quartermaster, onShip: false, onCarrier: true, new CarrierFix(true, 0)));
        Assert.Null(CastLink.Of(Quartermaster, onShip: false, onCarrier: true, new CarrierFix(true, null)));
    }

    [Fact]
    public void OnTheCarrierAtAKnownDistanceTheLinkFadesButNeverUnderTheFloor()
    {
        Assert.Equal(1, CastLink.Of(Quartermaster, false, true, new CarrierFix(false, 0)));
        Assert.Equal(LinkSignal.Strength(300), CastLink.Of(Quartermaster, false, true, new CarrierFix(false, 300)));
        Assert.True(CastLink.Of(Quartermaster, false, true, new CarrierFix(false, 300)) < 1);
        Assert.Equal(CastLink.Weakest, CastLink.Of(Quartermaster, false, true, new CarrierFix(false, 480)));
        Assert.Equal(CastLink.Weakest, CastLink.Of(Quartermaster, false, true, new CarrierFix(false, 5000)));
        Assert.Equal(300, CastLink.Away(false, true, new CarrierFix(false, 300)));
    }

    [Fact]
    public void OtherwiseTheMembersOwnLinkIsUsed()
    {
        Assert.Equal(0.6, CastLink.Of(Quartermaster, false, true, new CarrierFix(false, null)));
        Assert.Equal(0.6, CastLink.Of(Quartermaster, false, true, null));
        Assert.Equal(0.6, CastLink.Of(Quartermaster, false, false, new CarrierFix(true, 0)));
        Assert.Null(CastLink.Away(false, true, null));
        Assert.Null(CastLink.Away(false, false, new CarrierFix(false, 300)));
    }

    [Fact]
    public void TheFixIsTakenOnlyForACarrierTheCommanderOwns()
    {
        static StarPosition? Known(string system) => system == Own.StarSystem ? Home : null;

        Assert.Null(CarrierFix.Of(null, "Sol", StarPosition.Origin, Known));
        Assert.Null(CarrierFix.Of(CarrierState.None, "Sol", StarPosition.Origin, Known));
        Assert.Null(CarrierFix.Of(Own with { IsSquadron = true }, "Sol", StarPosition.Origin, Known));

        Assert.Equal(new CarrierFix(false, 0), CarrierFix.Of(Own, "shinrarta dezhra", null, Known));
        Assert.Equal(new CarrierFix(true, 0), CarrierFix.Of(Own with { DockedAtOwnCarrier = true }, Own.StarSystem, Home, Known));
        Assert.Equal(new CarrierFix(false, StarPosition.Origin.DistanceTo(Home)), CarrierFix.Of(Own, "Sol", StarPosition.Origin, Known));
        Assert.Equal(new CarrierFix(false, null), CarrierFix.Of(Own, "Sol", null, Known));
        Assert.Equal(new CarrierFix(false, null), CarrierFix.Of(Own with { StarSystem = "Colonia" }, "Sol", StarPosition.Origin, Known));
    }

    [Fact]
    public void ACarrierMemberWhoBoardsAndLaterLeavesIsOnTheCarrierBetweenAndNeverOnTheShip()
    {
        var chapter = new Adventure
        {
            Key = "c",
            Name = "C",
            Aboard = [],
            Beats =
            [
                .. Enumerable.Range(0, 5).Select(at => new AdventureBeat
                {
                    Title = $"B{at}",
                    Line = "Go.",
                    Boards = at == 1 ? ["quartermaster"] : [],
                    Leaves = at == 3 ? ["quartermaster"] : [],
                }),
            ],
        };

        Assert.All(new[] { -1, 0, 3, 4 }, at => Assert.Empty(StoryAboard.OnCarrierAt(Crewed, Picked, chapter, at)));
        Assert.All(new[] { 1, 2 }, at => Assert.Equal(["quartermaster"], StoryAboard.OnCarrierAt(Crewed, Picked, chapter, at)));
        Assert.All(Enumerable.Range(-1, 6), at => Assert.Empty(StoryAboard.At(Crewed, chapter, at)));
        Assert.All(
            Enumerable.Range(0, 6),
            fired => Assert.Empty(StoryAboard.BySpan(Crewed, StoryAboard.Folded(Picked, Crewed, chapter, fired))));
    }

    [Fact]
    public void ACarrierBoardingNeedsNoSeatAndIsNeverHeld()
    {
        var due = StoryAboard.Due(Crewed, Picked, ["setUp"], "Sidewinder");
        var missed = StoryAboard.Missed(Picked, Crewed, ["breakIntoTwo"], "Sidewinder");

        Assert.Equal([new StorySwitch("quartermaster", true, "setUp")], due);
        Assert.Empty(missed.AwaitingSeat);
        Assert.Equal(["quartermaster"], StoryAboard.OnCarrierBySpan(Crewed, missed));
        Assert.Empty(StoryAboard.BySpan(Crewed, missed));
    }

    [Fact]
    public void AChapterKeepsTheLeavingOfAMemberOnTheCarrierAsItOpens()
    {
        var aboard = Picked with { Switched = [new StorySwitch("quartermaster", true, "setUp")] };
        var due = StoryAboard.Due(Crewed, aboard, ["midpoint"]);
        var chapter = new Adventure
        {
            Key = "c",
            Name = "C",
            Aboard = StoryAboard.BySpan(Crewed, aboard),
            Beats = [new AdventureBeat { Title = "B0", Line = "Go.", Leaves = ["quartermaster"] }],
        };

        Assert.Equal([new StorySwitch("quartermaster", false, "midpoint")], due);
        Assert.Equal(["quartermaster"], StoryAboard.Placed(chapter, due).Beats[0].Leaves);
    }

    [Fact]
    public void ALineIsVoicedByWhereTheMemberIsAndWhereTheCommanderIs()
    {
        double? Link(IReadOnlyCollection<string>? onCarrier, CarrierFix? carrier) =>
            StoryVoices.Of("quartermaster", Crewed, null, PersonaCatalog.Kex, onCarrier: onCarrier, carrier: carrier).Pinned!.Link;

        Assert.Null(Link(["quartermaster"], new CarrierFix(true, 0)));
        Assert.Equal(LinkSignal.Strength(300), Link(["quartermaster"], new CarrierFix(false, 300)));
        Assert.Equal(0.6, Link(["quartermaster"], null));
        Assert.Equal(0.6, Link([], new CarrierFix(true, 0)));
        Assert.Null(StoryVoices.Of("quartermaster", Crewed, null, PersonaCatalog.Kex, aboard: ["quartermaster"], onCarrier: ["quartermaster"], carrier: new CarrierFix(false, 300)).Pinned!.Link);
    }

    [Fact]
    public async Task AReplyFromBeyondCommsRangeIsRefusedAndOneInRangeIsThinned()
    {
        var member = new AdventureSpeaker(Quartermaster.Id, Quartermaster.Name!, Quartermaster.Who);
        double? away = 600;
        var line = new CastLine(castId => castId == member.Id ? member : null, () => null, carrierAway: _ => away);
        var selected = OnComms.Cast(member.Id, member.Name);

        var refused = Assert.IsType<LineDecision.Refused>(await line.RouteAsync(selected, "Status?", CancellationToken.None));
        Assert.Equal(TurnRoute.CarrierOutOfRange, refused.Route);
        Assert.Equal(CarrierComms.OutOfRange(600, member.Name), refused.Line);

        away = 300;
        var thinned = Assert.IsType<LineDecision.Taken>(await line.RouteAsync(selected, "Status?", CancellationToken.None));
        Assert.True(thinned.Speaker.Signal < 1);

        away = null;
        var clear = Assert.IsType<LineDecision.Taken>(await line.RouteAsync(selected, "Status?", CancellationToken.None));
        Assert.Equal(1, clear.Speaker.Signal);
    }

    [Theory]
    [InlineData("station", "place is station")]
    [InlineData(null, null)]
    [InlineData(StorySpeakerAboard.Ship, null)]
    [InlineData(StorySpeakerAboard.Carrier, null)]
    public void APlaceOtherThanShipOrCarrierIsAFault(string? place, string? fault)
    {
        var placed = Crewed with { Cast = [.. Secret.Cast, Quartermaster with { Aboard = [new("setUp", "midpoint", place)] }] };
        var faults = StoryCatalog.Faults(Card, placed);

        if (fault is null)
        {
            Assert.DoesNotContain(faults, found => found.Contains(".place", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(faults, found => found.Contains(fault, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SpansInDifferentPlacesAreAFault()
    {
        var mixed = Crewed with
        {
            Cast = [.. Secret.Cast, Quartermaster with { Aboard = [new("setUp", "catalyst", StorySpeakerAboard.Carrier), new("midpoint", null)] }],
        };

        Assert.Contains(StoryCatalog.Faults(Card, mixed), found => found.Contains("share one place", StringComparison.Ordinal));
    }
}
