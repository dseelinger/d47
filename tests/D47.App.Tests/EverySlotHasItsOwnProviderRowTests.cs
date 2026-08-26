using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The five slot rows, on the page a Commander actually opens (list.md Phase 57).
/// <para>
/// Core can say the mapping is right and the settings binding round-trips; it cannot say the row
/// was drawn, and a settings row that exists in the registry and never reaches the surface is a
/// choice nobody can make. That has happened here before — the whole reason
/// <c>SettingsSurfaceTests</c> exists.
/// </para>
/// </summary>
public class EverySlotHasItsOwnProviderRowTests
{
    private static readonly VoiceGroupInfo[] OverTheAir =
        [.. VoiceGroups.All.Where(slot => slot.Group != VoiceGroup.Aboard)];

    [AvaloniaFact]
    public void EveryOverTheAirSlotIsOnTheSurface()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        var missing = OverTheAir
            .Where(slot => host.View.ControlFor(SpeechCapability.SlotProviderKey(slot)) is null)
            .Select(slot => slot.Name)
            .ToArray();

        Assert.True(missing.Length == 0, $"slots with no row on the page: {string.Join(", ", missing)}");

        host.Close();
    }

    /// <summary>
    /// And the ship's row is still the one it always was. Five new rows beside it would be a
    /// worse answer than six: <c>speech.provider</c> is cited from the code, from the docs and
    /// from a Commander's own settings file.
    /// </summary>
    [AvaloniaFact]
    public void TheShipsOwnRowIsUnchanged()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.NotNull(host.View.ControlFor(SpeechCapability.ProviderKey));
        Assert.Null(host.View.ControlFor("speech.provider.aboard"));

        host.Close();
    }

    /// <summary>
    /// What the row shows is what the file says, which is the fault this repository keeps
    /// finding: the display path and the speaking path disagreeing while both look fine.
    /// </summary>
    [AvaloniaFact]
    public void ARowShowsTheProviderItsSlotIsActuallyOn()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with
            {
                Speech = current.Speech with
                {
                    Provider = TtsProviderCatalog.ElevenLabsId,
                    GroupProviders = new Dictionary<string, string>
                    {
                        [VoiceGroups.AnyoneInRange.Id] = TtsProviderCatalog.EdgeId,
                    },
                },
            });

        var host = SettingsHost.Open(settings, viewState, paths);

        var shown = Shown(host, SpeechCapability.SlotProviderKey(VoiceGroups.AnyoneInRange));

        Assert.Contains("Edge", shown, StringComparison.Ordinal);

        // And the ship is on the paid one at the same time, which is the whole point of the
        // phase: the companion is worth paying for and a stranger in local is not.
        Assert.Contains("ElevenLabs", Shown(host, SpeechCapability.ProviderKey), StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>
    /// The page still draws. Every row is generated, so a row that throws on measure takes the
    /// whole surface with it rather than only itself.
    /// </summary>
    [AvaloniaFact]
    public void TheSurfaceStillRendersWithSixProvidersOnIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.NotNull(host.Window.CaptureRenderedFrame());

        host.Close();
    }

    /// <summary>What a choice row is displaying, whichever of the two controls it drew.</summary>
    private static string Shown(SettingsHost host, string key)
    {
        var control = host.View.ControlFor(key);

        Assert.NotNull(control);

        if (control is ComboBox { SelectedItem: { } selected })
        {
            return selected.ToString() ?? string.Empty;
        }

        return string.Join(
            " ",
            control.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));
    }
}
