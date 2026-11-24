namespace D47.App.Panel;

/// <summary>How a page outside Search runs a Search › Systems search, or opens Search › System on a name.</summary>
public sealed record SystemSearchLink(Action<IReadOnlyDictionary<string, string>> Run, Action<string> OpenSystem);
