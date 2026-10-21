using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Controls;

/// <summary>The open Powerplay goal's two sections: the benefits held at the Commander's rank and those still to come.</summary>
public static class PowerplayBenefitsView
{
    public static Control Build(PowerplayBenefits benefits)
    {
        ArgumentNullException.ThrowIfNull(benefits);

        var root = new StackPanel { Spacing = 8 };
        root.Children.Add(Held(benefits));

        if (benefits.ToCome.Count > 0)
        {
            root.Children.Add(ToCome(benefits));
        }

        return root;
    }

    private static Control Held(PowerplayBenefits benefits)
    {
        var rows = new StackPanel { Spacing = 2 };
        rows.Children.Add(Heading($"Held at rank {benefits.Rank}"));

        foreach (var perk in benefits.Perks)
        {
            rows.Children.Add(Row(perk.Name, perk.Value, $"since rank {perk.Since}", ThemeManager.AKey));
        }

        if (benefits.Modules.Count > 0)
        {
            var names = string.Join(", ", benefits.Modules.Select(m => m.Name));
            rows.Children.Add(Row("modules unlocked", benefits.Modules.Count.ToString(CultureInfo.InvariantCulture), names, ThemeManager.AKey));
        }
        else if (benefits.FirstModule is { } first)
        {
            rows.Children.Add(Row("no module yet", string.Empty, $"{first.Name} at rank {first.Rank}", ThemeManager.GreyKey));
        }

        if (benefits.CarePackageRanks.Count > 0)
        {
            var count = benefits.CarePackageRanks.Count;
            rows.Children.Add(Row(
                count == 1 ? "mini care package" : $"{count} mini care packages",
                string.Empty,
                (count == 1 ? "rank " : "ranks ") + string.Join(", ", benefits.CarePackageRanks),
                ThemeManager.AKey));
        }

        if (benefits.HasDecal)
        {
            rows.Children.Add(Row("rank decal", string.Empty, string.Empty, ThemeManager.AKey));
        }

        return Slab(rows);
    }

    private static Control ToCome(PowerplayBenefits benefits)
    {
        var rows = new StackPanel { Spacing = 2 };
        rows.Children.Add(Heading("Still to come"));
        rows.Children.Add(Note($"Perks count in {benefits.Power}'s territory. Every rank past {PowerplayRanks.LastTabulatedRank} gives a full care package."));

        foreach (var chapter in benefits.ToCome)
        {
            var title = $"{chapter.Label} · {chapter.Merits:N0} merits · {GoalTrackBar.Percent(chapter.Share)}"
                + (chapter.IsCurrent ? " · now" : string.Empty);

            var head = Text(title.ToUpperInvariant(), TypeScale.MetaSmall, chapter.IsCurrent ? ThemeManager.CyanKey : ThemeManager.GreyKey, chrome: true);
            head.Margin = new Thickness(0, 6, 0, 2);
            rows.Children.Add(head);

            foreach (var row in chapter.Rows)
            {
                rows.Children.Add(ChangeRow(row));
            }

            if (chapter.CarePackages is { } care)
            {
                rows.Children.Add(Note(care));
            }
        }

        return Slab(rows);
    }

    private static Control ChangeRow(BenefitRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*,Auto"), Margin = new Thickness(0, 1) };

        var rank = Text(row.Rank.ToString(CultureInfo.InvariantCulture), TypeScale.Small, ThemeManager.WhiteKey, mono: true);
        var name = Text(row.Name, TypeScale.Small, ThemeManager.WhiteKey);
        Grid.SetColumn(name, 1);

        var change = new TextBlock { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Small, TextWrapping = TextWrapping.NoWrap };

        if (row.IsModule || row.Old is null)
        {
            change.Text = row.Change;
            Themed(change, TextBlock.ForegroundProperty, row.IsModule ? ThemeManager.WhiteKey : ThemeManager.AKey);
        }
        else
        {
            change.Inlines = new Avalonia.Controls.Documents.InlineCollection
            {
                Colored(row.Old, ThemeManager.GreyKey),
                Colored(" → ", ThemeManager.GreyKey),
                Colored(row.New!, ThemeManager.AKey),
            };
        }

        Grid.SetColumn(change, 2);
        grid.Children.Add(rank);
        grid.Children.Add(name);
        grid.Children.Add(change);

        return grid;
    }

    private static Avalonia.Controls.Documents.Run Colored(string text, string key)
    {
        var run = new Avalonia.Controls.Documents.Run(text);
        run[!Avalonia.Controls.Documents.TextElement.ForegroundProperty] = new DynamicResourceExtension(key);

        return run;
    }

    private static Control Row(string name, string value, string detail, string valueKey)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 1) };

        var left = new StackPanel { Spacing = 0 };
        left.Children.Add(Text(name, TypeScale.Small, ThemeManager.WhiteKey));

        if (detail.Length > 0)
        {
            left.Children.Add(Text(detail, TypeScale.Meta, ThemeManager.GreyKey));
        }

        var figure = Text(value, TypeScale.Small, valueKey, mono: true);
        Grid.SetColumn(figure, 1);
        grid.Children.Add(left);
        grid.Children.Add(figure);

        return grid;
    }

    private static TextBlock Heading(string text) => Text(text.ToUpperInvariant(), TypeScale.Meta, ThemeManager.CyanKey, chrome: true);

    private static TextBlock Note(string text) => Text(text, TypeScale.Meta, ThemeManager.GreyKey);

    private static TextBlock Text(string text, double size, string key, bool mono = false, bool chrome = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };

        if (mono)
        {
            block.FontFamily = new FontFamily(Fonts.MonoFamily);
        }
        else if (chrome)
        {
            block.FontFamily = new FontFamily(Fonts.ChromeFamily);
            block.FontWeight = FontWeight.Medium;
            block.LetterSpacing = size * Fonts.ChromeTracking;
        }

        Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static Border Slab(Control content)
    {
        var slab = new Border { Child = content, Padding = new Thickness(10, 8) };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
