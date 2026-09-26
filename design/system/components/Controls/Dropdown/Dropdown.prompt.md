Dropdown — For long, variable-length lists only: audio devices and voices. A tile with ▼ that opens a framed list below it. Use the Stepper for 5–7 fixed options and Segmented for 4 or fewer.

**Source:** DS-UPDATE.md §6 Dropdown. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Dropdown` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Dropdown options={voices} value={voice} onChange={setVoice} />
```

Props are in `Dropdown.d.ts`. The four-theme reference is `Dropdown.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">closed</span><div class="d47-dropdown" style="width:360px"><button class="d47-dropdown__button"><span class="d47-dropdown__value">System default · Headphones (Logi Z407)</span><span class="d47-dropdown__caret">▼</span></button></div></div>
      <div class="state"><span class="cap">open</span><div class="d47-dropdown inline" style="width:360px"><button class="d47-dropdown__button is-hover"><span class="d47-dropdown__value">Rachel · calm narrator · female, American</span><span class="d47-dropdown__caret">▼</span></button><div class="d47-dropdown__list"><div class="d47-dropdown__option">Ellis · clear, professional storyteller</div><div class="d47-dropdown__option selected">Rachel · calm narrator · female, American</div><div class="d47-dropdown__option is-hover">Adam · deep · male, American</div><div class="d47-dropdown__option">Charlotte · warm · female, British</div></div></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-dropdown` | __button (__value, __caret) and __list of __option (+ .selected). |
| `.inline` | Lays the list in flow instead of over what follows, for a static picture. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
