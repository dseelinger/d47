using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ticking;

/// <summary>
/// Pausing a subscriber loses whatever it does, so the diagnostics surface names it rather than leaving
/// it to be inferred from the log (https://github.com/dseelinger/d47/issues/58).
/// </summary>
public class APausedSubscriberIsNamedWhereSomebodyCanFindItTests
{
    private static TickLoop Broken(string name)
    {
        var loop = new TickLoop(NullLogger<TickLoop>.Instance);

        loop.Add(name, _ => throw new InvalidOperationException("broken"));

        for (var tick = 0; tick < 10; tick++)
        {
            loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100 * tick));
        }

        Assert.Equal([name], loop.Paused);

        return loop;
    }

    private static (CapabilityRegistry Registry, CapabilityDescriptor Descriptor) Diagnostics(
        TempInstall install,
        TickLoop? ticking)
    {
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance,
            loadFailed: false);

        var descriptor = DiagnosticsCapability.Create(
            install.Paths,
            new FakeVerbosityControl(),
            settings,
            TestSurface.Version,
            coverage: null,
            history: null,
            ticking);

        return (CapabilityRegistry.Build([descriptor]), descriptor);
    }

    [Fact]
    public async Task TheStatusReportNamesIt()
    {
        using var install = new TempInstall();

        var (registry, _) = Diagnostics(install, Broken("journal"));

        var result = await registry.InvokeAsync(
            "get_app_status", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("Paused after repeated failures: journal", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheStatusReportSaysNothingWhileEverythingIsRunning()
    {
        using var install = new TempInstall();

        var (registry, _) = Diagnostics(install, new TickLoop(NullLogger<TickLoop>.Instance));

        var result = await registry.InvokeAsync(
            "get_app_status", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Paused", result.Content, StringComparison.Ordinal);
    }

    /// <summary>The row exists whatever state the loop is in, and applies only while something is paused.</summary>
    [Fact]
    public void TheRowAppearsOnlyWhileSomethingIsPaused()
    {
        using var install = new TempInstall();

        var running = new TickLoop(NullLogger<TickLoop>.Instance);

        var (_, healthy) = Diagnostics(install, running);
        var (_, broken) = Diagnostics(install, Broken("macros"));

        var whenHealthy = healthy.Settings.Single(row => row.Key == DiagnosticsCapability.PausedKey);
        var whenBroken = broken.Settings.Single(row => row.Key == DiagnosticsCapability.PausedKey);

        Assert.False(whenHealthy.Applies(D47Settings.Defaults));
        Assert.True(whenBroken.Applies(D47Settings.Defaults));
        Assert.Equal("macros", whenBroken.Binding!.Read(D47Settings.Defaults));
    }
}
