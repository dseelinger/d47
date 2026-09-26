ListBoxItem — List rows are filled tiles 2px apart, never outlined boxes. The name is white and the secondary text orange. Selection is a solid orange fill with the name in knock and the labels in brown.

**Source:** D47-VS-ELITE.md 8, 11. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `ListBoxItem` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<div className="d47-list"><div className="d47-list-head">Ready for unlock</div><ListBoxItem name="Broo Tarquin" sub="14 modules" aside="230 LY · ~6 jumps" selected /></div>
```

Props are in `ListBoxItem.d.ts`. The four-theme reference is `ListBoxItem.themes.html`.

## Markup (hand-written)

```html
<div class="d47-list" style="width:400px">
      <div class="d47-list-head">Ready for unlock</div>
      <div class="d47-list-row selected"><div class="d47-list-row__main"><span class="d47-list-row__name">Broo Tarquin</span><span class="d47-list-row__sub">14 modules</span></div><span class="d47-list-row__aside">230 LY · ~6 jumps</span></div>
      <div class="d47-list-row is-hover"><div class="d47-list-row__main"><span class="d47-list-row__name">Tiana Fortune</span><span class="d47-list-row__sub">9 modules</span></div><span class="d47-list-row__aside">177 LY · ~4 jumps</span></div>
      <div class="d47-list-row"><div class="d47-list-row__main"><span class="d47-list-row__name">Lori Jameson</span><span class="d47-list-row__sub">4 modules</span></div><span class="d47-list-row__aside">92 LY · ~3 jumps</span></div>
      <div class="d47-list-row disabled"><div class="d47-list-row__main"><span class="d47-list-row__name">Marsha Hicks</span><span class="d47-list-row__sub">Locked</span></div><span class="d47-list-row__aside">—</span></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-list, .d47-list-head` | The column and its orange group head on a line rule. |
| `.d47-list-row` | __main (__name, __sub) and __aside; .selected, .disabled, .compact (36px). |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
