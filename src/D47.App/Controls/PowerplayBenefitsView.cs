using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using static D47.App.Theming.ThemedText;
using D47.Core.Knowledge;

namespace D47.App.Controls;

/// <summary>The open Powerplay goal's two sections: the benefits held at the Commander's rank and those still to come.</summary>
public static class PowerplayBenefitsView
{
    public const double ColumnMinimum = 340;

    private const double Gap = 16;

    private const int ChaptersShown = 3;

    /// <summary>
    /// Held and Still to come side by side, or stacked where the width cannot give each <see cref="ColumnMinimum"/>.
    /// <paramref name="showAll"/> opens every chapter; <paramref name="onShowAll"/> runs when its button is pressed.
    /// </summary>
    public static Control Build(PowerplayBenefits benefits, bool showAll = false, Action? onShowAll = null)
    {
        ArgumentNullException.ThrowIfNull(benefits);

        var held = Held(benefits);
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
        root.Children.Add(held);

        if (benefits.ToCome.Count == 0)
        {
            return root;
        }

        var ahead = ToCome(benefits, showAll, onShowAll);
        root.Children.Add(ahead);

        root.SizeChanged += (_, args) =>
        {
            var wide = args.NewSize.Width >= ColumnMinimum * 2 + Gap;

            root.ColumnDefinitions = wide ? new ColumnDefinitions($"*,{Gap},*") : new ColumnDefinitions("*");
            root.RowDefinitions = wide ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
            Grid.SetColumn(ahead, wide ? 2 : 0);
            Grid.SetRow(ahead, wide ? 0 : 1);
            ahead.Margin = wide ? new Thickness(0) : new Thickness(0, Gap, 0, 0);
        };

        return root;
    }

    private static Control Held(PowerplayBenefits benefits)
    {
        var rows = new StackPanel { Spacing = 0 };
        rows.Children.Add(Heading($"Held at rank {benefits.Rank}"));
        rows.Children.Add(Note($"Perks count in {benefits.Power}'s territory."));

        foreach (var perk in benefits.Perks)
        {
            rows.Children.Add(Row(perk.Name, perk.Value, $"since rank {perk.Since}", ThemeManager.AKey, ThemeManager.WhiteKey));
        }

        if (benefits.Modules.Count > 0)
        {
            var names = string.Join(", ", benefits.Modules.Select(m => m.Name));
            rows.Children.Add(Row("modules unlocked", benefits.Modules.Count.ToString(CultureInfo.InvariantCulture), names, ThemeManager.AKey, ThemeManager.WhiteKey));
        }
        else if (benefits.FirstModule is { } first)
        {
            rows.Children.Add(Row("no module yet", "—", $"{first.Name} at rank {first.Rank}", ThemeManager.GreyKey, ThemeManager.GreyKey));
        }

        if (benefits.CarePackageRanks.Count > 0)
        {
            var count = benefits.CarePackageRanks.Count;
            rows.Children.Add(Row(
                count == 1 ? "mini care package" : $"{count} mini care packages",
                string.Empty,
                (count == 1 ? "rank " : "ranks ") + string.Join(", ", benefits.CarePackageRanks),
                ThemeManager.AKey,
                ThemeManager.WhiteKey));
        }

        if (benefits.HasDecal)
        {
            rows.Children.Add(Row("rank decal", "✓ HELD", string.Empty, ThemeManager.BlueKey, ThemeManager.WhiteKey));
        }

        return rows;
    }

    private static Control ToCome(PowerplayBenefits benefits, bool showAll, Action? onShowAll)
    {
        var rows = new StackPanel { Spacing = 0 };
        rows.Children.Add(Heading("Still to come"));

        var shown = showAll ? benefits.ToCome : benefits.ToCome.Take(ChaptersShown).ToList();

        foreach (var chapter in shown)
        {
            var title = chapter.IsCurrent
                ? $"Ranks {chapter.FirstAhead}–{chapter.Last} · now"
                : $"Ranks {chapter.Label} · {chapter.Merits:N0} merits";

            var head = Text(title.ToUpperInvariant(), TypeScale.MetaSmall, chapter.IsCurrent ? ThemeManager.CyanKey : ThemeManager.GreyKey, chrome: true);
            head.Margin = new Thickness(0, 10, 0, 4);
            rows.Children.Add(head);

            foreach (var row in chapter.Rows)
            {
                rows.Children.Add(Ruled(ChangeRow(row)));
            }

            if (chapter.CarePackages is { } care)
            {
                rows.Children.Add(Note(care));
            }
        }

        var hidden = benefits.ToCome.Skip(shown.Count).ToList();

        if (hidden.Count > 0)
        {
            var more = new Button
            {
                Content = $"SHOW RANKS {hidden[0].FirstAhead}–{PowerplayRanks.LastTabulatedRank} · {hidden.Count} MORE CHAPTERS",
                MinHeight = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0),
            };

            Themed(more, Button.BackgroundProperty, ThemeManager.TileKey);
            more.Click += (_, _) => onShowAll?.Invoke();
            rows.Children.Add(more);
        }

        var past = Note($"Every rank past {PowerplayRanks.LastTabulatedRank} gives a full care package.");
        past.Margin = new Thickness(0, 8, 0, 0);
        rows.Children.Add(past);

        return rows;
    }

    private static Control ChangeRow(BenefitRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*,Auto"), Margin = new Thickness(0, 3) };

        var rank = Text(row.Rank.ToString(CultureInfo.InvariantCulture), TypeScale.Small, ThemeManager.WhiteKey, mono: true);
        var name = Text(row.Name, TypeScale.Small, ThemeManager.WhiteKey);
        name.Margin = new Thickness(0, 0, 12, 0);
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

    /// <summary>A name with its value beside it and a detail line under both.</summary>
    private static Control Row(string name, string value, string detail, string valueKey, string nameKey)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        line.Children.Add(Text(name, TypeScale.Small, nameKey));

        if (value.Length > 0)
        {
            line.Children.Add(Text(value, TypeScale.Small, valueKey, mono: true));
        }

        var stack = new StackPanel { Spacing = 0, Margin = new Thickness(0, 3), Children = { line } };

        if (detail.Length > 0)
        {
            stack.Children.Add(Text(detail, TypeScale.Meta, ThemeManager.GreyKey));
        }

        return Ruled(stack);
    }

    private static Control Ruled(Control row)
    {
        var rule = new Border { Child = row, BorderThickness = new Thickness(0, 0, 0, 1) };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return rule;
    }

    private static Control Heading(string text)
    {
        var head = Text(text.ToUpperInvariant(), TypeScale.Meta, ThemeManager.WhiteKey, chrome: true);
        var rule = new Border { Child = head, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4) };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.AKey);

        return rule;
    }

    private static TextBlock Note(string text)
    {
        var note = Text(text, TypeScale.Meta, ThemeManager.GreyKey);
        note.Margin = new Thickness(0, 4, 0, 2);

        return note;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
