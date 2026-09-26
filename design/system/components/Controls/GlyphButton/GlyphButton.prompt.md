GlyphButton — A 32px glyph tile inside a 44×44 hit area, for VR. On hover or focus the tile goes solid and a label appears flush beside it saying what it does. The reset glyph ↺ uses the same button.

**Source:** DS-UPDATE.md §5, §6 Glyph button. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `GlyphButton` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<GlyphButton label="Copy system name" onClick={copy} />
<GlyphButton glyph="↺" label="Reset to default" onClick={reset} />
```

Props are in `GlyphButton.d.ts`. The four-theme reference is `GlyphButton.themes.html`.

## Markup (hand-written)

```html
<div class="states" style="padding-left:160px">
      <div class="state"><span class="cap">rest</span><span class="d47-glyph"><span class="d47-glyph__tip">Copy system name</span><span class="d47-glyph__face"><span class="d47-copy-icon"></span></span></span></div>
      <div class="state"><span class="cap">hover, with its label</span><span class="d47-glyph is-hover"><span class="d47-glyph__tip">Copy system name</span><span class="d47-glyph__face"><span class="d47-copy-icon"></span></span></span></div>
      <div class="state"><span class="cap">pressed</span><span class="d47-glyph is-hover"><span class="d47-glyph__tip">Copied</span><span class="d47-glyph__face"><span class="d47-copy-icon"></span></span></span></div>
      <div class="state"><span class="cap">reset</span><span class="d47-glyph"><span class="d47-glyph__tip">Reset to default</span><span class="d47-glyph__face">↺</span></span></div>
      <div class="state"><span class="cap">disabled</span><span class="d47-glyph disabled"><span class="d47-glyph__face"><span class="d47-copy-icon"></span></span></span></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-glyph` | The 44×44 hit area. Holds __tip (the hover label) and __face (the 32px tile). |
| `.d47-copy-icon` | Two overlapping 10px squares, 2px strokes. |
| `.disabled` | grey2 glyph, no label. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
