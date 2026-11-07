using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>What a control shows, read from the controls it draws.</summary>
internal static class DrawnState
{
    extension(PageFooter footer)
    {
        /// <summary>The time shown, or null when the right side is not drawn.</summary>
        public string? Time =>
            footer.GetLogicalDescendants().OfType<StackPanel>().Single() is { IsVisible: true } stamp
                ? ((TextBlock)stamp.Children[^1]).Text
                : null;
    }

    extension(Sidebar sidebar)
    {
        /// <summary>The rows a Commander can choose, in order.</summary>
        public IReadOnlyList<Border> Items =>
            [.. ((StackPanel)sidebar.Child!).Children.OfType<Border>().Where(row => row.Child is Grid)];
    }

    extension(StatusLine line)
    {
        /// <summary>Whether the line shows its red notice.</summary>
        public bool Failed => line.IsVisible && line.Children.OfType<Notice>().Single().IsVisible;
    }

    extension(DrillView strip)
    {
        /// <summary>How many panes are drawn side by side.</summary>
        public int Panes => ((Grid)strip.Content!).Children.OfType<Border>().Count();
    }

    extension(FactionPage page)
    {
        /// <summary>The state the page draws: the failure notice, the searching slab, the sections or the no-report line, the Back button, or none of them.</summary>
        public FactionView View =>
            page.Shown<Notice>().Any(notice => notice.Label == FactionPage.FailedLabel) ? FactionView.Failed
            : page.Shown<TextBlock>().Any(block => block.Text == FactionPage.SearchingText) ? FactionView.Loading
            : page.Shown<Segment>().Any(segment => segment.Name == FactionPage.SectionsName)
                || page.Shown<TextBlock>().Any(block => block.Text == FactionPage.NoReportText) ? FactionView.Answered
            : page.Shown<Button>().Any(button => button.Name == FactionPage.BackName) ? FactionView.Picking
            : FactionView.Empty;

        /// <summary>The sentence in the failure notice.</summary>
        public string? FailureText => page.Shown<Notice>().Single(notice => notice.Label == FactionPage.FailedLabel).Text;

        /// <summary>The faction named in the field.</summary>
        public string? Faction => page.GetLogicalDescendants().OfType<TextBox>().Single(box => box.Name == FactionPage.FieldName).Text;

        /// <summary>Presses a section on the page's segment, as a Commander does.</summary>
        public void ShowSection(int section) =>
            page.GetLogicalDescendants().OfType<Segment>().Single(segment => segment.Name == FactionPage.SectionsName).PressOption(section);
    }

    extension(HullViewer viewer)
    {
        /// <summary>Whether the viewer holds an OpenGL view to draw through.</summary>
        public bool OnGpu => viewer.GetVisualChildren().OfType<HullGlView>().Any();
    }

    extension(PanelView panel)
    {
        /// <summary>Whether none of the reading on the transcript page showing is inside the view.</summary>
        public bool ReadingIsAboveTheFold
        {
            get
            {
                var journal = panel.Page == TranscriptPage.Journal;
                var scroller = panel.GetVisualDescendants().OfType<ScrollViewer>()
                    .Single(found => found.Name == (journal ? "JournalListScroller" : "TranscriptScroller"));
                var pad = panel.GetVisualDescendants().OfType<Control>()
                    .Single(found => found.Name == (journal ? "JournalList" : "TranscriptContent"));
                var top = pad.Margin.Top;
                var content = Math.Max(0, scroller.Extent.Height - pad.Margin.Top - pad.Margin.Bottom);
                var from = scroller.Offset.Y;
                var to = from + scroller.Viewport.Height;

                return Math.Min(to, top + content) - Math.Max(from, top) <= 0.5;
            }
        }
    }

    extension(BloomStack stack)
    {
        /// <summary>The ghosts drawn under the stack's child, backmost and narrowest first.</summary>
        public IReadOnlyList<Control> Ghosts =>
            [.. stack.GetVisualChildren().OfType<Control>().Where(child => !ReferenceEquals(child, stack.Child))];
    }

    extension(BloomStack)
    {
        /// <summary>Whether <paramref name="visual"/> is a stack's ghost rather than content of its own.</summary>
        public static bool IsGhost(Visual visual) =>
            visual.GetVisualParent() is BloomStack stack && !ReferenceEquals(visual, stack.Child);
    }

    extension(StarSystemPage page)
    {
        /// <summary>Whether the heading says the page follows the Commander's own system.</summary>
        public bool Following =>
            page.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text).FirstOrDefault(text => text is "YOUR SYSTEM · FOLLOWING YOU" or "STAR SYSTEM") switch
            {
                "YOUR SYSTEM · FOLLOWING YOU" => true,
                "STAR SYSTEM" => false,
                _ => throw new InvalidOperationException("The system page draws no heading."),
            };
    }

    extension(D47.App.Windowing.OverlayPanel overlay)
    {
        /// <summary>Whether the strip draws the frame that marks place mode.</summary>
        public bool IsPlacing => ((Border)overlay.Content!).BorderThickness != default;
    }

    extension(StoryRunPage page)
    {
        /// <summary>The section rows and line rows the page's list draws, in order.</summary>
        public IReadOnlyList<object> Rows =>
            [.. page.GetLogicalDescendants().OfType<ItemsControl>().Single(list => list.Name == StoryRunPage.ListName).ItemsSource!.Cast<object>()];
    }

    extension(Segment segment)
    {
        /// <summary>Presses the option at <paramref name="index"/>, which raises the choice as a click does.</summary>
        public void PressOption(int index) =>
            segment.GetLogicalDescendants().OfType<RadioButton>().ElementAt(index).IsChecked = true;
    }

    extension(AllResultsPage page)
    {
        /// <summary>The result rows, each tagged with its title, in order.</summary>
        public IReadOnlyList<Border> Rows =>
            [.. page.GetLogicalDescendants().OfType<Border>().Where(row => row.Tag is string)];
    }

    /// <summary>The controls of <typeparamref name="T"/> in <paramref name="root"/> that are visible, with every parent visible.</summary>
    private static IEnumerable<T> Shown<T>(this Control root)
        where T : Control =>
        root.GetLogicalDescendants().OfType<T>().Where(control => control.GetSelfAndLogicalAncestors().OfType<Control>().All(each => each.IsVisible));
}
