using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>A toggle row's checkbox shows what the setting behind it says.</summary>
[Trait("Category", "Integration")]
public class EveryCheckboxShowsItsOwnSettingTests
{
    /// <summary>
    /// The checkboxes the settings page draws for a row of its own, less the one whose caption another row
    /// on its page shares (privacy.usageCounts and egress.usagecounts are both "Usage counts").
    /// </summary>
    private const int DrawnCheckboxes = 159;

    /// <summary>
    /// Through the real drawn control, because the drawn control is the only place this fault was ever
    /// visible.
    /// </summary>
    [AvaloniaFact]
    public void ATogglesCheckboxAgreesWithItsSettingInBothDirections()
    {
        var (settings, viewState, paths, registry, _) = TestSurface.CreateFull();
        var host = SettingsHost.Open(settings, viewState, paths);

        var lying = new List<string>();
        var checkedBoxes = 0;

        foreach (var key in Rows(registry).Select(row => row.Key))
        {
            // Each row is read on its own page, the only place a Commander sees its checkbox.
            if (!host.ShowPlaceOf(key) || Unambiguous(host, key) is not CheckBox toggle)
            {
                continue;
            }

            checkedBoxes++;

            var before = settings.Read(key);

            foreach (var wanted in new[] { true, false })
            {
                // A row that refuses the value is a different subject — an AppliesWhen that is off, or one
                // the panel may not write — and not what this gate is about.
                if (settings.Apply(key, wanted ? "true" : "false", SettingsCaller.Panel).Status
                    != SettingApplyStatus.Applied)
                {
                    continue;
                }

                Dispatcher.UIThread.RunJobs();

                if (toggle.IsChecked != wanted)
                {
                    lying.Add($"{key} is {wanted} and its checkbox reads {toggle.IsChecked}");
                }
            }

            // Put back, so a switch that folds or hides other rows leaves them drawn for their own turn.
            settings.Apply(key, before ?? "false", SettingsCaller.Panel);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(DrawnCheckboxes, checkedBoxes);

        Assert.True(
            lying.Count == 0,
            "A toggle read one thing and drew another. The surface compares the row's value "
            + "ordinally against \"true\", so a Read answering bool.ToString() renders off for "
            + $"ever: {string.Join("; ", lying)}");

        host.Close();
    }

    private static Control? Unambiguous(SettingsHost host, string key)
    {
        try
        {
            return host.ControlFor(key);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// And the same claim at the source, so a new row is caught by reading the descriptor rather than
    /// only by drawing it — which also names the offending row rather than the symptom.
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
