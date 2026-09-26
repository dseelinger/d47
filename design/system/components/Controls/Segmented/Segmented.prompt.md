Segmented — For 4 or fewer options. Equal-width tiles, 2×2 when labels are long; the selected one is solid orange. Status goes on an 11px second line, with stored status in yellow.

**Source:** DS-UPDATE.md §6 Segmented. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Segmented` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Segmented value={engine} onChange={setEngine} options={[{ value: 'whisper', label: 'Whisper', status: 'This computer · free' }, { value: 'deepgram', label: 'Deepgram', status: 'Paid · key stored', stored: true }]} />
```

Props are in `Segmented.d.ts`. The four-theme reference is `Segmented.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state wide"><span class="cap">four, with status</span><div class="d47-segmented" style="width:100%"><div class="d47-segment has-status"><span class="d47-segment__label">Whisper</span><span class="d47-segment__status">This computer · free</span></div><div class="d47-segment has-status is-hover"><span class="d47-segment__label">Groq</span><span class="d47-segment__status">Paid · needs key</span></div><div class="d47-segment has-status"><span class="d47-segment__label">OpenAI</span><span class="d47-segment__status">Paid · needs key</span></div><div class="d47-segment has-status selected"><span class="d47-segment__label">Deepgram</span><span class="d47-segment__status stored">Paid · key stored</span></div></div></div>
      <div class="state wide"><span class="cap">2×2, long labels</span><div class="d47-segmented cols-2" style="width:100%"><div class="d47-segment selected"><span class="d47-segment__label">Press to talk (PTT)</span></div><div class="d47-segment"><span class="d47-segment__label">Toggle on and off</span></div><div class="d47-segment"><span class="d47-segment__label">Listen whenever I speak</span></div><div class="d47-segment"><span class="d47-segment__label">Listen when I say its name</span></div></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-segmented, .cols-2, .cols-3` | Four equal columns by default. |
| `.d47-segment, .selected, .has-status` | 40px, or 52px with a status line. |
| `.d47-segment__label, __status, .stored` | The label, and the 11px status; stored is yellow, brown when selected. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
