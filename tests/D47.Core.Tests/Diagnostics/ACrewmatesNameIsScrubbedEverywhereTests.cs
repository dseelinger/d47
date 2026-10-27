using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

public class ACrewmatesNameIsScrubbedEverywhereTests
{
    private static string Scrub(string json, Pseudonyms names) => JournalScrub.Line(json, names).Json!;

    [Fact]
    public void AnEmbarkCrewNameGetsTheStandInCrewMemberJoinsGave()
    {
        var names = new Pseudonyms();

        var joined = Scrub("""{"event":"CrewMemberJoins","Crew":"Zed Mate"}""", names);
        var embark = Scrub(
            """{"event":"Embark","Crew":[{"Name":"Zed Mate","Role":"Gunner"}],"Body":"Moon"}""", names);
        var stand = names.Person("Zed Mate");

        Assert.Contains(stand, joined);
        Assert.Contains(stand, embark);
        Assert.DoesNotContain("Zed Mate", embark);
    }

    [Fact]
    public void ADisembarkCrewNameIsReplaced()
    {
        var names = new Pseudonyms();

        var line = Scrub("""{"event":"Disembark","Crew":[{"Name":"Zed Mate","Role":"Gunner"}]}""", names);

        Assert.DoesNotContain("Zed Mate", line);
    }

    [Fact]
    public void AValueEqualToOneAlreadyReplacedGetsTheSameStandInInAnyField()
    {
        var names = new Pseudonyms();

        Scrub("""{"event":"Docked","StationName":"Zed's Pad","StationType":"Outpost"}""", names);
        Assert.False(names.Known("Zed's Pad", out _));

        names.Person("Zed's Pad");
        var line = Scrub("""{"event":"Disembark","Body":"Zed's Pad","BodyID":3}""", names);

        Assert.DoesNotContain("Zed's Pad", line);
        Assert.Contains(names.Person("Zed's Pad"), line);
    }

    [Fact]
    public void AValueThatOnlyContainsAReplacedNameIsLeftAlone()
    {
        var names = new Pseudonyms();
        names.Person("Zed");

        var line = Scrub("""{"event":"Location","Body":"Zed Prime 1","StarSystem":"Zed"}""", names);

        Assert.Contains("Zed Prime 1", line);
        Assert.DoesNotContain("\"StarSystem\":\"Zed\"", line);
    }
}
