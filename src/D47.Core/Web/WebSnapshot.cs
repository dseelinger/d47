using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Core.Web;

/// <summary>
/// One Commander's state as the web app draws it: copies of the game state and of d47's own records, with
/// no reference into either. A section whose source is empty or unknown is null.
/// </summary>
public sealed record WebSnapshot
{
    /// <summary>
    /// The document's format. A field may be added without changing it; it rises only when a field is
    /// removed or changes meaning.
    /// </summary>
    public const int CurrentFormat = 1;

    /// <summary>camelCase, null fields left out. Shared; never change it.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Format { get; init; } = CurrentFormat;

    public required string FrontierId { get; init; }

    public required string Name { get; init; }

    /// <summary>When the document was built.</summary>
    public required DateTimeOffset TakenAt { get; init; }

    /// <summary>The panel theme's id.</summary>
    public required string Theme { get; init; }

    /// <summary>Elite's HUD colour matrix, row by row, when the theme is the Commander's own HUD colours.</summary>
    public IReadOnlyList<double>? HudMatrix { get; init; }

    public WebProgress? Progress { get; init; }

    public WebShips? Ships { get; init; }

    public WebCarrier? Carrier { get; init; }

    public IReadOnlyList<WebEngineer>? Engineers { get; init; }

    public IReadOnlyList<WebMaterial>? Materials { get; init; }

    public IReadOnlyList<WebMission>? Missions { get; init; }

    public WebChecklist? Checklist { get; init; }

    public IReadOnlyList<WebWatch>? Watches { get; init; }

    public IReadOnlyList<WebRoutePlan>? RoutePlans { get; init; }
}

public sealed record WebProgress
{
    public long? Balance { get; init; }

    public WebLocation? Location { get; init; }

    /// <summary>The careers, without the navies.</summary>
    public IReadOnlyList<WebRank>? Ranks { get; init; }

    /// <summary>Empire and Federation.</summary>
    public IReadOnlyList<WebRank>? Navy { get; init; }

    /// <summary>Empire, Federation, Alliance and Independent, where read.</summary>
    public IReadOnlyList<WebReputation>? Reputation { get; init; }
}

public sealed record WebLocation
{
    public required string StarSystem { get; init; }

    /// <summary>Set only while docked.</summary>
    public string? StationName { get; init; }

    public string? Mode { get; init; }

    public string? Body { get; init; }

    public string? SystemFaction { get; init; }

    public string? ControllingPower { get; init; }

    public string? PowerplayState { get; init; }
}

public sealed record WebRank(string Career, int Rank, int? Percent);

/// <summary>A superpower and the Commander's reputation with it, -100 to 100.</summary>
public sealed record WebReputation(string Superpower, double Reputation);

public sealed record WebShips
{
    /// <summary>The stored ships, each as its list line.</summary>
    public IReadOnlyList<WebStoredShip>? Fleet { get; init; }

    public WebFlownShip? Flown { get; init; }
}

/// <summary>A stored ship; no system where nothing has reported one, as for a ship in transit.</summary>
public sealed record WebStoredShip(string Type, string? Name, string? StarSystem, string? StationName, bool InTransit, long? Value);

public sealed record WebFlownShip
{
    public string? TypeName { get; init; }

    public string? Name { get; init; }

    public string? Ident { get; init; }

    public long? Rebuy { get; init; }

    public double? MaxJumpRange { get; init; }

    public int? CargoCapacity { get; init; }

    public double? FuelCapacity { get; init; }

    public double? UnladenMass { get; init; }

    /// <summary>Hull and modules.</summary>
    public long? TotalValue { get; init; }

    public int? HullHealth { get; init; }

    public IReadOnlyList<WebModule>? Modules { get; init; }
}

public sealed record WebModule(string Slot, string Item, string? Blueprint, int? BlueprintLevel, string? Experimental, int? Health);

public sealed record WebCarrier
{
    public string? CallSign { get; init; }

    public string? Name { get; init; }

    public long? Balance { get; init; }

    public string? StarSystem { get; init; }

    public string? DestinationSystem { get; init; }

    public DateTimeOffset? DepartureTime { get; init; }

    public double? JumpRange { get; init; }

    public double? MaxJumpRange { get; init; }

    public int? Capacity { get; init; }

    public int? FreeSpace { get; init; }

    public int? CargoTonnes { get; init; }

    public string? DockingAccess { get; init; }

    public long? WeeklyUpkeep { get; init; }

    public int? FuelLevel { get; init; }

    public int? TritiumInHold { get; init; }

    public IReadOnlyList<WebCarrierService>? Services { get; init; }
}

public sealed record WebCarrierService(string Role, string Name, bool Activated, bool Enabled);

public sealed record WebEngineer
{
    public required string Name { get; init; }

    public string? Station { get; init; }

    public string? System { get; init; }

    public IReadOnlyList<WebSpeciality>? Specialities { get; init; }

    public string? UnlockCost { get; init; }

    public IReadOnlyList<string>? ReferredBy { get; init; }

    /// <summary>WithinReach, Unlocked or Locked.</summary>
    public required string Reach { get; init; }

    public WebEngineerStanding? Standing { get; init; }

    /// <summary>From the system in <see cref="WebProgress.Location"/>.</summary>
    public double? LightYears { get; init; }

    public int? Jumps { get; init; }

    public IReadOnlyList<WebPrerequisite>? Prerequisites { get; init; }
}

/// <summary>A speciality and its top grade; no grade where the game grades none.</summary>
public sealed record WebSpeciality(string Kind, int? Grade);

/// <summary>Known, Invited or Unlocked, and the grade and percent once unlocked.</summary>
public sealed record WebEngineerStanding(string Progress, int? Rank, int? RankProgress);

/// <summary>An unlock prerequisite: Met, InProgress, Unknown or NotMet, and the percent while in progress.</summary>
public sealed record WebPrerequisite(string Text, string State, int? Percent);

public sealed record WebMaterial(string DisplayName, string Category, int Count, int? Grade);

public sealed record WebMission
{
    public required string Title { get; init; }

    public string? Faction { get; init; }

    public string? TargetFaction { get; init; }

    public string? DestinationStation { get; init; }

    public string? DestinationSystem { get; init; }

    public DateTimeOffset? Expiry { get; init; }

    public long? Reward { get; init; }

    public WebMissionCargo? Cargo { get; init; }

    public int? PassengerCount { get; init; }
}

public sealed record WebMissionCargo(int Collected, int Delivered, int Total);

public sealed record WebChecklist
{
    public IReadOnlyList<WebChecklistList>? Lists { get; init; }

    public IReadOnlyList<WebGoal>? Goals { get; init; }
}

public sealed record WebChecklistList
{
    public required string Name { get; init; }

    /// <summary>Yours, Ships, Systems or SuitsAndWeapons.</summary>
    public required string Home { get; init; }

    public int Open { get; init; }

    public int Done { get; init; }

    public string? Next { get; init; }

    /// <summary>The list is the flown ship's, or the system the Commander is in.</summary>
    public bool IsHere { get; init; }

    public IReadOnlyList<WebChecklistItem>? Items { get; init; }
}

/// <summary>A line as the Checklist page words it, and its state.</summary>
public sealed record WebChecklistItem(string Text, string State);

public sealed record WebGoal
{
    public required string Name { get; init; }

    public long? Have { get; init; }

    public long? Need { get; init; }

    public double? Fraction { get; init; }

    public double? Rung { get; init; }

    public string? NextRank { get; init; }

    public bool IsDone { get; init; }
}

public sealed record WebWatch
{
    public required string Name { get; init; }

    /// <summary>Stock, NewCommunityGoal or NextSystem.</summary>
    public required string Kind { get; init; }

    public bool Enabled { get; init; }

    public string? Station { get; init; }

    public string? System { get; init; }

    public string? Commodity { get; init; }

    public string? Comparison { get; init; }

    public int? Threshold { get; init; }

    /// <summary>When a next-system watch last fired.</summary>
    public DateTimeOffset? FiredAt { get; init; }

    /// <summary>The last firing as the Watches page words it; null when it has not fired.</summary>
    public string? Fired { get; init; }

    public string? Service { get; init; }

    public string? ServiceKind { get; init; }

    public string? MinPad { get; init; }
}

public sealed record WebRoutePlan
{
    /// <summary>Jump, Riches, Trade, Exobiology, Carrier or Outfitting.</summary>
    public required string Kind { get; init; }

    public string? Headline { get; init; }

    public DateTimeOffset PlottedAt { get; init; }

    /// <summary>The furthest stop reached, as an index into the plan's stops.</summary>
    public int? Reached { get; init; }

    public WebJumpRoute? Jump { get; init; }
}

public sealed record WebJumpRoute(string Origin, string Destination, double TotalDistance, int TotalJumps, IReadOnlyList<WebWaypoint> Waypoints);

public sealed record WebWaypoint(string System, int Jumps, double? DistanceLeft, bool IsNeutron);
