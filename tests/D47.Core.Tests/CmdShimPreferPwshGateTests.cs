using Xunit;

namespace D47.Core.Tests;

/// <summary>Every <c>.cmd</c> shim under tools/ must probe for <c>pwsh</c> before it ever names <c>powershell</c>, so the same command runs on the same PowerShell version whichever shim invoked it.</summary>
public class CmdShimPreferPwshGateTests
{
    public static TheoryData<string> Shims =>
    [
        "rec-on",
    ];

    [Theory]
    [MemberData(nameof(Shims))]
    public void CmdShimNamesPwshBeforePowershell(string name)
    {
        var path = Path.Combine(RepositoryRoot(), "tools", $"{name}.cmd");
        Assert.True(File.Exists(path), $"Expected shim not found: {path}");

        var text = File.ReadAllText(path);
        var pwshIndex = text.IndexOf("pwsh", StringComparison.Ordinal);
        var powershellIndex = text.IndexOf("powershell", StringComparison.Ordinal);

        Assert.True(
            pwshIndex >= 0,
            $"{name}.cmd never names pwsh - it must probe for pwsh before falling back to powershell.");
        Assert.True(
            powershellIndex >= 0,
            $"{name}.cmd never names powershell - it needs a fallback for machines without pwsh.");
        Assert.True(
            pwshIndex < powershellIndex,
            $"{name}.cmd names powershell before pwsh - the probe must try pwsh first.");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}
