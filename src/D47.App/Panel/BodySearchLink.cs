namespace D47.App.Panel;

/// <summary>How a page outside Search runs a Search › Bodies search: whether lookups are on, and the run.</summary>
public sealed record BodySearchLink(Func<bool> Enabled, Action<IReadOnlyDictionary<string, string>> Run);
