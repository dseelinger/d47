using Avalonia.Controls;
using Avalonia.Layout;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>The button under an objective that shows or plans its ship, or opens the page that is the next step.</summary>
internal static class ShipObjectiveButtons
{
    /// <summary>The button for the objective, or null when it has none or the surface holds no ships.</summary>
    internal static Button? For(AdventureSurface surface, AdventureTrigger? trigger, PanelNavigator nav, PanelPrompts prompts)
    {
        if (trigger is null)
        {
            return null;
        }

        var ships = trigger.Kind == TriggerKind.Board;
        var fleet = surface.Fleet;
        var intend = surface.Intend;

        if (ships && (fleet is null || intend is null))
        {
            return null;
        }

        if (ShipObjective.For(trigger, ships ? fleet!() : []) is not { } shape
            || (PageKey(shape) is { } key && !nav.CanShow(key)))
        {
            return null;
        }

        var button = new Button { Content = shape.Label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => Press(shape, intend, nav, prompts);
        return button;
    }

    private static void Press(ShipObjectiveButton shape, Func<string, ShipBuild?>? intend, PanelNavigator nav, PanelPrompts prompts)
    {
        switch (shape.Kind)
        {
            case ShipObjectiveKind.Plan:
                ShipsMode.Plan(prompts, shape.HullName, intend!, build =>
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
            case ShipObjectiveKind.ShowEngineer:
                nav.Show(EngineersPages.DirectoryRoot);
                nav.GoTo(EngineersPages.Crumb(shape.Engineer!));
                break;
            case ShipObjectiveKind.ShowUnsold:
                nav.Show(UnsoldPage.RootKey);
                break;
            case ShipObjectiveKind.ShowSuits:
                nav.Show(OnFootMode.Root);
                break;
            default:
                Open(nav, LoadoutPages.Ship(shape.Entry!));
                break;
        }
    }

    private static string? PageKey(ShipObjectiveButton shape) =>
        shape.Kind switch
        {
            ShipObjectiveKind.ShowEngineer => EngineersPages.DirectoryRoot,
            ShipObjectiveKind.ShowUnsold => UnsoldPage.RootKey,
            ShipObjectiveKind.ShowSuits => OnFootMode.Root,
            _ => null,
        };

    private static void Open(PanelNavigator nav, NavCrumb ship)
    {
        nav.Show(LoadoutPages.FleetRoot);
        nav.GoTo(ship);
    }
}
