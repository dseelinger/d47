namespace D47.App.Panel;

/// <summary>How a page outside Search runs a Search › Bodies search: the run.</summary>
public sealed record BodySearchLink(Action<IReadOnlyDictionary<string, string>> Run);
