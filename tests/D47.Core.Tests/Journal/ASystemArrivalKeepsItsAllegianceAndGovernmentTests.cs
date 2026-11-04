using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ASystemArrivalKeepsItsAllegianceAndGovernmentTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private const string Full =
        """{"timestamp":"2026-10-28T10:05:00Z","event":"FSDJump","StarSystem":"Alpha","SystemAddress":1,"SystemAllegiance":"Independent","SystemGovernment":"$government_Democracy;","SystemGovernment_Localised":"Democracy","SystemSecurity":"$SYSTEM_SECURITY_high;","SystemSecurity_Localised":"High Security","SystemSecondEconomy":"$economy_Extraction;","SystemSecondEconomy_Localised":"Extraction","SystemFaction":{"Name":"C.O.N.T.R.A.I.L","FactionState":"Boom"}}""";

    private const string Bare =
        """{"timestamp":"2026-10-28T10:10:00Z","event":"FSDJump","StarSystem":"Beta","SystemAddress":2}""";

    [Fact]
    public void AnArrivalKeepsAllFiveAndTheNextOneWithoutThemClearsThem()
    {
        var there = JournalLocation.Unknown.Apply(Event(Full));

        Assert.Equal("Independent", there.SystemAllegiance);
        Assert.Equal("Democracy", there.SystemGovernment);
        Assert.Equal("High Security", there.SystemSecurity);
        Assert.Equal("Extraction", there.SystemSecondEconomy);
        Assert.Equal("C.O.N.T.R.A.I.L", there.SystemFaction);

        var gone = there.Apply(Event(Bare));

        Assert.Null(gone.SystemAllegiance);
        Assert.Null(gone.SystemGovernment);
        Assert.Null(gone.SystemSecurity);
        Assert.Null(gone.SystemSecondEconomy);
        Assert.Null(gone.SystemFaction);
    }

    [Fact]
    public void LocationAndCarrierJumpKeepThemToo()
    {
        foreach (var kind in new[] { "Location", "CarrierJump" })
        {
            var there = JournalLocation.Unknown.Apply(Event(Full.Replace("FSDJump", kind, StringComparison.Ordinal)));

            Assert.Equal("Democracy", there.SystemGovernment);
            Assert.Equal("C.O.N.T.R.A.I.L", there.SystemFaction);
        }
    }

    [Fact]
    public void ANoneGovernmentAndAnEmptyAllegianceAreNull()
    {
        var json = """{"timestamp":"2026-10-28T10:05:00Z","event":"FSDJump","StarSystem":"Gamma","SystemAddress":3,"SystemAllegiance":"","SystemGovernment":"$government_None;","SystemGovernment_Localised":"None"}""";

        var there = JournalLocation.Unknown.Apply(Event(json));

        Assert.Null(there.SystemAllegiance);
        Assert.Null(there.SystemGovernment);
    }
}
