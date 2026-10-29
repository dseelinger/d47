namespace D47.App.Coverage;

/// <summary>What the Features page shows for a tool or settings row: its name, capability, description and exercise steps.</summary>
public sealed record ToolExercise(string Name, string CapabilityName, string Description, string? Steps);
