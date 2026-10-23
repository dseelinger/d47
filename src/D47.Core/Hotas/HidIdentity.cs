namespace D47.Core.Hotas;

/// <summary>A controller interface's HID device path and button count.</summary>
public readonly record struct HidInterface(string Path, int ButtonCount);

/// <summary>The identity a stick binding is stored under, built from HID device paths only.</summary>
public static class HidIdentity
{
    /// <summary>One identity per entry, in order; interfaces whose identities collide add the path's third segment.</summary>
    public static IReadOnlyList<string> Build(IReadOnlyList<HidInterface> interfaces)
    {
        var bases = interfaces.Select(Base).ToList();
        var result = new string[interfaces.Count];

        for (var i = 0; i < interfaces.Count; i++)
        {
            var collides = bases.Where((other, j) => j != i && other == bases[i]).Any();
            result[i] = collides ? $"{bases[i]}:{Segment(interfaces[i].Path, 2)}" : bases[i];
        }

        return result;
    }

    private static string Base(HidInterface hid) => $"hid:{Segment(hid.Path, 1)}:b{hid.ButtonCount}";

    private static string Segment(string path, int index)
    {
        var parts = path.Split('#');
        var segment = index < parts.Length ? parts[index] : "";
        return segment.ToUpperInvariant();
    }
}
