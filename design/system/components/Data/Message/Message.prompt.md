Message — The transcript is SMS-shaped. D47 on the left with an orange bar; the Commander on the right with a cyan bar and tint. The header is the name, the intent tags, then the time after a fixed 12px gap. The cost line is mono 12 grey2 uppercase, inside the message it belongs to.

**Source:** DS-UPDATE.md §6 Message (SMS). d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Message` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<div className="d47-messages"><Message intent="continuity.resume" time="19:41">Good evening, Commander.</Message><Message from="cmdr" name="CMDR John Deparagon" time="19:41">How are you this evening?</Message></div>
```

Props are in `Message.d.ts`. The four-theme reference is `Message.themes.html`.

## Markup (hand-written)

```html
<div class="d47-messages" style="width:100%">
      <div class="d47-message"><div class="d47-message__head"><span class="d47-message__name">D47</span><span class="d47-message__intent">continuity.resume</span><span class="d47-message__time">19:41</span></div><div class="d47-message__body">Good evening, Commander. Ready to go.</div></div>
      <div class="d47-message cmdr"><div class="d47-message__head"><span class="d47-message__name">CMDR John Deparagon</span><span class="d47-message__time">19:41</span></div><div class="d47-message__body">How are you this evening?</div></div>
      <div class="d47-message"><div class="d47-message__head"><span class="d47-message__name">D47</span><span class="d47-message__delivery">calm</span><span class="d47-message__time">19:42</span></div><div class="d47-message__body">Functioning within tolerance, Commander. Docked at Sacred Fire, Giryak, systems quiet.</div>
        <div class="d47-message__chips"><span class="d47-message__chip here">Giryak</span><span class="d47-glyph"><span class="d47-glyph__tip">Copy system name</span><span class="d47-glyph__face"><span class="d47-copy-icon"></span></span></span></div>
        <div class="d47-message__cost"><span>Answered via claude-sonnet-5</span><span>·</span><span>Effort high</span><span>·</span><span class="d47-message__cost-value">$0.1075</span></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-messages` | The list, 4px apart, pinned to the bottom by its container. |
| `.d47-message, .cmdr` | D47 left with an orange bar; .cmdr right with a cyan bar and cyan ground. Max 72% wide. |
| `__head: __name, __intent, __delivery, __time` | Name, then tags, then the time 12px after them. |
| `__body, __chips, __chip(.here), __cost` | Sintony 16 white; slab chips; the mono cost line. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
