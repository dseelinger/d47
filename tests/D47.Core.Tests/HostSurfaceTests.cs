using System.Reflection;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The host surfaces are the seam between Core and the App, and a null member on one of them is not a
/// smaller app — it is a different one.
/// </summary>
public class HostSurfaceTests
{
    /// <summary>
    /// Surfaces that have no <c>Inert</c> yet, and why that is a deliberate hole rather than an
    /// oversight.
    /// </summary>
    private static readonly HashSet<string> WithoutInert =
    [
        "ListeningSurface",
        "SpeechSurface",
        "HeadsetSurface",
    ];

    private static IEnumerable<Type> Surfaces() =>
        typeof(AboutSurface).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Surface", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    [Fact]
    public void EverySurfaceEitherOffersAnInertOrIsKnownNotTo()
    {
        var missing = Surfaces()
            .Where(t => t.GetProperty("Inert", BindingFlags.Public | BindingFlags.Static) is null)
            .Select(t => t.Name)
            .Where(name => !WithoutInert.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These host surfaces offer no Inert and are not listed as knowingly without one: {string.Join(", ", missing)}. "
            + "A surface a test cannot build completely is a surface whose optional members go untested — see #78.");
    }

    [Fact]
    public void TheExemptionListNamesOnlySurfacesThatStillLackAnInert()
    {
        // The list rots the other way too: a surface that grows an Inert should leave it, or the exemption
        // silently excuses a guard that is now available.
        var stale = WithoutInert
            .Where(name => Surfaces().FirstOrDefault(t => t.Name == name) is { } type
                           && type.GetProperty("Inert", BindingFlags.Public | BindingFlags.Static) is not null)
            .ToList();

        Assert.True(
            stale.Count == 0,
            $"These are listed as having no Inert, but they do now: {string.Join(", ", stale)}. Remove them from the list.");

        var gone = WithoutInert
            .Where(name => Surfaces().All(t => t.Name != name))
            .ToList();

        Assert.True(gone.Count == 0, $"These are listed but no longer exist: {string.Join(", ", gone)}.");
    }

    [Fact]
    public void EveryInertSuppliesEveryMemberOfItsSurface()
    {
        var holes = new List<string>();

        foreach (var type in Surfaces())
        {
            if (type.GetProperty("Inert", BindingFlags.Public | BindingFlags.Static) is not { } inert)
            {
                continue;
            }

            var instance = inert.GetValue(null);

            Assert.NotNull(instance);

            foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!member.CanRead || member.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (member.GetValue(instance) is null)
                {
                    holes.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        Assert.True(
            holes.Count == 0,
            $"These members are null on their surface's Inert: {string.Join(", ", holes)}. "
            + "A null member makes its row or its feature absent, so a test binding this surface would never see it. "
            + "That is exactly how #78 shipped a build that could not start.");
    }
}
