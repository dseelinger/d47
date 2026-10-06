using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Commander's roots land in call order, so the window and the headset both call
/// <c>EnableChecklist</c>, then <c>EnableMissions</c>, before <c>EnableStanding</c>, <c>EnableStatistics</c> and <c>EnableSession</c>.
/// </summary>
public class CommanderOpensOnTheChecklistTests
{
    [Theory]
    [InlineData("MainWindow.axaml.cs")]
    [InlineData("Headset/VrPanelSurface.cs")]
    public void TheChecklistRootIsFurnishedBeforeMissionsStandingStatisticsAndSession(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "D47.App", file));

        var order = new[] { "EnableChecklist(", "EnableMissions(", "EnableStanding(", "EnableStatistics(", "EnableSession(" }
            .Select(call => text.IndexOf("." + call, StringComparison.Ordinal))
            .ToArray();

        Assert.All(order, index => Assert.True(index >= 0, $"A Commander root is not furnished in {file}."));
        Assert.Equal(order.Order(), order);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
