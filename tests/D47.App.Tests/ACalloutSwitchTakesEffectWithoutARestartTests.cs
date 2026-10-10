using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A callout-settings change re-applies the shipped catalogue against the live <c>SettingsService</c>,
/// not a snapshot taken when the change fired (#139).
/// </summary>
[Trait("Category", "Gate")]
public class ACalloutSwitchTakesEffectWithoutARestartTests
{
    [Fact]
    public void TheSettingsChangedHandlerPassesTheLiveServiceToApply()
    {
        Assert.Contains(
            AppSource.CodeLines("ShippedCallouts.Apply("),
            line => line.Text == "ShippedCallouts.Apply(Callouts, Settings, DateTimeOffset.Now);");
        Assert.Empty(AppSource.CodeLines("ShippedCallouts.Apply(Callouts, Settings.Current"));
    }
}
