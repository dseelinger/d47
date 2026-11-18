using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Audio;

/// <summary>A speech row in the spend ledger records the voice slot that spoke (#2553).</summary>
public class ASpeechChargeRecordsTheSlotThatSpokeTests
{
    private const string Path = @"C:\d47\data\spend.jsonl";

    private static readonly VoiceSelection Voice = new("chosen-voice");

    private sealed class Clock : IWallClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    }

    public static TheoryData<VoiceGroup, string> Slots => new()
    {
        { VoiceGroup.Aboard, "voice-aboard" },
        { VoiceGroup.Carrier, "voice-carrier" },
        { VoiceGroup.Npcs, "voice-npcs" },
        { VoiceGroup.PeopleYouKnow, "voice-people-you-know" },
        { VoiceGroup.DirectMessages, "voice-direct-messages" },
        { VoiceGroup.AnyoneInRange, "voice-anyone-in-range" },
    };

    [Theory]
    [MemberData(nameof(Slots))]
    public async Task EachSlotWritesItsOwnId(VoiceGroup slot, string expected)
    {
        var (files, spend) = Setup();

        await new MeteredTtsProvider(new FakeTtsProvider(), spend, slot)
            .SynthesizeAsync("Hello", Voice, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.Single(SpendFile.Entries(files, Path)).Purpose);
    }

    [Fact]
    public async Task ACastVoiceWritesTheCastId()
    {
        var (files, spend) = Setup();

        await new MeteredTtsProvider(new FakeTtsProvider(), spend, purpose: SpendPurposes.VoiceStoryCast)
            .SynthesizeAsync("Hello", Voice, TestContext.Current.CancellationToken);

        Assert.Equal("voice-story-cast", Assert.Single(SpendFile.Entries(files, Path)).Purpose);
    }

    [Fact]
    public async Task ADecoratorGivenNeitherWritesNoPurpose()
    {
        var (files, spend) = Setup();

        await new MeteredTtsProvider(new FakeTtsProvider(), spend)
            .SynthesizeAsync("Hello", Voice, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(SpendFile.Entries(files, Path)).Purpose);
    }

    [Fact]
    public void EverySlotAndTheCastHaveTheirOwnName()
    {
        Assert.All(VoiceGroups.All, slot =>
            Assert.Equal(slot.Name, SpendPurposes.NameOf(SpendPurposes.VoiceSlot(slot.Group))));
        Assert.Equal("Story cast", SpendPurposes.NameOf(SpendPurposes.VoiceStoryCast));
    }

    private static (MemoryFileSystem Files, SpeechSpend Spend) Setup()
    {
        var files = new MemoryFileSystem();
        var spend = new SpeechSpend();

        spend.LedgerTo(new SpendLedger(Path, files, new Clock(), NullLogger.Instance), () => new D47Settings());

        return (files, spend);
    }
}
