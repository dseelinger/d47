using System.Globalization;
using D47.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Settings;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>A control redrawn from the settings never writes the settings back.</summary>
public class ARefreshWritesNothingBackTests
{
    private static SettingRow? First(SettingsService settings, Func<SettingRow, bool> pick) =>
        settings.Sections.SelectMany(section => section.Rows)
            .FirstOrDefault(row => !row.DrawnElsewhere && row.Applies(settings.Current) && pick(row));

    private static string NextNumber(SettingRow row, string? current)
    {
        var value = double.Parse(current!, CultureInfo.InvariantCulture);
        var up = value + row.Step;
        var other = row.Maximum is { } max && up > max ? value - row.Step : up;

        return other.ToString(CultureInfo.InvariantCulture);
    }

    [AvaloniaFact]
    public void ToggleChoiceNumberAndTextRowsRedrawWithoutApplying()
    {
        var (settings, _, _) = TestSurface.Create();
        var applied = 0;

        var controls = new SettingControls(
            new SettingRowHost(
                settings,
                new UserControl(),
                () => true,
                (_, _, _) =>
                {
                    applied++;
                    return true;
                },
                () => { }),
            new SettingServices(null, null, null, null, null, null, null, null, null, null, null, [], null));

        var rows = new[]
        {
            First(settings, row => row.Kind == SettingKind.Toggle),
            First(settings, row => row.Kind == SettingKind.Choice
                && !row.AllowsFreeText
                && row.ConfirmLabel is null
                && row.FetchChoiceAsync is null
                && row.Key != SpeechCapability.GuardianPresetKey
                && row.ChoicesFor(settings.Current).Count is > 1 and <= SettingsView.LongListThreshold),
            First(settings, row => row.Kind == SettingKind.Number),
            First(settings, row => row.Kind == SettingKind.Text),
        };

        Assert.All(rows, Assert.NotNull);

        foreach (var row in rows.OfType<SettingRow>())
        {
            var built = controls.Build(row, new StatusLine());
            built.Refresh();
            var current = settings.Read(row.Key);

            var other = row.Kind switch
            {
                SettingKind.Toggle => current == "true" ? "false" : "true",
                SettingKind.Choice => row.ChoicesFor(settings.Current).First(choice => choice != current),
                SettingKind.Number => NextNumber(row, current),
                _ => current == "changed" ? "again" : "changed",
            };

            var result = settings.Apply(row.Key, other, SettingsCaller.Panel);
            Assert.True(result.Ok, $"{row.Key}: {result.Message}");
            Assert.Equal(other, settings.Read(row.Key));
            built.Refresh();
        }

        Assert.Equal(0, applied);
    }
}
