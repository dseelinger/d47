using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The <c>get_bgs_tick</c> tool in each of its three states, and the phrases that reach it.</summary>
public sealed class TheLastBgsTickIsAnsweredWithoutAModelTests
{
    private static readonly DateTimeOffset Tick = new(2026, 10, 15, 16, 14, 0, TimeSpan.Zero);

    private static CapabilityDescriptor Capability(bool enabled, DateTimeOffset? held, DateTimeOffset now)
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(
            store,
            secrets,
            new D47Settings { Knowledge = new KnowledgeSettings { BgsTick = enabled } },
            NullLogger<SettingsService>.Instance);

        return GalaxyCapability.Create(null, () => null, settings, now: () => now, bgsTick: () => held);
    }

    private static async Task<string> Ask(CapabilityDescriptor capability) =>
        (await capability.Tools.Single(tool => tool.Name == "get_bgs_tick")
            .Handler(ToolArguments.Empty, TestContext.Current.CancellationToken)).Content;

    [Fact]
    public async Task ATimeHeldIsSaidInUtcWithHoursSince()
    {
        var said = await Ask(Capability(true, Tick, Tick.AddHours(23).AddMinutes(10)));

        Assert.Equal("The last BGS tick was at 16:14 UTC on 15 October, 23 hours ago.", said);
    }

    [Fact]
    public async Task UnderAnHourItIsMinutes()
    {
        var said = await Ask(Capability(true, Tick, Tick.AddMinutes(40)));

        Assert.Equal("The last BGS tick was at 16:14 UTC on 15 October, 40 minutes ago.", said);
    }

    [Fact]
    public async Task ATimeHeldInAnotherOffsetIsSaidInUtc()
    {
        var said = await Ask(Capability(true, Tick.ToOffset(TimeSpan.FromHours(2)), Tick.AddMinutes(1)));

        Assert.Equal("The last BGS tick was at 16:14 UTC on 15 October, 1 minute ago.", said);
    }

    [Fact]
    public async Task TheSettingOffSaysSoAndWhereToTurnItOn()
    {
        var said = await Ask(Capability(false, Tick, Tick.AddHours(1)));

        Assert.Equal(
            "BGS tick time is off, so I don't know when the tick was. It can be turned on under Galaxy search.",
            said);
    }

    [Fact]
    public async Task TheSettingOnWithNothingReadSaysSo()
    {
        var said = await Ask(Capability(true, null, Tick));

        Assert.Equal("I haven't read the tick time yet.", said);
    }

    [Theory]
    [InlineData("when was the tick")]
    [InlineData("when was the last tick")]
    [InlineData("when was the bgs tick")]
    [InlineData("when did the bgs tick")]
    [InlineData("has the bgs ticked")]
    [InlineData("bgs tick")]
    public void EachPhraseReachesTheToolAndNoOtherToolHasIt(string said)
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;

        var owners = registry.All
            .SelectMany(registered => registered.Descriptor.Tools)
            .Where(tool => tool.Commands.Any(command =>
                string.Equals(command.Phrase, said, StringComparison.OrdinalIgnoreCase)))
            .Select(tool => tool.Name);

        Assert.Equal(["get_bgs_tick"], owners);
        Assert.Equal("get_bgs_tick", new KeywordRouter(registry).MatchToolCommand(said)?.ToolName);
    }
}
