using D47.Core.Ships;

namespace D47.Core.Tests.Ships;

/// <summary>The fleet sentence the model is told, built from a plan service.</summary>
internal static class ShipsLine
{
    public static string? Of(ShipPlanService? ships)
    {
        if (ships is null)
        {
            return null;
        }

        var fleet = ships.Fleet();
        var planned = fleet.Count(entry => entry.Build is { Slots.Count: > 0 });
        var intended = fleet.Count(entry => !entry.IsOwned);

        if (fleet.Count == 0)
        {
            return null;
        }

        var said = $"Fleet: {fleet.Count} ship{(fleet.Count == 1 ? string.Empty : "s")}";

        if (intended > 0)
        {
            said += $", {intended} of them intended rather than owned";
        }

        said += planned > 0
            ? $", {planned} with a build planned."
            : ", none with a build planned.";

        return said;
    }
}
