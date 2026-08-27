using System.Reflection;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The host surfaces are the seam between Core and the App, and a null member on one of them is
/// not a smaller app — it is a different one (<a href="https://github.com/dseelinger/d47/issues/79">#79</a>).
/// <para>
/// <b>This is #78 generalised.</b> That defect shipped 0.76.0 and 0.76.1, neither of which could
/// start: four button-only About rows reached a Commander having never once been bound by any of
/// the 5,042 tests, because every test built <c>AboutSurface</c> with its members left null — and
/// a null member makes its row <em>absent</em> by design. An absent row is one no test can see.
/// </para>
/// <para>
/// The fix for that one row was to supply the delegates. The fix for the <em>class</em> is here: a
/// surface offers one canonical <c>Inert</c> with every member supplied, tests bind that rather
/// than building one inline, and this asserts the Inert is complete. Add a member to a surface without
/// adding it to its Inert and this fails at once, rather than at a Commander's next launch.
/// </para>
/// </summary>
public class HostSurfaceTests
{
    /// <summary>
    /// Surfaces that have no <c>Inert</c> yet, and why that is a deliberate hole rather than an
    /// oversight. Each of these carries <c>required</c> members whose inert values are real
    /// objects rather than no-op lambdas — an <c>EliteBinds</c>, a <c>SecretCheck</c>, a
    /// <c>VrState</c> — so writing one is a piece of work rather than a line, and the App test
    /// surface already builds a full one of each.
    /// <para>
    /// <b>Listing them here is the point.</b> A ninth surface added tomorrow fails this test until
    /// somebody decides which side of the line it is on, so the hole cannot grow by accident.
    /// </para>
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
        // The list rots the other way too: a surface that grows an Inert should leave it, or the
        // exemption quietly excuses a guard that is now available.
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
