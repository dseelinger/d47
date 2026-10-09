using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The Test row in Provider and model probes the provider and records the result.</summary>
[Trait("Category", "Integration")]
public class TheTestButtonMarksTheModelTests
{
    [Fact]
    public async Task AWorkingProbeMarksTheModelAvailable()
    {
        var (row, availability) = await Press(SecretCheck.Works("Anthropic answered — 3 models."), failFirst: true);

        Assert.Equal(LlmAvailability.Available, availability.Current);
        Assert.StartsWith("Available", row.Binding!.Read!(new D47Settings()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedKeyMarksTheModelNotConfigured()
    {
        var (row, availability) = await Press(SecretCheck.Rejected("The key was refused."), failFirst: false);

        Assert.Equal(LlmAvailability.NotConfigured, availability.Current);
        Assert.Contains("The key was refused.", row.Binding!.Read!(new D47Settings()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableServiceMarksTheModelTemporarilyUnavailable()
    {
        var (_, availability) = await Press(SecretCheck.Unreachable("No answer."), failFirst: false);

        Assert.Equal(LlmAvailability.TemporarilyUnavailable, availability.Current);
        Assert.Equal("No answer.", availability.Reason);
    }

    private static async Task<(SettingRow Row, LlmAvailabilityState Availability)> Press(
        SecretCheck check,
        bool failFirst)
    {
        using var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        var availability = new LlmAvailabilityState(providerConfigured: true);

        if (failFirst)
        {
            availability.MarkFailed("Earlier failure.", transient: true);
        }

        var row = ConversationCapability.Create(
                settings,
                availability,
                new SpendTracker(),
                new TurnCancellation(NullLogger<TurnCancellation>.Instance),
                () => { },
                verifyKey: (_, _) => Task.FromResult(check)).Settings
            .Single(candidate => candidate.Key == ConversationCapability.TestKey);

        await row.PressAsync!(new Progress<double>(), CancellationToken.None);

        return (row, availability);
    }
}
