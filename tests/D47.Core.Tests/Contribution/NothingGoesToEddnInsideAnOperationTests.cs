using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class NothingGoesToEddnInsideAnOperationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private const string Header =
        """{"timestamp":"2026-10-25T10:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r1"}""";

    private const string LoadGame =
        """{"timestamp":"2026-10-25T11:00:00Z","event":"LoadGame","Horizons":true,"Odyssey":true,"gameversion":"4.1.0.100","build":"r1"}""";

    private const string EnterOperation =
        """{"timestamp":"2026-10-25T11:00:00Z","event":"GameModeChange","GameMode":"Operation"}""";

    private const string ExitMainGame =
        """{"timestamp":"2026-10-25T11:30:00Z","event":"GameModeChange","GameMode":"MainGame"}""";

    private const string MainJump =
        """{"timestamp":"2026-10-25T10:30:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[1,2,3]}""";

    private const string MainSignal =
        """{"timestamp":"2026-10-25T10:59:00Z","event":"FSSSignalDiscovered","SystemAddress":10477373803,"SignalName":"Sol Beacon","SignalType":"NavBeacon","IsStation":false}""";

    private const string InstanceSignal =
        """{"timestamp":"2026-10-25T11:00:02Z","event":"FSSSignalDiscovered","SystemAddress":2832631632297,"SignalName":"$Operations_Runner_Name;","IsStation":true}""";

    private const string InstanceLocation =
        """{"timestamp":"2026-10-25T11:00:05Z","event":"Location","StarSystem":"Ceos","SystemAddress":2832631632297,"StarPos":[5,6,7],"Docked":true,"StationName":"$Operations_Runner_Name;"}""";

    private const string ExitLocation =
        """{"timestamp":"2026-10-25T11:30:05Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[1,2,3],"Docked":true,"StationName":"Abraham Lincoln"}""";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnComposer Composer()
    {
        var composer = new EddnComposer(() => "anon", "1", () => null);
        composer.Observe(Event(Header));
        composer.Observe(Event(MainJump));

        return composer;
    }

    private static List<EddnEnvelope> Feed(EddnComposer composer, params string[] lines) =>
        [.. lines.SelectMany(line => composer.ComposeAll(Event(line), T0))];

    [Fact]
    public void NothingIsComposedBetweenEnteringAndLeavingAnOperation()
    {
        var composer = Composer();

        Assert.Empty(Feed(composer, LoadGame, EnterOperation, InstanceSignal, InstanceSignal, InstanceLocation));
        Assert.Null(composer.Flush(T0.AddMinutes(1)));
    }

    [Fact]
    public void TheLocationAfterLeavingAnOperationIsComposedAsNormal()
    {
        var composer = Composer();

        Feed(composer, LoadGame, EnterOperation, InstanceSignal, InstanceLocation);
        var after = Feed(composer, LoadGame, ExitMainGame, ExitLocation);

        var envelope = Assert.Single(after);
        Assert.Equal("Sol", (string?)envelope.Message["StarSystem"]);
    }

    [Fact]
    public void ARunOpenedInTheMainGameIsSentByTheLoadGameThatBeginsAnOperation()
    {
        var composer = Composer();

        Assert.Empty(Feed(composer, MainSignal));

        var sent = Feed(composer, LoadGame, EnterOperation, InstanceSignal, InstanceLocation);

        var envelope = Assert.Single(sent);
        Assert.Equal("Sol", (string?)envelope.Message["StarSystem"]);
        Assert.Equal("Sol Beacon", (string?)envelope.Message["signals"]![0]!["SignalName"]);
    }

    [Fact]
    public void ALaunchThatPrimesIntoAnOperationStartsInsideIt()
    {
        var composer = Composer();
        composer.Observe(Event(LoadGame));
        composer.Observe(Event(EnterOperation));

        Assert.Empty(Feed(composer, InstanceLocation));
    }

    [Fact]
    public void ALoadGameAloneLeavesAnOperation()
    {
        var composer = Composer();

        Feed(composer, LoadGame, EnterOperation);

        Assert.Single(Feed(composer, LoadGame, ExitLocation));
    }

    [Fact]
    public void ComposeReturnsNothingInsideAnOperation()
    {
        var composer = Composer();
        composer.Compose(Event(LoadGame));
        composer.Compose(Event(EnterOperation));

        Assert.Null(composer.Compose(Event(InstanceLocation)));
    }
}
