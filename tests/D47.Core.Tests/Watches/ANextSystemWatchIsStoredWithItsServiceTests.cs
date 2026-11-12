using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ANextSystemWatchIsStoredWithItsServiceTests
{
    private const string WatchesFile = @"C:\d47\data\watches.json";

    private static readonly DateTimeOffset Fired = new(2026, 11, 12, 20, 31, 0, TimeSpan.Zero);

    private static WatchStore Store(MemoryFileSystem files) => new(WatchesFile, files, NullLogger<WatchStore>.Instance);

    private static Watch Guardian() => new(
        Guid.NewGuid(), "Guardian technology broker", WatchKind.NextSystem,
        Service: "Technology Broker", ServiceKind: "Guardian", MinPad: PadSize.Large, CheckRoute: true);

    [Fact]
    public void AWrittenWatchAndItsFiringReadBackInAnotherStore()
    {
        var files = new MemoryFileSystem();
        var writer = Store(files);
        var watch = Guardian();

        Assert.Null(writer.Add(watch));
        Assert.True(writer.MarkArrived(watch.Id, "Kremainn", Fired));

        var reader = Store(files);
        Assert.True(reader.Poll());

        Assert.Equal(
            watch with { Enabled = false, FiredSystem = "Kremainn", FiredAt = Fired },
            Assert.Single(reader.Watches));
        Assert.Empty(reader.Problems);
    }

    [Fact]
    public void AServiceOrKindIsReadInTheListsSpelling()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            WatchesFile,
            """{"watches":[{"name":"Traders","kind":"nextSystem","service":"material trader","serviceKind":"manufactured"}]}""");

        var store = Store(files);
        store.Poll();

        var watch = Assert.Single(store.Watches);
        Assert.Equal("Material Trader", watch.Service);
        Assert.Equal("Manufactured", watch.ServiceKind);
        Assert.False(watch.CheckRoute);
    }

    [Theory]
    [InlineData("""{"name":"A","kind":"nextSystem"}""", "it names no station service.")]
    [InlineData("""{"name":"A","kind":"nextSystem","service":"Cheap Fuel"}""", "'Cheap Fuel' is not a station service D47 knows.")]
    [InlineData("""{"name":"A","kind":"nextSystem","service":"Material Trader","serviceKind":"Guardian"}""", "'Guardian' is not a kind of Material Trader. It is Raw, Manufactured or Encoded.")]
    [InlineData("""{"name":"A","kind":"nextSystem","service":"Black Market","serviceKind":"Raw"}""", "Black Market has no kinds to choose from.")]
    [InlineData("""{"name":"A","kind":"nextSystem","service":"Black Market","minPad":"small"}""", "the pad is Medium or Large.")]
    public void ALineWithAServiceKindOrPadNotInTheListsIsRefused(string line, string reason)
    {
        var files = new MemoryFileSystem();
        files.WriteText(WatchesFile, $$"""{"watches":[{{line}}]}""");

        var store = Store(files);
        store.Poll();

        Assert.Empty(store.Watches);
        Assert.Equal(reason, Assert.Single(store.Problems).Reason);
    }

    [Fact]
    public void TheLimitCoversEveryKind()
    {
        var store = Store(new MemoryFileSystem());

        for (var i = 0; i < WatchStore.MaxWatches; i++)
        {
            Assert.Null(store.Add(new Watch(Guid.NewGuid(), $"Goals {i}", WatchKind.NewCommunityGoal)));
        }

        Assert.Equal($"the file already holds {WatchStore.MaxWatches} watches.", store.Add(Guardian()));
    }

    [Fact]
    public void ReEnablingKeepsTheFiringAndChangingTheServiceForgetsIt()
    {
        var store = Store(new MemoryFileSystem());
        var watch = Guardian();
        store.Add(watch);
        store.MarkArrived(watch.Id, "Kremainn", Fired);

        Assert.Null(store.Update(watch with { Enabled = true }));
        Assert.Equal("Kremainn", Assert.Single(store.Watches).FiredSystem);

        Assert.Null(store.Update(watch with { ServiceKind = "Human" }));
        var changed = Assert.Single(store.Watches);
        Assert.Null(changed.FiredSystem);
        Assert.Null(changed.FiredAt);
    }

    [Fact]
    public void TheListNamesTheConditionAndWhereItFired()
    {
        var watch = Guardian() with { Enabled = false, FiredSystem = "Kremainn", FiredAt = Fired };

        Assert.Equal(
            "1 watch.\nGuardian technology broker: a station with a Guardian technology broker, a large pad, in the next system, off. Kremainn · 12 Nov 20:31.",
            WatchesCapability.Describe([watch], TimeZoneInfo.Utc));
    }
}
