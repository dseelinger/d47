using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Hotas;
using D47.Core.Loadout;
using D47.Core.Lore;
using D47.Core.Memory;
using D47.Core.Persona;
using D47.Core.Ships;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Storage;

[Trait("Category", "Integration")]
public class AStoreDoesNotOpenAnUnchangedFileTests : IDisposable
{
    private static readonly DateTime Stamp = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-store-stamp-tests", Guid.NewGuid().ToString("N"));

    public static TheoryData<string> Stores =>
    [
        "adventures", "goals", "switches", "ship builds", "on-foot builds", "ship cores", "memory", "lore", "alarms",
    ];

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void WhenTheWriteTimeAndLengthAreUnchangedThePollNeverReadsTheText(string store)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "store.json");
        var poll = PollOf(store, path);

        Write(path, "not json 1");

        Assert.True(poll());
        Assert.False(poll());

        // Same length and write time, different text: only a read could notice.
        Write(path, "not json 2");

        Assert.False(poll());

        File.SetLastWriteTimeUtc(path, Stamp.AddSeconds(1));

        Assert.True(poll());
    }

    private static void Write(string path, string text)
    {
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, Stamp);
    }

    private static Func<bool> PollOf(string store, string path) => store switch
    {
        "adventures" => new AdventureStore(path, NullLogger<AdventureStore>.Instance).Poll,
        "goals" => new GoalStore(path, NullLogger<GoalStore>.Instance).Poll,
        "switches" => new SwitchStore(path, NullLogger<SwitchStore>.Instance).Poll,
        "ship builds" => new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance).Poll,
        "on-foot builds" => new OnFootBuildStore(path, NullLogger<OnFootBuildStore>.Instance).Poll,
        "ship cores" => new ShipCoreStore(path, NullLogger<ShipCoreStore>.Instance).Poll,
        "memory" => new MemoryStore(path, NullLogger<MemoryStore>.Instance).Poll,
        "lore" => new LoreStore(path, NullLogger<LoreStore>.Instance).Poll,
        "alarms" => new AlarmStore(path, NullLogger<AlarmStore>.Instance).Poll,
        _ => throw new ArgumentOutOfRangeException(nameof(store)),
    };
}
