using System.Collections;
using System.Reflection;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class ReplyModelPaths
{
    /// <summary>Why the path does not lead anywhere in the type of <paramref name="model"/>; null when it does.</summary>
    public static string? Unresolved(object model, string path)
    {
        var type = model.GetType();

        foreach (var segment in path.Split('.'))
        {
            var (name, suffix) = Split(segment);

            if (name.Length > 0)
            {
                if (Member(type, name) is not { } member)
                {
                    return $"{type.Name} has no member {name}";
                }

                type = MemberType(member);
            }

            if (suffix.Length > 0)
            {
                var inner = suffix == "[]" ? ElementType(type) : ValueType(type);

                if (inner is null)
                {
                    return $"{type.Name} has no {(suffix == "[]" ? "elements" : "dictionary values")}";
                }

                type = inner;
            }
        }

        return null;
    }

    /// <summary>Whether some value at the path is not null, false, zero or empty.</summary>
    public static bool AnyPresent(object model, string path)
    {
        IEnumerable<object?> current = [model];

        foreach (var segment in path.Split('.'))
        {
            var (name, suffix) = Split(segment);
            var step = name.Length > 0
                ? current.Where(item => item is not null).Select(item => MemberValue(item!, name))
                : current;

            current = suffix switch
            {
                "[]" => step.SelectMany(item => item is IEnumerable list and not string ? list.Cast<object?>() : []),
                "{}" => step.SelectMany(item => item is IEnumerable list and not string ? list.Cast<object?>().Select(Value) : []),
                _ => step,
            };
        }

        return current.Any(Present);
    }

    private static (string Name, string Suffix) Split(string segment) =>
        segment.EndsWith("[]", StringComparison.Ordinal) ? (segment[..^2], "[]")
        : segment.EndsWith("{}", StringComparison.Ordinal) ? (segment[..^2], "{}")
        : (segment, string.Empty);

    private static MemberInfo? Member(Type type, string name) =>
        (MemberInfo?)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
        ?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance);

    private static Type MemberType(MemberInfo member)
    {
        var type = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static object? MemberValue(object item, string name) => Member(item.GetType(), name) switch
    {
        PropertyInfo property => property.GetValue(item),
        FieldInfo field => field.GetValue(item),
        _ => null,
    };

    private static Type? ElementType(Type type) =>
        type == typeof(string) ? null
        : type.IsArray ? type.GetElementType()
        : Generic(type, typeof(IEnumerable<>))?.GetGenericArguments()[0];

    private static Type? ValueType(Type type) =>
        (Generic(type, typeof(IReadOnlyDictionary<,>)) ?? Generic(type, typeof(IDictionary<,>)))?.GetGenericArguments()[1];

    private static Type? Generic(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == definition);

    private static object? Value(object? entry) =>
        entry?.GetType().GetProperty("Value")?.GetValue(entry);

    private static bool Present(object? value) => value switch
    {
        null => false,
        string text => text.Length > 0,
        bool flag => flag,
        DateTimeOffset moment => moment != default,
        Enum choice => Convert.ToInt64(choice) != 0,
        IEnumerable list => list.Cast<object?>().Any(),
        IConvertible number when value.GetType().IsPrimitive || value is decimal => Convert.ToDouble(number) != 0,
        { } other when other.GetType().IsValueType => !other.Equals(Activator.CreateInstance(other.GetType())),
        _ => true,
    };
}
