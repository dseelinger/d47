SearchField — A 340px search field with a line2 border that turns orange on focus. It must not outweigh the message input.

**Source:** DS-UPDATE.md §6 Search field. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `SearchField` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<SearchField value={q} onChange={setQ} />
```

Props are in `SearchField.d.ts`. The four-theme reference is `SearchField.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">rest</span><div class="d47-search"><input placeholder="Search this page"></div></div>
      <div class="state"><span class="cap">focus</span><div class="d47-search is-focus"><input value="Giryak"></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-search` | 340×36; holds a bare input. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
