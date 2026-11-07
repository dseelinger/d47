using System.Reflection;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Tests.Knowledge;

/// <summary>Reads the shipped tables' full contents from their private fields, for audits the app has no use for.</summary>
internal static class TableContents
{
    public static IReadOnlyCollection<string> PermitSystems => Loaded(typeof(PermitSystemTable));

    public static IReadOnlyCollection<string> StarterSystems => Loaded(typeof(StarterSystemTable));

    public static int GradedMaterials => Static<System.Collections.ICollection>(typeof(MaterialGrades), "ByName").Count;

    public static IReadOnlyCollection<string> PowersWithCapitals =>
        [.. Static<System.Collections.IDictionary>(typeof(PowerCapitals), "Table").Keys.Cast<string>()];

    private static IReadOnlyCollection<string> Loaded(Type table)
    {
        var lazy = Static<object>(table, "Loaded");

        return (IReadOnlyCollection<string>)lazy.GetType().GetProperty("Value")!.GetValue(lazy)!;
    }

    private static T Static<T>(Type type, string name) =>
        (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
