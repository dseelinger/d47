# Building with d47

d47 lives inside Elite Dangerous. It borrows Elite's visual language and does not invent its own.
This design system is the **target** look: Claude Design owns it, and the Avalonia app is built to
match. Behaviour, strings and data come from the app — the text on these cards and screens is
placeholder.

**There are no React components to import.** `window.D47` carries the class maps, the theme list and
`setTheme`. Build with the classes and tokens below.

## Wrapping

```html
<div class="d47-root" data-d47-theme="elite">
  <!-- everything else -->
</div>
```

`d47-root` sets the ground, the accent ink and Sintony. `data-d47-theme` picks the palette:

| Id | Name | Notes |
| --- | --- | --- |
| `elite` | Elite | The default. Glow and scanlines on. |
| `dark` | Dark | VS Code Dark+ values. No glow, no scanlines. |
| `light` | Light | No glow, no scanlines. |
| `elite-palette` | My HUD colours | The Commander's HUD colour matrix applied to the coloured tokens only. Drawn here in Elite's colours, because the matrix is per player. |

## Colour is relationship and state

| Token | Meaning |
| --- | --- |
| `--d47-a` | Orange: can act, and values. |
| `--d47-grey` | Unavailable, read-only, or a label. `--d47-grey2` for placeholders and disabled. |
| `--d47-white` | Names, titles and speech. |
| `--d47-cyan` | Yours, current or ready — the current system, your own turns, PTT READY, a focused input. |
| `--d47-blue` | Confirmed or met — ✓ MET, verified counts. Never used for "yours". |
| `--d47-yellow` | Stored or equipped — KEY STORED, capacity. |
| `--d47-red` | Destructive, hostile or locked. |
| `--d47-bg`, `--d47-bar`, `--d47-slab` | Window ground; title bar and modal; read-only data and disabled. Never tinted. |
| `--d47-knock`, `--d47-brown` | Text on a solid orange fill; labels inside a selected row. |
| `--d47-tile`, `--d47-tile2` | Accent 20% / 30% into the ground: a control at rest / on hover. |
| `--d47-line`, `--d47-line2` | Accent 55% / 28%: the window frame and list heads / row separators. |

Every system name other than the current one is orange.

## Type

`--d47-font-chrome` Saira 500/600 — chrome, headings, tiles; uppercase with 0.04–0.12em tracking.
`--d47-font-prose` Sintony — body and speech, sentence case. `--d47-font-mono` JetBrains Mono — numbers,
keys, timestamps, costs. Scale: title 28, tab 15, group head 15/600, row label 15, body 16, control
13–14, meta 11–12.

## Rules

- **Controls are flat filled tiles**, 2px apart: `tile` at rest, `tile2` on hover, solid `a` with
  `knock` text when pressed or selected. No outlines, no rounded corners, no button weight — SEND is
  a default tile. Destructive is a red tile.
- **Read-only data** sits on `slab`: grey label, white or orange value.
- **Glyphs** are 32px inside a 44×44 hit area. Every interactive target is at least 44px.
- **Section heads** sit on a 1px full-width orange rule. Glow only on the brand diamond and the active tab.
- **Pick the control by the list:** Segmented for 4 or fewer options, Stepper for 5–7 fixed options,
  Dropdown only for long, variable-length lists (audio devices, voices). Every boolean is a checkbox tile.
- **Settings rows** are `240px | 1fr | 44px` with a 3px left border (orange when protected) and an
  always-reserved reset column. One sub-section per page; no accordions.
- Chrome is uppercase and prose is sentence case. No emoji. Commander names in full: `CMDR JOHN DEPARAGON`.

## Classes

| Card | Classes |
| --- | --- |
| Surfaces | `d47-window`, `d47-titlebar`, `d47-brand-mark`, `d47-brand-name`, `d47-window__body`, `d47-title-block`, `d47-title`, `d47-crumb`, `d47-section-head`, `d47-scanlines` |
| Tab | `d47-tabs`, `d47-tab`, `d47-tabs-rule`, `d47-subbar`, `d47-subtabs`, `d47-subtab`, `d47-sidebar` |
| TileButton | `d47-tile-button` + `destructive`, `tall`, `full`, `compact` |
| GlyphButton | `d47-glyph`, `__face`, `__tip`, `d47-copy-icon` |
| CheckboxTile | `d47-checkbox` + `checked`, `caps`; `d47-checkbox-grid` + `cols-4`; `d47-check` |
| Segmented | `d47-segmented` + `cols-2`/`cols-3`; `d47-segment` + `selected`, `has-status` |
| Stepper, NumberStepper | `d47-stepper`, `d47-number-stepper`, `d47-stepper__arrow`, `__value`, `__text`, `__count` |
| Dropdown | `d47-dropdown`, `__button`, `__value`, `__caret`, `__list`, `__option` |
| LevelBar, MixerTable | `d47-level`, `d47-mixer__head`, `d47-mixer__row`, `d47-mixer__mute`, `d47-mixer__none` |
| KeyBinding, ApiKey | `d47-keybind`, `__chip`; `d47-apikey`, `__stored`, `__mask`, `__status`, `__input` |
| SearchField, TextBox | `d47-search`; `d47-field`, `__prefix` |
| DataBlock | `d47-data-grid`, `d47-data`, `__label`, `__value`; `d47-gauge`; `d47-ladder` |
| Message, StatusRow | `d47-messages`, `d47-message` + `cmdr`; `d47-status`; `d47-composer` |
| Modal | `d47-scrim`, `d47-modal`, `__head`, `__dek`, `__title`, `__body`, `__foot`; `d47-breakdown` |
| GroupHead, SettingsRow | `d47-page-head`, `d47-group-head`, `d47-settings`, `d47-group`, `d47-row` + `protected`, `d47-inset` |
| ListBoxItem, Card | `d47-list`, `d47-list-head`, `d47-list-row` + `selected`; `d47-card` + `selected` |

`is-hover`, `is-pressed` and `is-focus` draw those states without the pointer.

## Where the truth is

`styles.css` and its imports: `tokens/palette.css` (the four themes), `tokens/type.css`, `_ds_bundle.css`
(the kit). Each card's `.prompt.md` has its markup. Full screens are under `ui_kits/d47/`.
The decisions behind all of it are in the d47 repository at `design/ds-update/`.
