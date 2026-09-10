using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The Commander's own carrier colours an invented exchange rather than being the one fact it always
/// reaches for (#88).
/// </summary>
public class TheInventedPeopleKnowWhoseDeckTheyAreOnTests
{
    private static NpcChatterCarrier Present(string? destinationSystem = null)
    {
        var mine = CarrierState.None with
        {
            CallSign = "K7Q-B4Z",
            Name = "Nomad's Rest",
            CarrierId = 3_700_123_456,
            StarSystem = "Shinrarta Dezhra",
            DestinationSystem = destinationSystem,
        };

        return NpcChatterCarrier.Of(
            mine,
            new JournalLocation("Shinrarta Dezhra", null, true, "K7Q-B4Z")
            {
                Mode = FlightMode.Docked, StationType = "FleetCarrier", MarketId = 3_700_123_456,
            });
    }

    private static NpcChatterCarrier Absent()
    {
        var mine = CarrierState.None with
        {
            CallSign = "K7Q-B4Z",
            Name = "Nomad's Rest",
            CarrierId = 3_700_123_456,
            StarSystem = "Shinrarta Dezhra",
        };

        return NpcChatterCarrier.Of(mine, new JournalLocation("Sol", null, false, null));
    }

    // ---- What the prompt says, with Owned and Present set ------------------------------------

    [Fact]
    public void ItTellsHowHisOwnCrewRegardHim()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Controller, Present());

        Assert.Contains("not surprised he is aboard", prompt, StringComparison.Ordinal);
        Assert.Contains("their job to be here", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ItTellsHowAVisitingPilotRegardsHim()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, Present());

        Assert.Contains("surprised", prompt, StringComparison.Ordinal);
        Assert.Contains("embarrassed to have been overheard", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutTheSpotlightItRefusesToMakeOwnershipTheSubject()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, Present(), spotlight: false);

        Assert.Contains("Do not make his owning the place the subject", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WithTheSpotlightItMayMakeOwnershipTheSubject()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, Present(), spotlight: true);

        Assert.Contains("may make his owning the place the thing being talked about", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Do not make his owning the place the subject", prompt, StringComparison.Ordinal);
    }

    // ---- With Present false, nothing treats him as the owner of where he is ------------------

    [Fact]
    public void WithPresentFalseNothingTreatsHimAsOwnerOfWhereHeIs()
    {
        var about = Absent();
        Assert.True(about.Owned);
        Assert.False(about.Present);

        foreach (var kind in Enum.GetValues<NpcChatterKind>())
        {
            var prompt = NpcChatter.Instruction(kind, about, spotlight: true);

            Assert.DoesNotContain("whose deck this is", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("owning the place", prompt, StringComparison.Ordinal);
        }
    }

    // ---- The naming rule from #249 still holds -------------------------------------------

    [Fact]
    public void TheTowerAndCaptainCastingRuleStillHolds()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Controller, Present());

        Assert.Contains($"exactly {NpcChatter.TowerName} or exactly {NpcChatter.CaptainName}", prompt, StringComparison.Ordinal);
    }

    // ---- Across a visit, ownership is the subject of at most one exchange --------------------

    [Fact]
    public void AcrossAVisitOwnershipIsTheSubjectOfAtMostOne()
    {
        var spotlight = new NpcChatterOwnershipSpotlight();
        var claims = Enumerable.Range(0, 12).Select(_ => spotlight.Claim(present: true)).ToList();

        Assert.Single(claims, claim => claim);
        Assert.True(claims[0]);
    }

    [Fact]
    public void LeavingReturnsTheSpotlightForTheNextVisit()
    {
        var spotlight = new NpcChatterOwnershipSpotlight();

        Assert.True(spotlight.Claim(present: true));
        Assert.False(spotlight.Claim(present: true));

        // He steps away — the visit ends.
        Assert.False(spotlight.Claim(present: false));

        // And a new visit gets its own spotlight.
        Assert.True(spotlight.Claim(present: true));
    }

    [Fact]
    public void NeverPresentNeverClaimsIt()
    {
        var spotlight = new NpcChatterOwnershipSpotlight();

        var claims = Enumerable.Range(0, 5).Select(_ => spotlight.Claim(present: false)).ToList();

        Assert.DoesNotContain(true, claims);
    }
}
