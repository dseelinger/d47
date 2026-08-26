using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The three settings a floor and a ceiling arrive as (list.md Phase 54), at the store seam.
/// <para>
/// The behaviour half — what the clamp does with them and which callers take the background
/// model — is asserted where it happens: <c>EffortRangeTests</c> and <c>TurnLoopTests</c>.
/// </para>
/// </summary>
public class AFloorAndACeilingTests
{
    private static SettingsStore StoreFor(TempInstall install) =>
        new(install.Paths, NullLogger<SettingsStore>.Instance);

    /// <summary>
    /// Decision 3 of the phase: all three null means behaviour identical to today. A file
    /// written by any earlier build has none of these keys, and this is what it loads as.
    /// </summary>
    [Fact]
    public void AFileWrittenBeforeTheBoundsExistedLoadsWithNone()
    {
        using var install = new TempInstall();
        File.WriteAllText(
            install.Paths.SettingsFile,
            """{"schemaVersion":1,"llm":{"provider":"anthropic","model":"claude-opus-5"}}""");

        var settings = StoreFor(install).Load();

        Assert.Equal("claude-opus-5", settings.Llm.Model);
        Assert.Null(settings.Llm.BackgroundModel);
        Assert.Null(settings.Llm.EffortFloor);
        Assert.Null(settings.Llm.EffortCeiling);
    }

    [Fact]
    public void TheThreeSurviveARoundTrip()
    {
        using var install = new TempInstall();
        var store = StoreFor(install);

        store.Save(new D47Settings
        {
            Llm = new LlmSettings
            {
                Model = "claude-opus-5",
                BackgroundModel = "claude-haiku-4-5",
                EffortFloor = ThinkingEffort.Medium,
                EffortCeiling = ThinkingEffort.Xhigh,
            },
        });

        var reloaded = store.Load();

        Assert.Equal("claude-haiku-4-5", reloaded.Llm.BackgroundModel);
        Assert.Equal(ThinkingEffort.Medium, reloaded.Llm.EffortFloor);
        Assert.Equal(ThinkingEffort.Xhigh, reloaded.Llm.EffortCeiling);
    }

    /// <summary>
    /// The rung the phase added, through the seam that writes it down. <c>Xhigh</c> camel-cases
    /// to a single word, so a file that spelled it any other way would not load.
    /// </summary>
    [Fact]
    public void TheFifthRungIsWrittenAsOneWord()
    {
        using var install = new TempInstall();
        var store = StoreFor(install);

        store.Save(new D47Settings { Llm = new LlmSettings { EffortCeiling = ThinkingEffort.Xhigh } });

        Assert.Contains("\"xhigh\"", File.ReadAllText(install.Paths.SettingsFile), StringComparison.Ordinal);
        Assert.Equal(ThinkingEffort.Xhigh, store.Load().Llm.EffortCeiling);
    }
}
