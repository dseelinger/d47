using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Which Biology body heads the Commander has opened or closed, by system and body. Not saved.</summary>
public sealed class BiologyHeads
{
    private readonly Dictionary<(long SystemAddress, int BodyId), bool> _open = [];

    /// <summary>True or false once the head has been pressed; null while it is as first drawn.</summary>
    public bool? Open(long systemAddress, int bodyId) =>
        _open.TryGetValue((systemAddress, bodyId), out var open) ? open : null;

    public void Set(long systemAddress, int bodyId, bool open) => _open[(systemAddress, bodyId)] = open;
}

public sealed partial class StarSystemPage
{
    public const string NothingKnown =
        "No scan of this system is on record. Fire the discovery scanner, then open the Full Spectrum Scanner to count the biological signals.";

    public const string NotMapped = "Map it with the surface scanner to name its genera.";

    /// <summary>Marks a body's head on the Biology section.</summary>
    public const string BiologyHeadClass = "biology-head";

    /// <summary>Marks a genus row on the Biology section.</summary>
    public const string GenusRowClass = "genus-row";

    /// <summary>Names a genus row's picture button.</summary>
    public const string SpeciesPictureButtonName = "SpeciesPictureButton";

    /// <summary>The most species a genus row names before "and N more".</summary>
    public const int MostSpeciesNamed = 3;

    /// <summary>The side of a genus row's picture square.</summary>
    public const double SpeciesSide = 64;

    private readonly HashSet<long> _surveyAsked = [];
    private Border? _biologyHolder;
    private string? _biologySeen;
    private string? _biologyLabelDrawn;

    /// <summary>"Biology 3", counting the bodies with biology; "Biology" when the system's biology is not known.</summary>
    public static string BiologyLabel(SystemBiologyAnswer? answer) =>
        answer is { IsKnown: true }
            ? $"Biology {answer.Bodies.Count.ToString(CultureInfo.InvariantCulture)}"
            : "Biology";

    /// <summary>The segment's label, recorded so a redraw of the section alone can tell when the count has changed.</summary>
    private string BiologySegment(StarSystemProfile profile)
    {
        AskSurveyed(profile.SystemAddress);

        return _biologyLabelDrawn = BiologyLabel(BiologyAnswer(profile));
    }

    private SystemBiologyAnswer? BiologyAnswer(StarSystemProfile profile)
    {
        if (_surface.Biology is not { } read)
        {
            return null;
        }

        var region = profile.Position is { } at ? GalacticRegions.Find(at) : null;
        var surveyed = _surface.Surveyed is { } held && held.TryGet(profile.SystemAddress, out var biology) ? biology : null;

        return read(profile.SystemAddress, region, surveyed);
    }

    private Border Biology(StarSystemProfile profile)
    {
        _biologyHolder = new Border { Child = BiologyGroups(profile, BiologyAnswer(profile)) };

        return _biologyHolder;
    }

    /// <summary>Redraws the section when the Commander's records or position change what it shows.</summary>
    private void CheckBiology()
    {
        if (_biologyHolder is null || _section != Section.Biology || _profile is not { } profile)
        {
            return;
        }

        var answer = BiologyAnswer(profile);

        if (BiologySignature(answer) != _biologySeen)
        {
            ShowBiology(profile, answer);
        }
    }

    /// <summary>Redraws the section alone, or the whole page when the segment's count has changed.</summary>
    private void ShowBiology(StarSystemProfile profile, SystemBiologyAnswer? answer)
    {
        if (_biologyHolder is null)
        {
            return;
        }

        if (BiologyLabel(answer) != _biologyLabelDrawn)
        {
            Draw();
            return;
        }

        _biologyHolder.Child = BiologyGroups(profile, answer);
        _redrawn = true;
    }

    private StackPanel BiologyGroups(StarSystemProfile profile, SystemBiologyAnswer? answer)
    {
        _biologySeen = BiologySignature(answer);

        var groups = new StackPanel { Spacing = 18 };

        AskSurveyed(profile.SystemAddress);

        if (answer is not { IsKnown: true })
        {
            groups.Children.Add(Sentence(NothingKnown, ThemeManager.WhiteKey));

            return groups;
        }

        var here = HereBody(profile.SystemAddress);
        var region = profile.Position is { } at && GalacticRegions.Find(at) is { } name ? GalacticRegions.IdOf(name) : null;

        foreach (var line in answer.Bodies)
        {
            groups.Children.Add(BodyGroup(profile, line, line.BodyId == here, region));
        }

        return groups;
    }

    /// <summary>The body the Commander is on, while the page follows them in this system.</summary>
    private int? HereBody(long systemAddress) =>
        _following && _surface.Here() is { SystemAddress: { } address, BodyId: { } body } && address == systemAddress ? body : null;

    /// <summary>What the section draws from, so a tick redraws it only when that changes.</summary>
    private string BiologySignature(SystemBiologyAnswer? answer)
    {
        if (answer is not { IsKnown: true })
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append(HereBody(answer.SystemAddress)).Append('|');

        foreach (var body in answer.Bodies)
        {
            text.Append(body.BodyId).Append(':').Append(body.Mapped).Append(':').Append(body.GeneraReported).Append(':').Append(body.ReportedValue).Append(':').Append(body.BiologicalCount).Append('[');

            foreach (var genus in body.Genera)
            {
                text.Append(genus.Genus).Append(':').Append(genus.State).Append(':').Append(genus.Taken).Append('(');

                foreach (var species in genus.Species)
                {
                    text.Append(species.Species).Append(':').Append(species.Source).Append(':').Append(species.EntryId).Append(':').Append(species.Count).Append(':').Append(species.Value).Append(',');
                }

                text.Append(')');
            }

            text.Append(']');
        }

        return text.ToString();
    }

    /// <summary>Asks Spansh once for a system's surveyed biology the holder does not have, and writes the answer to it.</summary>
    private void AskSurveyed(long address)
    {
        if (_surface.AskSurveyed is not { } ask
            || _surface.Surveyed is not { } held
            || held.TryGet(address, out _)
            || !_surveyAsked.Add(address))
        {
            return;
        }

        _ = Survey(ask, held, address);
    }

    private async Task Survey(Func<long, CancellationToken, Task<SystemBiology>> ask, SurveyedBiology held, long address)
    {
        if (await held.GetAsync(address, ask, CancellationToken.None).ConfigureAwait(false) is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_profile is not { } profile || profile.SystemAddress != address)
            {
                return;
            }

            if (_section == Section.Biology)
            {
                ShowBiology(profile, BiologyAnswer(profile));
            }
            else if (BiologyLabel(BiologyAnswer(profile)) != _biologyLabelDrawn)
            {
                Draw();
            }
        });
    }

    private StackPanel BodyGroup(StarSystemProfile profile, BodyLine line, bool here, int? region)
    {
        var address = profile.SystemAddress;
        var open = _surface.Heads.Open(address, line.BodyId)
            ?? (here || (!line.Mapped && !line.GeneraReported) || line.Genera.Any(genus => genus.State != GenusState.Done));

        var group = new StackPanel { Spacing = Gaps.Tile };
        group.Children.Add(BodyHead(profile, line, here, open));

        if (!open)
        {
            return group;
        }

        if (!line.Mapped && !line.GeneraReported)
        {
            var slab = new Border
            {
                MinHeight = TypeScale.MinimumTarget,
                Padding = new Thickness(14, 10),
                Child = RoutingKit.Ink(NotMapped, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true),
            };
            ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
            group.Children.Add(slab);
        }

        foreach (var genus in line.Genera)
        {
            group.Children.Add(GenusRow(genus, region, line.BodyName));
        }

        return group;
    }

    private Button BodyHead(StarSystemProfile profile, BodyLine line, bool here, bool open)
    {
        var address = profile.SystemAddress;

        var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, ClipToBounds = true };
        start.Children.Add(Typeset.Chrome(open ? "▼" : "►", TypeScale.Control, ThemeManager.AKey, vertical: VerticalAlignment.Center));
        start.Children.Add(Ellipsed(line.BodyName, TypeScale.Secondary, FontWeight.SemiBold, here ? ThemeManager.CyanKey : ThemeManager.WhiteKey));

        if (here)
        {
            var tag = RoutingKit.Tag("Here", ThemeManager.CyanKey);
            tag.FontSize = TypeScale.MetaSmall;
            tag.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;
            start.Children.Add(tag);
        }

        var distance = profile.Bodies.FirstOrDefault(body => body.BodyId == line.BodyId)?.DistanceToArrival;
        var figure = Mono(BodyDistanceText(distance), ThemeManager.AKey);
        figure.VerticalAlignment = VerticalAlignment.Center;
        start.Children.Add(figure);

        foreach (var text in new[] { profile.Bodies.FirstOrDefault(body => body.BodyId == line.BodyId)?.SubType, line.ReportedValue is { } worth ? $"{worth:N0} cr" : null })
        {
            if (!string.IsNullOrEmpty(text))
            {
                var extra = Mono(text, ThemeManager.AKey);
                extra.VerticalAlignment = VerticalAlignment.Center;
                start.Children.Add(extra);
            }
        }

        var (summary, ink) = HeadSummary(line);
        var end = Caption(summary, ink);
        end.VerticalAlignment = VerticalAlignment.Center;
        end.Margin = new Thickness(16, 0, 0, 0);

        var content = new DockPanel();
        DockPanel.SetDock(end, Dock.Right);
        content.Children.Add(end);
        content.Children.Add(start);

        var head = new Button
        {
            Content = content,
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        head.Classes.Add(BiologyHeadClass);
        Avalonia.Automation.AutomationProperties.SetName(head, line.BodyName);
        head.Click += (_, _) =>
        {
            _surface.Heads.Set(address, line.BodyId, !open);
            ShowBiology(profile, BiologyAnswer(profile));
        };

        return head;
    }

    /// <summary>The head's right end: "4 genera · 1 done", "2 genera · ✓ all done" or "3 biological signals · not mapped", with its ink.</summary>
    public static (string Text, string Key) HeadSummary(BodyLine line)
    {
        if (!line.Mapped && !line.GeneraReported)
        {
            return ($"{Counted(line.BiologicalCount, "biological", "signal")} · not mapped", ThemeManager.GreyKey);
        }

        var genera = line.Genera.Count;
        var counted = $"{genera.ToString(CultureInfo.InvariantCulture)} {(genera == 1 ? "genus" : "genera")}";

        return genera > 0 && line.Done == genera
            ? ($"{counted} · ✓ all done", ThemeManager.BlueKey)
            : ($"{counted} · {line.Done.ToString(CultureInfo.InvariantCulture)} done", ThemeManager.GreyKey);
    }

    /// <summary>A genus's state as its row prints it, with its ink.</summary>
    public static (string Text, string Key) GenusStateText(GenusLine genus)
    {
        var count = $"{genus.Taken.ToString(CultureInfo.InvariantCulture)} / {GenusProgress.Required.ToString(CultureInfo.InvariantCulture)}";

        return genus.State switch
        {
            GenusState.Done => ($"✓ Done · {count}", ThemeManager.BlueKey),
            GenusState.Sampling => ($"Sampling · {count}", ThemeManager.CyanKey),
            GenusState.Abandoned => ($"Abandoned · {count}", ThemeManager.WarnKey),
            _ => ("Not started", ThemeManager.GreyKey),
        };
    }

    /// <summary>"A · B · C and 2 more": at most <see cref="MostSpeciesNamed"/> species, then a count of the rest.</summary>
    public static string SpeciesText(IReadOnlyList<SpeciesLine> species)
    {
        if (species.Count == 0)
        {
            return Dash;
        }

        var named = string.Join(" · ", species.Take(MostSpeciesNamed).Select(SpeciesFigures));
        var more = species.Count - MostSpeciesNamed;

        return more > 0 ? $"{named} and {more.ToString(CultureInfo.InvariantCulture)} more" : named;
    }

    /// <summary>"Stratum Cucumisis ×7 — 16,202,800 cr": the count only above one, the value only when known.</summary>
    private static string SpeciesFigures(SpeciesLine line)
    {
        var count = line.Count > 1 ? $" ×{line.Count}" : string.Empty;
        var value = line.Value is { } worth ? $" — {worth.ToString("N0", CultureInfo.CurrentCulture)} cr" : string.Empty;

        return line.Species + count + value;
    }

    /// <summary>"One sample lost when you logged a different species."</summary>
    public static string LostText(int taken)
    {
        var count = taken switch
        {
            1 => "One sample",
            2 => "Two samples",
            _ => $"{taken.ToString(CultureInfo.InvariantCulture)} samples",
        };

        return $"{count} lost when you logged a different species.";
    }

    private void EnlargeSpecies(GenusLine genus, string bodyName, SpeciesLine species, (Avalonia.Media.Imaging.Bitmap Bitmap, bool IsBodysOwnVariant) picture)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new SpeciesPictureDialog(
            genus.Genus.ToUpperInvariant(), bodyName, species.Species, picture.Bitmap, picture.IsBodysOwnVariant, Codex.Link(genus.Genus, genus.Species));

        _ = dialog.Over(owner);
    }

    /// <summary>The REPORTED or POSSIBLE tag beside a genus row's species.</summary>
    internal static TextBlock SourceTag(SpeciesSource source)
    {
        var tag = RoutingKit.Tag(source == SpeciesSource.Reported ? "Reported" : "Possible", ThemeManager.GreyKey);
        tag.FontSize = TypeScale.MetaSmall;
        tag.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;

        return tag;
    }

    private Border GenusRow(GenusLine genus, int? region, string bodyName)
    {
        var first = genus.Species.Count > 0 ? genus.Species[0] : null;

        var square = new Border { Width = SpeciesSide, Height = SpeciesSide, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        var picture = first is null ? null : Codex.SpeciesPictureOf(first.Species, first.EntryId, region);

        if (picture is { } chosen)
        {
            square.Child = new Image { Source = chosen.Bitmap, Width = SpeciesSide, Height = SpeciesSide, Stretch = Stretch.UniformToFill };
        }

        Control pictureCell = square;

        if (picture is { } shown && first is not null)
        {
            var press = new Button
            {
                Name = SpeciesPictureButtonName,
                Content = square,
                Padding = default,
                Width = SpeciesSide,
                Height = SpeciesSide,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Avalonia.Automation.AutomationProperties.SetName(press, $"Enlarge, {first.Species}");
            press.Click += (_, _) => EnlargeSpecies(genus, bodyName, first, shown);
            pictureCell = press;
        }

        var name = Ellipsed(genus.Genus, TypeScale.ControlLarge, FontWeight.Bold, ThemeManager.WhiteKey);
        name.VerticalAlignment = VerticalAlignment.Center;

        var species = new WrapPanel { ItemSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var speciesInk = first?.Source switch
        {
            SpeciesSource.Sampled when genus.State == GenusState.Done => ThemeManager.WhiteKey,
            SpeciesSource.Sampled when genus.State == GenusState.Sampling => ThemeManager.CyanKey,
            _ => ThemeManager.GreyKey,
        };
        var named = Typeset.Chrome(SpeciesText(genus.Species), TypeScale.ControlLarge, speciesInk, weight: FontWeight.Medium, wrap: true);
        named.VerticalAlignment = VerticalAlignment.Center;
        species.Children.Add(named);

        if (first?.Source is SpeciesSource.Reported or SpeciesSource.Possible)
        {
            species.Children.Add(SourceTag(first.Source));
        }

        var described = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { species } };

        if (genus.State == GenusState.Abandoned)
        {
            described.Children.Add(RoutingKit.Ink(LostText(genus.Taken), TypeScale.Small, ThemeManager.GreyKey, wrap: true));
        }

        var (state, stateInk) = GenusStateText(genus);
        var progress = Typeset.Chrome(state, TypeScale.Control, stateInk, weight: FontWeight.SemiBold, vertical: VerticalAlignment.Center);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{SpeciesSide.ToString(CultureInfo.InvariantCulture)},150,*,Auto,Auto"),
            ColumnSpacing = 16,
        };
        Cell(grid, 0, pictureCell);
        Cell(grid, 1, name);
        Cell(grid, 2, described);
        Cell(grid, 3, progress);

        if (Codex.Link(genus.Genus, genus.Species) is { } link)
        {
            link.Margin = new Thickness(0, 0, 10, 0);
            Cell(grid, 4, link);
        }

        var row = new Border { MinHeight = SpeciesSide, Child = grid };
        row.Classes.Add(GenusRowClass);
        Avalonia.Automation.AutomationProperties.SetName(row, genus.Genus);
        ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }
}
