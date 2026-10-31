using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every two-state control in the app is drawn as Elite's checkbox (#395).</summary>
[Trait("Category", "Gate")]
public sealed class EveryTwoStateControlIsAnEliteCheckboxTests
{
    private static readonly Regex ToggleSwitchToken = new(@"\bToggleSwitch\b", RegexOptions.Compiled);

    private static readonly Regex ExactToggleButton =
        new(@"\bnew\s+ToggleButton\b|:\s*ToggleButton\b", RegexOptions.Compiled);

    [Fact]
    public void NoControlIsBuiltAsAToggleSwitchOrAToggleButton()
    {
        var root = Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App");

        var offenders =
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                               && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .SelectMany(path => File.ReadAllLines(path)
                    .Select((line, index) => (path, line, index))
                    .Where(entry => ToggleSwitchToken.IsMatch(entry.line) || ExactToggleButton.IsMatch(entry.line)))
                .Select(entry => $"{Path.GetRelativePath(root, entry.path)}:{entry.index + 1}: {entry.line.Trim()}")
                .ToList();

        Assert.True(
            offenders.Count == 0,
            "Use CheckBox instead — a ToggleSwitch, or a control whose declared or constructed type is "
            + $"exactly ToggleButton, is one the app does not draw:{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }
}
