# Handoff: Guardian Voice Effects settings group, v5

## Overview
One settings group on the **Its voice** page of d47. It sets optional audio treatments for the ship AI's voice.

In the group the Commander can:
- pick a preset
- test the result
- tick effects
- **reorder the effect chain**
- **set a level for each effect**
- **manage presets of their own: create, view, update, rename and delete them**

**Scope note:** this goes beyond #237/#238. Effect order, per-effect levels and the Commander's own presets change what is stored, how presets are matched and how the audio is processed. Treat them as a follow-up issue, or widen #237/#238 on purpose.

## About the design files
`Guardian Voice Effects v5.dc.html` is a **design reference built in HTML**. It shows the intended look and behaviour; it is not production code. Rebuild it in the d47 Avalonia app with the app's existing d47 styles and controls. Open the file in a browser, served from this folder, to click through it. `reference/original-mockup.png` shows where the design started.

## Fidelity
**High-fidelity.** Colours, type, spacing and states come from the d47 design system in its Elite theme: `_ds/…/_ds_bundle.css` and `tokens/*.css`. Where this README and the kit disagree, the kit is right.

**Placeholders you must replace:**
- the effects in each built-in preset
- each effect's parameter, range and default level
- the example saved preset "Hull breach"

## Layout
The group is fluid, up to 960px wide, and sits in the settings page column. From top to bottom:

### 1. Group head (`d47-group-head`)
- Flex row, 12px gap, min-height 38px, 6px padding at the bottom, with a **1px `--d47-a` rule** under it across the full width.
- Name: "GUARDIAN VOICE EFFECTS". Saira 15px, weight 600, uppercase, 0.06em tracking, `--d47-white`.
- Description: "Optional, off by default, applies to every core." Sintony 14px, `--d47-grey`, on one line.
- Reset glyph on the right:
  - 32×32 face inside a 44×44 hit area, `↺` in orange on `--d47-tile`.
  - On hover or focus the face is solid `--d47-a` and the glyph `--d47-knock`.
  - Tooltip: "RESET GROUP".
  - Pressing it clears every box and puts the order and levels back to their defaults.

### 2. Preset row (`d47-row`)
The row is a grid, `240px | minmax(0,1fr) | 44px`, with a 16px gap. It has a min-height of 56px, 6px 0 6px 12px padding and a 3px transparent left border.

- **Label:** "Preset", Sintony 15px, white.
- **Control column:** a 2px-gap flex row aligned to the top, holding:
  - **Preset picker** (`d47-dropdown inline`, flex 1):
    - Button: 40px tall, `--d47-tile` (`--d47-tile2` on hover and while open), caret `▼` in `--d47-a` at 11px.
    - Value: Saira 14px, weight 500. It is **white for a built-in preset or Custom, and `--d47-cyan` for one of the Commander's own**.
    - After a saved preset has been picked and then changed, the value reads "Custom" followed by "  · changed from <name>" in `--d47-grey`.
    - **The list opens in the page and pushes the content below it down. Never use a pop-up or flyout, because they don't render in the VR headset.**
    - List box: `--d47-bar` background, 1px `--d47-a` border, 2px padding, 2px gap.
    - Options: 36px min-height, `--d47-tile`, Saira 14px, weight 500, white. Hover is `--d47-tile2`; the current option is solid `--d47-a` with `--d47-knock` text.
  - **List order:**
    1. The built-in presets: Off, Vocoder, Deep core, Damaged core, Ring-mod rasp, 8-bit computer, Flanged vocoder.
    2. The heading "YOUR PRESETS": Saira 11px, weight 600, uppercase, 0.06em tracking, `--d47-grey`, 10px 12px 4px padding.
    3. The Commander's presets, labelled in `--d47-cyan`. If there are none, show one row on `--d47-slab` in grey 14px: "None yet. Set up the effects, then press SAVE AS."
    4. An 8px space, then Custom.
  - **TEST** (`d47-tile-button tall`): 120×40. While playing it is solid `--d47-a` with `--d47-knock` text and reads "PLAYING".
  - **Preset buttons** (all `d47-tile-button tall`, 120×40, 2px apart). Which buttons appear depends on the value the preset field shows:

    | Preset field shows | Buttons after TEST |
    | --- | --- |
    | A built-in preset (including Off) | SAVE AS, `disabled` (`--d47-slab` background, `--d47-grey2` text) |
    | Custom, not based on one of your presets | SAVE AS |
    | Custom, changed from one of your presets | UPDATE, SAVE AS |
    | One of your presets, unchanged | RENAME, DELETE (`destructive`) |

    - SAVE AS and RENAME are selected (solid `--d47-a` with `--d47-knock` text) while their name row is open.
    - DELETE is `--d47-red-tile` with red text at rest, and solid `--d47-red` with white text on hover or press. **There is no confirmation step; UNDO in the notice covers mistakes.**
- **Reset column:** 44px, reserved.

### 3. Notice (shown after an action)
A strip straight below the Preset row. Min-height 36px, `--d47-slab` background, 3px `--d47-cyan` left border, padding 0 12px 0 15px.
- Message in Sintony 14px, white: "Saved <name>.", "Renamed <old> to <new>.", "Updated <name> with the current effects." or "Deleted <name>."
- After Update and Delete, an **UNDO** tile (`d47-tile-button compact`) sits on the right. It restores the saved presets and the effect settings exactly as they were before the action.
- The notice hides after 6 seconds, or when the next action replaces it.

### 4. Name row (`d47-row`, shown only while creating or renaming)
It appears below the Preset row (and below the notice, if one is showing), inside the page, not as a pop-up. Its 3px left border is `--d47-cyan`, and it has 10px padding top and bottom.

- **Label:** "Preset name" when creating, "New name" when renaming.
- **Control:** a 2px-gap row with:
  - A `d47-field` text box (flex 1, 44px, 1px `--d47-a` border that turns `--d47-cyan` when focused). Placeholder "Name this preset", 32 characters at most, focused when the row opens. When renaming it starts filled with the current name.
  - **SAVE** (when creating) or **RENAME** (when renaming), 120×44. Shown `disabled` while the name is invalid.
  - **CANCEL**, 120×44.
- **Message line** below, 12px, 6px gap:
  - Normally grey: "Saves the ticked effects, their order and levels. Your presets are listed under Your presets."
  - After a failed attempt it is `--d47-red` and says "Enter a name.", "A preset with that name already exists." or, when renaming, "That is already its name."
- **Keys:** Enter confirms and Esc cancels.

### 5. Effects list (full group width)
The list has a 1px `--d47-line2` rule above it and 12px 0 12px 15px padding. It does **not** use the 240px label column, so the level bars have room.

- **Heading line:** "Effects" (Sintony 15px, white) with the hint "Applied top to bottom. Drag to reorder." (12px, grey) beside it, 12px gap, aligned on the text baseline. There is a 10px gap between the heading and the list.
- **The list:** one effect per line, 2px between lines. Each line is one `--d47-tile` strip, at least 44px tall, containing from left to right:
  1. **Drag handle.** 24px wide, full height.
     - Six 3×3px squares in `--d47-a`, two columns, 3px apart.
     - Background clear at rest, `--d47-tile2` on hover.
     - While dragging, the handle is solid `--d47-a` with `--d47-knock` dots, the strip turns `--d47-tile2`, and the strip is at 55% opacity.
  2. **Position number.** 24px wide, right-aligned, JetBrains Mono 11px, grey, two digits (01, 02, …).
  3. **Checkbox** (`d47-checkbox`), 200px wide, on a clear background (`--d47-tile2` on hover).
     - Box: 16×16 with a 2px `--d47-a` border. When ticked it shows an 8×8 `--d47-a` square.
     - Label: Sintony 15px, white.
  4. **Level** (`d47-level`, takes the remaining width, 4px padding on the left and 12px on the right):
     - Parameter label: 48px wide, Saira 11px, weight 600, uppercase, grey.
     - Track: 20 segments with 2px gaps, 14px tall, **at least 180px wide**. Lit segments are `--d47-a`. Unlit segments are `--d47-bg`, so they read as dark slots in the tile.
     - When the effect is unticked the bar gets `muted`: lit segments turn `--d47-grey2`. It can still be changed.
  5. **Stepper** (`d47-number-stepper`), 156×44:
     - − and + arrows, 44px wide, on `--d47-tile2`.
     - The value sits on `--d47-slab` in JetBrains Mono. It is white when the effect is ticked and `--d47-grey2` when it isn't.

**Default order:** Cylon, Pitch down, Octave-down layer, Chorus, Flanger, Phaser, Wah, Metallic resonance, Ring modulation, Deep ring mod, Tremolo, Overdrive, Bitcrusher, Glitch, Reverb. New effects from #239–#242 go into this order in the right place.

**Parameters (placeholders).** The level runs from 1 to 20 and is shown in real units:

| Effect | Parameter | Range | Default level |
| --- | --- | --- | --- |
| Cylon | Depth | 5–100% | 10 |
| Pitch down | Pitch | −0.6 to −12 st | 5 |
| Octave-down layer | Mix | 5–100% | 8 |
| Chorus | Depth | 5–100% | 10 |
| Flanger | Rate | 0.1–5 Hz | 6 |
| Phaser | Rate | 0.1–5 Hz | 6 |
| Wah | Rate | 0.2–8 Hz | 6 |
| Metallic resonance | Amount | 5–100% | 10 |
| Ring modulation | Freq | 20–2000 Hz | 6 |
| Deep ring mod | Freq | 5–200 Hz | 6 |
| Tremolo | Rate | 1–20 Hz | 6 |
| Overdrive | Drive | 5–100% | 8 |
| Bitcrusher | Bits | 15 → 2 bit | 12 |
| Glitch | Rate | 5–100% | 6 |
| Reverb | Mix | 5–100% | 6 |

Nothing in this group is hidden behind "show everything".

## Interactions and behaviour
**Choosing a preset**
- Pick a **built-in preset** to tick exactly its effects, reset the order to the default and reset every level to its default.
- Pick **one of your presets** to restore its ticked effects, order and levels exactly. That preset becomes the **basis** (see State), so later changes can be saved back to it.
- Pick **Custom** to close the list without changing anything.

**Changing effects**
- **Tick a box** to turn that effect on or off.
- **Reorder** by dragging a handle. Other lines move out of the way live, and the position numbers update. With the handle focused, the arrow keys move the effect up or down one place, which covers gamepad and VR use.
- **Change a level** by clicking a segment, dragging across the track, or pressing the stepper's − or + by one step. The level stays between 1 and 20.

**Test, save and reset**
- **TEST** plays the sample line through the current chain. It shows "PLAYING" while the sample plays (faked as 1.6s in the mockup).
- **Managing your presets:**
  - **Create:**
    1. Press SAVE AS (active only when the field shows Custom). The name row opens and the preset list closes.
    2. Enter a name. It is trimmed, must not be empty, and must not match any built-in name, any of your preset names or "Custom", ignoring case.
    3. Press SAVE. The preset is added to Your presets, the field shows it straight away in cyan, it becomes the basis, and the notice reads "Saved <name>."
  - **View:** your presets are listed in the preset picker under Your presets. Pick one to load it.
  - **Update:** load one of your presets, then change something; the field shows "Custom · changed from <name>". Press UPDATE to save the current effects into that preset straight away. The notice offers UNDO. SAVE AS in the same state leaves the original alone and creates a new preset.
  - **Rename:** with one of your presets showing and unchanged, press RENAME. The name row opens filled with the current name. The same name rules apply, except the preset's own current name doesn't count as taken. A name that differs only in capitalisation is allowed; exactly the same name gives "That is already its name."
  - **Delete:** with one of your presets showing, press DELETE. It is removed straight away. The field then shows Custom, the basis is cleared, and the notice offers UNDO.
- **Group reset** turns every effect off, restores the default order and levels, and clears the basis, so the field shows Off.

**Transitions:** none. All states switch instantly.

## State
**Saved settings**
- `enabled: Set<EffectId>`
- `order: EffectId[]`, covering every effect, ticked or not
- `levels: Record<EffectId, 1..20>`
- `userPresets: { name, enabled, order, levels }[]`

**Calculated, not saved**
- `preset`: the first preset whose settings match the current ones exactly, checking the built-in presets first, then your presets; otherwise "Custom". Settings match when all three of these are equal:
  - the ticked effects, ignoring their order
  - the full order
  - every level
- A built-in preset uses the default order and default levels. An empty set with defaults is Off.
- `basis`: the name of the preset of yours that was last loaded, saved or renamed. It is only valid while that preset still exists. Picking a built-in preset, deleting the preset or a group reset clears it. It is what UPDATE writes into and what the "changed from" suffix names. Store it with the settings so UPDATE is still offered after a restart.

**UI only:** `pickerOpen`, `isTesting`, `dragging`, `nameMode` (none, create or rename), `draft`, `notice`, `undoSnapshot`.

**Rules**
- Preset names are unique, ignoring case.
- Two presets must never have identical settings. SAVE AS is only available on Custom, so saving can't create a duplicate.
- Your presets are per Commander. They sit beside the voice settings and belong to no single core.

**Open questions:** a limit on how many presets can be saved, the order of Your presets (creation order in the mockup, or alphabetical), and exporting or sharing presets.

## Design tokens (Elite)
**Colours**
- Grounds: `--d47-bg` #070606, `--d47-bar` #0F0D0C, `--d47-slab` #232120
- Text: `--d47-white` #EDE9E3, `--d47-grey` #A09B94, `--d47-grey2` #6E6A65
- Accent: `--d47-a` #FF7A1A, `--d47-knock` #140800
- States: `--d47-cyan` #33D6E8 (yours), `--d47-red` #F0343F
- Mixed in OKLab: `--d47-tile` and `--d47-tile2` are the accent mixed 20% and 30% into the ground. `--d47-line2` is 28%. `--d47-red-tile` is the red tile colour from the kit.

**Type**
- Saira for chrome: group head 15/600, tiles 13/600, dropdown 14/500, small labels 11/600.
- Sintony for text: labels 15, description 14, hints 12.
- JetBrains Mono for numbers and values: 11–13.

**Spacing**
- 2px between tiles, 16px column gap in rows, 12px left padding in rows, 3px row left border.
- 44px hit targets and reset column. The handle is 24px wide but full height.

**Shape:** no rounded corners, no shadows, no glow in this group.

## Assets
There are no images. `↺`, `▼`, `−` and `+` are text characters, and the handle dots are 3px squares. Use the app's own glyphs if it has them.

## Files
- `Guardian Voice Effects v5.dc.html`: the interactive prototype. Its logic class has the effect table, preset matching, reordering and managing your presets (with undo).
- `support.js`: the runtime the prototype needs to open.
- `_ds/…`: the d47 kit.
- `reference/original-mockup.png`: where the design started.
