using System.Text.Json;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class ReplyKeyPaths
{
    /// <summary>
    /// Every key path of the reply, each with whether some value at it is not null, false, zero or empty.
    /// </summary>
    public static Dictionary<string, bool> Of(JsonElement root, IReadOnlyCollection<string> dynamicPaths)
    {
        var found = new Dictionary<string, bool>(StringComparer.Ordinal);
        Walk(root, string.Empty, dynamicPaths, found);
        return found;
    }

    private static void Walk(JsonElement value, string path, IReadOnlyCollection<string> dynamicPaths, Dictionary<string, bool> found)
    {
        if (dynamicPaths.Contains(path))
        {
            Note(found, path, Present(value));
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in value.EnumerateObject())
                {
                    Walk(member.Value, path.Length == 0 ? member.Name : $"{path}.{member.Name}", dynamicPaths, found);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    Walk(item, $"{path}[]", dynamicPaths, found);
                }

                break;
            default:
                Note(found, path, Present(value));
                break;
        }
    }

    private static void Note(Dictionary<string, bool> found, string path, bool present) =>
        found[path] = present || found.GetValueOrDefault(path);

    private static bool Present(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => value.GetDouble() != 0,
        JsonValueKind.String => value.GetString() is { Length: > 0 },
        JsonValueKind.Object => value.EnumerateObject().Any(member => Present(member.Value)),
        JsonValueKind.Array => value.GetArrayLength() > 0,
        _ => false,
    };
}
