using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class CareerRatingsArePerCommanderTests
{
    private const string Mining = "adventures.rating.mining";

    private static readonly string[] Keys =
    [
        "combat", "trading", "mining", "exploration", "exobiology", "passengers", "searchAndRescue", "crime",
    ];

    [Fact]
    public void ACommanderWithNoRatingsReadsFiftyForEach()
    {
        var projected = CommanderScope.Project(new D47Settings(), "F1");
        var surface = TestSurface.For(new MemoryInstall());

        foreach (var key in Keys)
        {
            var row = surface.Settings.Sections.SelectMany(section => section.Rows)
                .Single(row => row.Key == AdventureCapability.RatingKeyPrefix + key);

            Assert.Equal("50", row.Binding!.Read(projected));
        }
    }

    [Fact]
    public void ProjectLaysEachRatingOverTheInstallations()
    {
        var stored = new D47Settings
        {
            Commanders =
            [
                new CommanderSettings
                {
                    CommanderFid = "F1",
                    CombatRating = 1,
                    TradingRating = 2,
                    MiningRating = 3,
                    ExplorationRating = 4,
                    ExobiologyRating = 5,
                    PassengersRating = 6,
                    SearchAndRescueRating = 7,
                    CrimeRating = 8,
                },
            ],
        };

        var ratings = CommanderScope.Project(stored, "F1").CareerRatings;

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], Listed(ratings));
        Assert.Equal([50, 50, 50, 50, 50, 50, 50, 50], Listed(CommanderScope.Project(stored, "F2").CareerRatings));
    }

    [Fact]
    public void PersistWritesEachChangedRatingToTheCommandersOverlayOnly()
    {
        var stored = new D47Settings();
        var effective = CommanderScope.Project(stored, "F1");

        var next = effective with
        {
            CareerRatings = new CareerRatingSettings
            {
                Combat = 11,
                Trading = 12,
                Mining = 13,
                Exploration = 14,
                Exobiology = 15,
                Passengers = 16,
                SearchAndRescue = 17,
                Crime = 18,
            },
        };

        var written = CommanderScope.Persist(stored, effective, next, "F1", "Alice");

        Assert.Equal([50, 50, 50, 50, 50, 50, 50, 50], Listed(written.CareerRatings));

        var overlay = Assert.Single(written.Commanders);
        Assert.Equal(
            [11, 12, 13, 14, 15, 16, 17, 18],
            new int?[]
            {
                overlay.CombatRating, overlay.TradingRating, overlay.MiningRating, overlay.ExplorationRating,
                overlay.ExobiologyRating, overlay.PassengersRating, overlay.SearchAndRescueRating, overlay.CrimeRating,
            }.Select(rating => rating!.Value));
    }

    [Fact]
    public void TwoCommandersOnOneInstallKeepSeparateRatings()
    {
        var surface = TestSurface.For(new MemoryInstall());

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(Mining, "80", SettingsCaller.Panel);

        surface.Settings.UseCommander("F2", "Bob");
        Assert.Equal(50, surface.Settings.Current.CareerRatings.Mining);
        surface.Settings.Apply(Mining, "20", SettingsCaller.Panel);

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal(80, surface.Settings.Current.CareerRatings.Mining);

        surface.Settings.UseCommander("F2", "Bob");
        Assert.Equal(20, surface.Settings.Current.CareerRatings.Mining);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-5")]
    public void AValueOutsideOneToAHundredIsRefused(string value)
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.UseCommander("F1", "Alice");

        var result = surface.Settings.Apply(Mining, value, SettingsCaller.KeywordRouter);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Equal(50, surface.Settings.Current.CareerRatings.Mining);
    }

    [Theory]
    [InlineData("set my mining rating to 80", "80")]
    [InlineData("set my mining rating to eighty", "80")]
    [InlineData("rate mining 35", "35")]
    [InlineData("rate mining to twenty five", "25")]
    [InlineData("set my mining rating to one hundred", "100")]
    public void ARatingIsSetByVoiceWithoutTheModel(string said, string value)
    {
        var surface = TestSurface.For(new MemoryInstall());

        var heard = surface.Router.MatchSettingBySound(said);

        Assert.NotNull(heard);
        Assert.True(heard.Certain);
        var match = Assert.Single(heard.Matches);
        Assert.Equal(Mining, match.Row.Key);
        Assert.Equal(value, match.Value);
    }

    [Fact]
    public void ASpokenValueOutsideTheRangeIsRefused()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.UseCommander("F1", "Alice");

        var match = Assert.Single(surface.Router.MatchSettingBySound("set my mining rating to 150")!.Matches);
        var result = surface.Settings.Apply(match.Row.Key, match.Value, SettingsCaller.KeywordRouter);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
    }

    [Fact]
    public void TheModelCannotWriteAnyRating()
    {
        var surface = TestSurface.For(new MemoryInstall());

        foreach (var key in Keys)
        {
            var result = surface.Settings.Apply(AdventureCapability.RatingKeyPrefix + key, "80", SettingsCaller.Model);

            Assert.False(result.Ok);
        }

        Assert.Equal(50, surface.Settings.Current.CareerRatings.Mining);
    }

    private static int[] Listed(CareerRatingSettings ratings) =>
    [
        ratings.Combat, ratings.Trading, ratings.Mining, ratings.Exploration,
        ratings.Exobiology, ratings.Passengers, ratings.SearchAndRescue, ratings.Crime,
    ];
}

public class CareerRatingsAreOnTheStoriesTabTests
{
    [Fact]
    public void TheStoriesSettingsHoldAllEightRatings()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var keys = surface.Settings.Sections.SelectMany(section => section.Rows)
            .Select(row => row.Key)
            .Where(SettingsLayout.IsCareerRatingFamily)
            .ToList();

        var tab = SettingsLayout.Tabs.Single(place => place.Id == "adventures");

        Assert.Equal(8, keys.Count);
        Assert.All(keys, key => Assert.Contains(tab.Entries, entry => entry.Family?.Invoke(key) == true));
    }
}

public class EachCareerRatingSaysWhatItIsForTests
{
    [Fact]
    public void EveryRatingRowCarriesAVisibleDescription()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var rows = surface.Settings.Sections.SelectMany(section => section.Rows)
            .Where(row => SettingsLayout.IsCareerRatingFamily(row.Key))
            .ToList();

        Assert.Equal(8, rows.Count);
        Assert.All(rows, row => Assert.Contains("1 to 100", row.Note!(new D47Settings())));
        Assert.Contains("Law and Disorder", rows.Single(row => row.Label == "Crime").Note!(new D47Settings()));
    }
}
