using System.Text.Json;

namespace D47.Knowledge.Tests.ReplyFields;

/// <summary>Where one key path of a recorded reply goes.</summary>
internal abstract record ReplyMapping
{
    /// <summary>The reply's field reaches this property path of the reader's result.</summary>
    public static ReplyMapping To(string property) => new Property(property);

    /// <summary>The reply's field is left out on purpose.</summary>
    public static ReplyMapping Excluded(string reason) => new Exclusion(reason);

    internal sealed record Property(string Path) : ReplyMapping;

    internal sealed record Exclusion(string Reason) : ReplyMapping;
}

/// <summary>
/// The key paths of one recorded reply and where each goes. A key path joins object members with
/// <c>.</c> and marks array members with <c>[]</c>. A property path does the same through the reader's
/// result, marks a dictionary's values with <c>{}</c>, and names a tuple's elements <c>Item1</c>, <c>Item2</c>.
/// </summary>
/// <param name="Fixture">The recording's file name without its extension.</param>
/// <param name="Read">The reader under test.</param>
/// <param name="Dynamic">Key paths whose members are named by data; each is one leaf.</param>
/// <param name="Paths">Every key path of the recording.</param>
internal sealed record ReplyManifest(
    string Fixture,
    Func<JsonDocument, object> Read,
    IReadOnlyCollection<string> Dynamic,
    IReadOnlyDictionary<string, ReplyMapping> Paths);
