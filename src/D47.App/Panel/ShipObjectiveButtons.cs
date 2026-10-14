using Avalonia.Controls;
using Avalonia.Layout;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>The button under a board objective that shows or plans its ship.</summary>
internal static class ShipObjectiveButtons
{
    /// <summary>The button for the objective, or null when it has none or the surface holds no ships.</summary>
    internal static Button? For(AdventureSurface surface, AdventureTrigger? trigger, PanelNavigator nav, PanelPrompts prompts)
    {
        if (trigger is null
            || surface.Fleet is not { } fleet
            || surface.Intend is not { } intend
            || ShipObjective.For(trigger, fleet()) is not { } shape)
        {
            return null;
        }

        var button = new Button { Content = shape.Label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => Press(shape, intend, nav, prompts);
        return button;
    }

    private static void Press(ShipObjectiveButton shape, Func<string, ShipBuild?> intend, PanelNavigator nav, PanelPrompts prompts)
    {
        switch (shape.Kind)
        {
            case ShipObjectiveKind.Plan:
                ShipsMode.Plan(prompts, shape.HullName, intend, build =>
                {
                    if (build is not null)
                    {
                        Open(nav, LoadoutPages.Ship(new FleetEntry(build, null, false)));
                    }
                });
                break;
            case ShipObjectiveKind.ShowShips:
                nav.Show(LoadoutPages.FleetRoot);
                break;
            default:
                Open(nav, LoadoutPages.Ship(shape.Entry!));
                break;
        }
    }

    private static void Open(PanelNavigator nav, NavCrumb ship)
    {
        nav.Show(LoadoutPages.FleetRoot);
        nav.GoTo(ship);
    }
}
