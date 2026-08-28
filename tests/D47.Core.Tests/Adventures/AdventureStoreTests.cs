using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The adventures on disk (Phase 47). Hand-editable, per Commander, and a record that
/// cannot be read back is reported by name rather than dropped.
/// </summary>
public class AdventureStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-store", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureStore Store() =>
        new(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);

    [Fact]
    public void AnAdventureRoundTripsThroughTheFile()
    {
        var store = Store();

        Assert.Null(store.Save("F1", LanternRoute(Accepted)));

        var reread = Store();
        reread.Poll();

        var back = Assert.Single(reread.For("F1"));

        Assert.Equal("The Lantern Route", back.Name);
        Assert.Equal(AdventureSource.Generated, back.Source);
        Assert.Equal(Accepted, back.AcceptedAt);
        Assert.Equal(5, back.Beats.Count);
        Assert.Equal(TriggerKind.Dock, back.Beats[2].Trigger.Kind);
        Assert.Equal(Anchorage, back.Beats[2].Trigger.MarketId);
        Assert.Equal("Maren Anchorage", back.Beats[2].Trigger.Station);
        Assert.Equal("The beacon speaks to one person by name.", back.Spine?.Turn);
        Assert.Empty(reread.Problems);
    }

    [Fact]
    public void TheFileIsReadableByAPerson()
    {
        var store = Store();
        store.Save("F1", LanternRoute());

        var text = File.ReadAllText(store.Path);

        Assert.Contains("\"kind\": \"dock\"", text);
        Assert.Contains("\"marketId\": 3700481092", text);
        Assert.Contains("\"station\": \"Maren Anchorage\"", text);
        Assert.Contains("\"source\": \"generated\"", text);
    }

    [Fact]
    public void TwoCommandersAreKeptApart()
    {
        var store = Store();
        store.Save("F1", LanternRoute());
        store.Save("F2", LanternRoute() with { Key = "other", Name = "Other" });

        Assert.Single(store.For("F1"));
        Assert.Equal("Other", Assert.Single(store.For("F2")).Name);
        Assert.Empty(store.For("F3"));
    }

    [Fact]
    public void SavingTheSameKeyReplaces()
    {
        var store = Store();
        store.Save("F1", LanternRoute());
        store.Save("F1", LanternRoute() with { Name = "Renamed" });

        Assert.Equal("Renamed", Assert.Single(store.For("F1")).Name);
    }

    [Fact]
    public void AnInvalidAdventureIsRefusedWithTheReasonAndNotWritten()
    {
        var store = Store();

        var refusal = store.Save("F1", LanternRoute() with { Beats = [] });

        Assert.Contains("at least one beat", refusal);
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void ARecordNamingAnUnknownTriggerIsRefusedByNameAndTheRestLoad()
    {
        var store = Store();
        store.Save("F1", LanternRoute());

        var text = File.ReadAllText(store.Path).Replace("\"kind\": \"dock\"", "\"kind\": \"deliver\"");
        var broken = LanternRoute() with { Key = "fine", Name = "Fine" };

        // A second, valid adventure beside the broken one, written by hand.
        var withSecond = text.Replace(
            "\"adventures\": [",
            "\"adventures\": [\n" + System.Text.Json.JsonSerializer.Serialize(new
            {
                key = "fine",
                name = "Fine",
                beats = new[] { new { title = "T", trigger = new { kind = "arrive", systemAddress = 1 }, line = "L" } },
            }) + ",");

        File.WriteAllText(store.Path, withSecond);

        var reread = Store();
        reread.Poll();

        var problem = Assert.Single(reread.Problems);
        Assert.Equal("The Lantern Route", problem.Where);
        Assert.Contains("deliver", problem.Reason);
        Assert.Contains("arrive, dock, land, scan, rank", problem.Reason);

        Assert.Equal("Fine", Assert.Single(reread.For("F1")).Name);
        _ = broken;
    }

    [Fact]
    public void RemoveDeletesTheRecord()
    {
        var store = Store();
        store.Save("F1", LanternRoute());

        Assert.True(store.Remove("F1", "the-lantern-route"));
        Assert.False(store.Remove("F1", "the-lantern-route"));
        Assert.Empty(store.For("F1"));
    }

    [Fact]
    public void APreviousDraftRidesAlong()
    {
        var store = Store();
        var draft = LanternRoute() with { Previous = LanternRoute() with { Name = "First draft" } };

        store.Save("F1", draft);

        var reread = Store();
        reread.Poll();

        Assert.Equal("First draft", Assert.Single(reread.For("F1")).Previous?.Name);
    }

    [Fact]
    public void PollSeesAHandEdit()
    {
        var store = Store();
        store.Save("F1", LanternRoute());

        var raised = 0;
        store.Changed += () => raised++;

        File.WriteAllText(store.Path, File.ReadAllText(store.Path).Replace("The Lantern Route", "The Beacon Route"));

        Assert.True(store.Poll());
        Assert.Equal(1, raised);
        Assert.Equal("The Beacon Route", Assert.Single(store.For("F1")).Name);
        Assert.False(store.Poll());
    }
}
