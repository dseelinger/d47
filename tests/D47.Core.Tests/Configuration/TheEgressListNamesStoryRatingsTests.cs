using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesStoryRatingsTests
{
    private static EgressEntry Entry(bool on)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.StoryRatings,
            settings with { Ui = settings.Ui with { StoryRatings = on } },
            llmKeyPresent: true);
    }

    [Fact]
    public void OnItNamesTheWorkerWhatIsSentAndWhatIsNot()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("Story ratings", entry.Name);
        Assert.Equal(StoryRatingSettings.Address, entry.Destination);
        Assert.Contains("a random number made on this PC for your Commander", entry.What, StringComparison.Ordinal);
        Assert.Contains("no Frontier ID, no Commander name, no key, no position and nothing from your journal", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OffItSaysNothingIsFetchedOrSent()
    {
        var entry = Entry(on: false);

        Assert.False(entry.Active);
        Assert.Equal("Story ratings are off, so nothing is fetched or sent.", entry.What);
    }

    [Fact]
    public void TheEntryIsInTheFixedListAndTheDefaultIsOn()
    {
        Assert.Contains(EgressDisclosure.StoryRatings, EgressDisclosure.Ids);
        Assert.True(new D47Settings().Ui.StoryRatings);
        Assert.Equal("https://d47-ratings.dseelinger.workers.dev", StoryRatingSettings.Address);
    }

    [Fact]
    public void TheSettingRowIsLabelledAndTiedToTheEntry()
    {
        var row = AdventureCapability.Create().Settings.Single(setting => setting.Key == AdventureCapability.StoryRatingsKey);
        var off = new D47Settings();

        Assert.Equal("Story ratings", row.Label);
        Assert.Equal(EgressDisclosure.StoryRatings, row.EgressId);
        var binding = row.Binding!;

        Assert.Equal("true", binding.Read(off));
        Assert.Equal("false", binding.Read(binding.Write!(off, "false")));
    }
}
