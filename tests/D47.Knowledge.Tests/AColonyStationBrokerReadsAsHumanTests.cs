using System.Text.Json;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class AColonyStationBrokerReadsAsHumanTests
{
    private static string Row(string colonised) =>
        "{\"count\":1,\"results\":[{\"name\":\"Bobertino Relay\",\"system_name\":\"Shui Wei Sector SD-S b4-3\","
        + "\"technology_broker\":\"Guardian\",\"system_is_colonised\":" + colonised + ","
        + "\"services\":[{\"name\":\"Technology Broker\"}]}]}";

    [Fact]
    public void AColonisedSystemsBrokerIsHumanWhateverTheIndexSays()
    {
        using var document = JsonDocument.Parse(Row("true"));

        Assert.Equal("Human", SpanshResponse.ReadStations(document).Stations[0].TechnologyBroker);
    }

    [Fact]
    public void AnUncolonisedSystemsBrokerKeepsTheIndexsValue()
    {
        using var document = JsonDocument.Parse(Row("false"));

        Assert.Equal("Guardian", SpanshResponse.ReadStations(document).Stations[0].TechnologyBroker);
    }
}
