using D47.App.Updates;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The link the update prompt opens comes out of a network response and is handed to the shell,
/// so what it is allowed to be is a security property rather than a formatting one. These cases
/// are the shapes a hostile or redirected API would reach for.
/// </summary>
public class UpdateCheckerTests
{
    [Theory]
    [InlineData("https://github.com/dseelinger/d47/releases/tag/v0.2.0")]
    [InlineData("https://github.com/dseelinger/d47/releases/latest")]
    public void ARealReleasePageIsTrusted(string url)
    {
        Assert.True(UpdateChecker.IsTrustedReleaseUrl(url));
    }

    [Theory]
    // Nothing at all, and a response missing the property.
    [InlineData(null)]
    [InlineData("")]
    // Another host, including one that only looks like the right one.
    [InlineData("https://evil.example/dseelinger/d47/releases")]
    [InlineData("https://github.com.evil.example/dseelinger/d47/releases")]
    [InlineData("https://notgithub.com/dseelinger/d47/releases")]
    // The right host, someone else's repository - or one whose name merely starts the same.
    [InlineData("https://github.com/someone/else/releases")]
    [InlineData("https://github.com/dseelinger/d47-evil/releases")]
    // The right page over the wrong scheme, and schemes that are not the web at all. The last
    // is the one that matters: UseShellExecute would run it.
    [InlineData("http://github.com/dseelinger/d47/releases")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData(@"\\attacker\share\payload.exe")]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    public void AnythingElseIsRefused(string? url)
    {
        Assert.False(UpdateChecker.IsTrustedReleaseUrl(url));
    }
}
