using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>The three draft commands are the Commander's, reachable by phrase only while a draft exists.</summary>
public sealed class ADraftIsActedOnByVoiceTests
{
    private static readonly (string Said, string Tool)[] Commands =
    [
        ("change the adventure", AdventureCapability.ChangeDraftTool),
        ("accept the adventure", AdventureCapability.AcceptDraftTool),
        ("reject the adventure", AdventureCapability.RejectDraftTool),
    ];

    private static CapabilityRegistry Registry(AdventureCapability.AdventureDesk desk) =>
        CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);

    [Fact]
    public void EachPhraseReachesItsToolWhileADraftExists()
    {
        var router = new KeywordRouter(Registry(new AdventureCapability.AdventureDesk { HasDraft = () => true }));

        foreach (var (said, tool) in Commands)
        {
            Assert.Equal(tool, router.MatchToolCommand(said)?.ToolName);
        }
    }

    [Fact]
    public void NoPhraseMatchesWhenThereIsNoDraft()
    {
        var router = new KeywordRouter(Registry(new AdventureCapability.AdventureDesk()));

        foreach (var (said, _) in Commands)
        {
            Assert.Null(router.MatchToolCommand(said));
        }
    }

    [Fact]
    public void NoneOfThemIsAdvertisedToTheModel()
    {
        var advertised = ToolSurface.All(Registry(new AdventureCapability.AdventureDesk { HasDraft = () => true }))
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (_, tool) in Commands)
        {
            Assert.DoesNotContain(tool, advertised);
        }
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderIsNot()
    {
        var acted = new List<string>();
        var registry = Registry(new AdventureCapability.AdventureDesk
        {
            HasDraft = () => true,
            Change = () => { acted.Add("change"); return null; },
            Accept = () => { acted.Add("accept"); return null; },
            Reject = () => { acted.Add("reject"); return null; },
        });

        foreach (var (_, tool) in Commands)
        {
            var refused = await registry.InvokeAsync(tool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(refused.IsError);
        }

        Assert.Empty(acted);

        foreach (var (_, tool) in Commands)
        {
            var result = await registry.InvokeAsync(tool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result.IsError);
        }

        Assert.Equal(["change", "accept", "reject"], acted);
    }

    [Fact]
    public async Task ARefusalFromTheDeskIsWhatTheCommanderHears()
    {
        var registry = Registry(new AdventureCapability.AdventureDesk
        {
            HasDraft = () => true,
            Accept = () => "There is more than one draft adventure. Open the one you mean.",
        });

        var result = await registry.InvokeAsync(
            AdventureCapability.AcceptDraftTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("There is more than one draft adventure. Open the one you mean.", result.Content);
    }
}
