using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A canned line from the Commander's own carrier is always addressed to its owner (#290).</summary>
public class YourOwnCarrierSpeaksToItsOwnerTests
{
    private const string Commander = "Doug Seelinger";

    private const string Owner = "Commander Seelinger";

    private static Announcement CarrierLine(string messageKey = "$DockingChatter_Neutral;") =>
        new(IncomingMessages.CarrierCannedKey, "Ensure to observe starport protocol during your visit, pilot.")
        {
            Voice = VoiceRole.TowerControl,
            CommsChannel = "npc",
            Transcript = "Sacred Fire BNH-T2F: Ensure to observe starport protocol during your visit, pilot.\n",
            MessageKey = messageKey,
        };

    private static Announcement AuthorityLine() =>
        new(IncomingMessages.AuthorityCannedKey, "Patrol vessel on station.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "System Authority Vessel",
            CommsChannel = "npc",
            Transcript = "System Authority Vessel: Patrol vessel on station.\n",
        };

    private static Func<FlavourBrief, string, CancellationToken, Task<FlavourReply>> From(
        ILlmProvider provider) =>
        (brief, ask, token) => FlavourTurn.AskForAsync(
            provider, null, null, null, ask, null, null, null, NullLogger.Instance, token);

    private static Task<Announcement?> Vary(
        Announcement announcement,
        ILogger? logger = null,
        bool hasModel = true,
        bool personality = true,
        int percent = 100,
        TimeSpan? budget = null,
        ILlmProvider? provider = null) =>
        new Rewording(new RewordChance(new Random(1)), logger)
        {
            Budget = budget ?? TimeSpan.FromSeconds(3),
        }.VaryAsync(
            announcement,
            hasModel,
            personality,
            percent,
            () => ShipFacts.Unknown,
            Commander,
            From(provider ?? new FakeLlmProvider(new LlmStreamEvent.Failed("overloaded", false))));

    private static FakeLlmProvider Replying(string line) =>
        new(new LlmStreamEvent.TextDelta(line),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed));

    [Fact]
    public void ACarrierLineIsRewordedEvenAtZeroPercent()
    {
        var chance = new RewordChance(new Random(1));

        for (var i = 0; i < 50; i++)
        {
            Assert.True(chance.ShouldReword(CarrierLine(), rewordPercent: 0));
        }
    }

    [Fact]
    public void TheSystemAuthorityLineStillRollsAtTheRewordPercent()
    {
        var chance = new RewordChance(new Random(1));

        for (var i = 0; i < 50; i++)
        {
            Assert.False(chance.ShouldReword(AuthorityLine(), rewordPercent: 0));
        }
    }

    public static TheoryData<string> Misses => ["no model", "personality off", "timed out", "failed", "rejected"];

    private static Task<Announcement?> Missing(string how, ILogger? logger = null) => how switch
    {
        "no model" => Vary(CarrierLine(), logger, hasModel: false),
        "personality off" => Vary(CarrierLine(), logger, personality: false),
        "timed out" => Vary(CarrierLine(), logger, budget: TimeSpan.Zero, provider: Replying("Welcome, owner.")),
        "failed" => Vary(CarrierLine(), logger),
        "rejected" => Vary(CarrierLine(), logger, provider: Replying("I don't have that capability.")),
        _ => throw new ArgumentOutOfRangeException(nameof(how)),
    };

    [Theory]
    [MemberData(nameof(Misses))]
    public async Task WithNoRewriteTheOwnerHearsTheAuthoredLine(string how)
    {
        var spoken = await Missing(how);

        Assert.NotNull(spoken);
        Assert.Equal($"Welcome back, {Owner}.", spoken.Text);
        Assert.DoesNotContain("pilot", spoken.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Commander, spoken.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Misses))]
    public async Task ALineSpokenAsWrittenSaysWhyAtInformation(string how)
    {
        var logger = new RecordingLogger();

        await Missing(how, logger);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(IncomingMessages.CarrierCannedKey, entry.Message, StringComparison.Ordinal);
        Assert.Contains(how, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedStreamLogsTheProvidersMessage()
    {
        var logger = new RecordingLogger();

        await Vary(CarrierLine(), logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("overloaded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnotherBriefedLineSpokenOnTheRollSaysSo()
    {
        var logger = new RecordingLogger();

        var spoken = await Vary(AuthorityLine(), logger, percent: 0);

        Assert.Equal(AuthorityLine().Text, spoken?.Text);
        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(IncomingMessages.AuthorityCannedKey, entry.Message, StringComparison.Ordinal);
        Assert.Contains("roll", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARewriteThatComesBackIsSpoken()
    {
        var spoken = await Vary(CarrierLine(), provider: Replying($"Welcome home, {Owner}."));

        Assert.Equal($"Welcome home, {Owner}.", spoken?.Text);
    }

    [Fact]
    public async Task ACarrierKeyWithNoAuthoredLineIsSpokenAsFrontierWroteIt()
    {
        var line = CarrierLine("$DockingFailed_Distance:#distance=7500;");

        var spoken = await Vary(line, hasModel: false);

        Assert.Equal(line.Text, spoken?.Text);
    }

    [Theory]
    [InlineData("$DockingChatter_Neutral;")]
    [InlineData("$DockingChatter_Allied;")]
    [InlineData("$DockingChatter_Cordial;")]
    [InlineData("$DockingChatter_Friendly;")]
    [InlineData("$DockingChatter_Unfriendly;")]
    [InlineData("$STATION_docking_granted;")]
    [InlineData("$STATION_NoFireZone_entered;")]
    [InlineData("$STATION_NoFireZone_exited;")]
    public void EveryKeyTheCarrierSendsHasAnAuthoredLineForItsOwner(string key)
    {
        var line = CarrierOwnerLines.For(key, Commander);

        Assert.NotNull(line);
        Assert.Contains(Owner, line, StringComparison.Ordinal);
        Assert.DoesNotContain("pilot", line, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("$STATION_docking_granted;", "Docking granted")]
    [InlineData("$STATION_NoFireZone_entered;", "No fire zone entered")]
    [InlineData("$STATION_NoFireZone_exited;", "No fire zone exited")]
    public void AnAuthoredLineKeepsFrontiersFact(string key, string fact)
    {
        Assert.StartsWith(fact, CarrierOwnerLines.For(key, Commander), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCarriersRawKeyTravelsOnTheAnnouncement()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-09-18T09:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = "Sacred Fire BNH-T2F",
            ["Message"] = "$STATION_docking_granted;",
            ["Message_Localised"] = "Docking request granted.",
            ["Channel"] = "npc",
        });
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        var reader = new IncomingMessages
        {
            Enabled = () => true,
            IncludeNpcs = () => true,
            CarrierCallSign = "BNH-T2F",
        };

        var read = reader.Read(parsed!);

        Assert.Equal(IncomingMessages.CarrierCannedKey, read?.Key);
        Assert.Equal("$STATION_docking_granted;", read?.MessageKey);
    }
}
