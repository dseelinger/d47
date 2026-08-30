using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>
/// What a furnished page marks so a surface nothing can be pressed on does not draw it
/// (<a href="https://github.com/dseelinger/d47/issues/202">#202</a>).
/// <para>
/// <b>The rule already existed and its mechanism could not hold it.</b> One style selector —
/// <c>PanelView.output-only Button</c> — was the whole of "a mini panel carries no clickable
/// control", and it failed in two separate ways at once. Avalonia style setters sit at
/// <c>BindingPriority.Style</c>, <em>below</em> <c>LocalValue</c>, so any control that assigns its
/// own <c>IsVisible</c> from code pins the value and the selector never applies; three did, and
/// they were exactly the controls a Commander reported seeing on a mini panel. And it matched
/// <em>exact</em> <c>Button</c>, so a filter checkbox on a chrome bar was never covered at all.
/// </para>
/// <para>
/// <b>So the unit is a container rather than a control, and that is what makes it hold.</b> A
/// hidden parent hides its children whatever they say about themselves, so a page is free to go on
/// writing <c>_suggestions.IsVisible = pending.Count > 0</c> and be right about it — the bar those
/// controls sit on is not there. It also survives a rebuild: pages rebuild their contents
/// constantly and build their bars once, so hiding the bar stays done while hiding each control
/// would be undone by the next redraw.
/// </para>
/// <para>
/// <b>Marked by the page, applied by a style in <see cref="PanelView"/>.</b> A page cannot work
/// this out for itself: <c>Mode</c> is a property of the view and is never passed down, and the
/// <c>output-only</c> class is styling rather than information — a page can neither read it nor
/// branch on it. What a page <em>does</em> know is which of its controls are chrome, and that is
/// the only thing it is asked for.
/// </para>
/// <para>
/// <b>A style rather than a walk over the tree, and the timing is why.</b> An imperative pass was
/// written first and could not be made to fire late enough: a drill level builds its page on first
/// sight, which is after the class has been set and after every event that could have triggered
/// one — so the walk found an empty pane and never ran again. A style applies when the control
/// enters the tree, whenever that turns out to be.
/// </para>
/// <para>
/// <b>What that asks of a page is one thing, and it is the whole contract: never assign
/// <c>IsVisible</c> on a container you marked.</b> A local value outranks a style setter, which is
/// the defect this exists to close — reintroducing it one level up would be the same bug wearing
/// the fix. Assigning it on the controls <em>inside</em> is free and expected; a hidden parent
/// hides them whatever they say.
/// </para>
/// <para>
/// <b>Keyed on the class and never on <c>Mode</c>.</b> The desktop window's own mini is fully
/// clickable by design and deliberately never takes the class — see
/// <c>MainWindow</c>'s comment, <em>"the flat overlay stays output-only by not making this
/// call"</em> — so a rule that read the mode would strip the controls off the one mini where they
/// work.
/// </para>
/// </summary>
public static class PageChrome
{
    /// <summary>
    /// The class a container of pressable things carries. One spelling, named here rather than
    /// typed at each site, because a style and a walk both have to agree about it.
    /// </summary>
    public const string Class = "page-chrome";

    /// <summary>
    /// Marks a container as chrome and hands it back, so it can be written inline where the
    /// container is built rather than as a second statement somewhere below it.
    /// </summary>
    public static T AsChrome<T>(this T control)
        where T : Control
    {
        control.Classes.Add(Class);
        return control;
    }

    /// <summary>Whether this control was marked.</summary>
    public static bool IsChrome(this Control control) => control.Classes.Contains(Class);
}
