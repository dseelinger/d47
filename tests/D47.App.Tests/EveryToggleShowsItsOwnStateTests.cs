using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A toggle row shows what the setting behind it says (<a
/// href="https://github.com/dseelinger/d47/issues/37">#37</a>).
/// <para>
/// <b>Reported as "Show the overlay is always toggled off" — including while the overlay was on
/// screen.</b> <c>SettingsView.BuildToggle</c> reads the row's value and compares it ordinally
/// against the lowercase word <c>"true"</c>, and the overlay row answered
/// <c>Enabled.ToString()</c>, which is <c>"True"</c>.
/// </para>
/// <para>
/// <b>The write side is what hid it.</b> <c>bool.TryParse</c> is case-insensitive, so flipping the
/// switch set the setting, the overlay appeared, and the switch that had just been flipped read
/// off. Nothing failed; one control lied.
/// </para>
/// <para>
/// So this is a gate rather than a test about one row: spelling <c>"true"</c> by hand was a
/// convention every other capability happened to follow, and a convention with nothing enforcing
/// it is a bug waiting for the next toggle.
/// </para>
/// </summary>
public class EveryToggleShowsItsOwnStateTests
{
    /// <summary>
    /// Through the <b>real drawn control</b>, because the drawn control is the only place this
    /// fault was ever visible.
    /// </summary>
    [AvaloniaFact]
    public void ATogglesSwitchAgreesWithItsSettingInBothDirections()
    {
        var (settings, viewState, paths, registry, _) = TestSurface.CreateFull();
        var host = SettingsHost.Open(settings, viewState, paths);

        var toggles = Rows(registry)
            .Select(row => (row.Key, Switch: host.View.ControlFor(row.Key) as ToggleSwitch))
            .Where(found => found.Switch is not null)
            .ToList();

        Assert.NotEmpty(toggles);

        var lying = new List<string>();

        foreach (var (key, toggle) in toggles)
        {
            foreach (var wanted in new[] { true, false })
            {
                // A row that refuses the value is a different subject — an AppliesWhen that is
                // off, or one the panel may not write — and not what this gate is about.
                if (settings.Apply(key, wanted ? "true" : "false", SettingsCaller.Panel).Status
                    != SettingApplyStatus.Applied)
                {
                    continue;
                }

                Dispatcher.UIThread.RunJobs();

                if (toggle!.IsChecked != wanted)
                {
                    lying.Add($"{key} is {wanted} and its switch reads {toggle.IsChecked}");
                }
            }
        }

        Assert.True(
            lying.Count == 0,
            "A toggle read one thing and drew another. The surface compares the row's value "
            + "ordinally against \"true\", so a Read answering bool.ToString() renders off for "
            + $"ever: {string.Join("; ", lying)}");

        host.Close();
    }

    /// <summary>
    /// And the same claim at the source, so a new row is caught by reading the descriptor rather
    /// than only by drawing it — which also names the offending row rather than the symptom.
    /// </summary>
    [Fact]
    public void EveryToggleRowReadsOneOfExactlyTwoWords()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();

        var wrong = Rows(registry)
            .Where(row => row.Binding is not null)
            .Select(row => (row.Key, Value: row.Binding!.Read(new D47Settings())))
            .Where(read => read.Value is not ("true" or "false"))
            .Select(read => $"{read.Key} reads \"{read.Value}\"")
            .ToList();

        Assert.True(
            wrong.Count == 0,
            $"Toggle rows answering something other than \"true\" or \"false\": {string.Join("; ", wrong)}");
    }

    private static IEnumerable<SettingRow> Rows(CapabilityRegistry registry) =>
        registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Where(row => row.Kind == SettingKind.Toggle);
}
