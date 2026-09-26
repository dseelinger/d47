Tab — Tab: a tile, the active one solid orange with a soft glow, on a 2px orange rule. SubTab: text, the active one white with a 2px orange underline. Settings navigation is a sidebar showing one sub-section per page, with no accordions.

**Source:** DS-UPDATE.md §6 Tab / SubTab. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Tab` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Tab tabs={['Transcript', 'Fleet', 'Engineers', 'Settings']} value={tab} onChange={setTab} />
<Tab variant="subtabs" tabs={['In ship', 'Log file']} value={sub} onChange={setSub} />
```

Props are in `Tab.d.ts`. The four-theme reference is `Tab.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state wide"><span class="cap">tabs</span><div style="width:100%"><div class="d47-tabs"><span class="d47-tab active">Transcript</span><span class="d47-tab is-hover">Fleet</span><span class="d47-tab">Engineers</span><span class="d47-tab">Settings</span></div><div class="d47-tabs-rule"></div></div></div>
      <div class="state"><span class="cap">sub-tabs</span><div class="d47-subtabs"><span class="d47-subtab active">In ship</span><span class="d47-subtab is-hover">Log file</span><span class="d47-subtab">Journal file</span></div></div>
      <div class="state"><span class="cap">sidebar</span><div class="d47-sidebar" style="width:230px"><div class="d47-sidebar__note">Changes apply as you make them.</div><div class="d47-sidebar__group">Voice and hearing</div><div class="d47-sidebar__item active">Voice input</div><div class="d47-sidebar__item is-hover">Its voice</div><div class="d47-sidebar__item">Sounds and levels</div><div class="d47-sidebar__section">The ship's AI</div></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-tabs, .d47-tab, .active` | Tile tabs, 40px, 2px gaps. Follow the strip with .d47-tabs-rule. |
| `.d47-subtabs, .d47-subtab, .active` | Text sub-tabs; active is white with a 2px orange underline. |
| `.d47-subbar` | The sub-tab row: sub-tabs left, tools at __end, line2 rule below. |
| `.d47-sidebar` | __note, __group (current section), __item (+ .active), __section (other sections). |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
