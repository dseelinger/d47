using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>An open choice shows each option as a 44 px tile on the mini panel with the phrase to say; pressing one runs the choose tool, and once made the option chosen is shown.</summary>
public class AnOpenStoryChoiceShowsTilesOnTheMiniPanelTests
{
    private const string Key = "the-lantern-route";

    private static readonly DateTimeOffset Accepted = new(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);

    private static Adventure Chapter() => new()
    {
        Key = Key,
        Name = "The Lantern Route",
        Source = AdventureSource.Generated,
        AcceptedAt = Accepted,
        Beats =
        [
            new AdventureBeat
            {
                Title = "The fork",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Choice },
                Steps =
                [
                    new BeatStep
                    {
                        Trigger = new AdventureTrigger { Kind = TriggerKind.Choice },
                        Options =
                        [
                            new BeatOption { Label = "fight", Steps = [new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 } }] },
                            new BeatOption { Label = "flee", Steps = [new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Collect, Count = 1 } }] },
                        ],
                    },
                ],
                Line = "Done.",
            },
        ],
    };

    [AvaloniaFact]
    public void PressingATileRunsTheChooseToolForThatOption()
    {
        var chosen = new List<int>();
        var surface = AdventureFixture.Surface() with
        {
            Choose = option =>
            {
                chosen.Add(option);
                return Task.FromResult(new ToolResult { IsError = false, Content = "Next: fly home." });
            },
        };

        Assert.Equal("", surface.Book.Write("F1", Chapter()) ?? "");
        surface.Book.CatchUp([]);
        surface.Book.Offered("F1", Key, 0, Accepted.AddMinutes(1));

        var mini = new AdventureMini(surface);
        var window = new Window { Width = 400, Height = 600, Content = mini };
        window.Show();

        var tiles = mini.GetVisualDescendants().OfType<Border>()
            .Where(border => (AutomationProperties.GetName(border) ?? "").StartsWith("choose option", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["choose option one", "choose option two"], tiles.Select(tile => AutomationProperties.GetName(tile)));
        Assert.All(tiles, tile => Assert.True(tile.Bounds.Height >= 44));

        var second = tiles[1];
        var centre = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);

        Assert.Equal([1], chosen);
    }
}
