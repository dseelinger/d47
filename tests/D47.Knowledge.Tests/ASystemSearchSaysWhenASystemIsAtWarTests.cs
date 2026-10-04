using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class ASystemSearchSaysWhenASystemIsAtWarTests
{
    [Fact]
    public void ActiveAndPendingWarsAndCivilWarsCount()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "spansh-search-faction-present-eurybia-blue-mafia.json")));

        var systems = SpanshResponse.ReadSearch(document).Systems.ToDictionary(system => system.Name);

        Assert.True(systems["Eurybia"].AtWar);
        Assert.True(systems["Hyades Sector RE-G b11-3"].AtWar);
        Assert.True(systems["Trianguli Sector SO-R b4-6"].AtWar);
    }

    [Fact]
    public void AnElectionIsNotAWar()
    {
        using var document = JsonDocument.Parse("""
            {"count":1,"results":[{"name":"Quiet Reach","minor_faction_presences":[
              {"name":"A","active_states":["Election"],"pending_states":["Boom"]},
              {"name":"B","active_states":["Election"]}]}]}
            """);

        Assert.False(SpanshResponse.ReadSearch(document).Systems[0].AtWar);
    }
}
