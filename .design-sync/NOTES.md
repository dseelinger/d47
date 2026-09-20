# design-sync notes for d47

## This repository is off-script

d47 is an Avalonia desktop application, not a React package, so none of the skill's converter
scripts (`package-build.mjs`, `resync.mjs` and the rest) apply. The bundle is produced by a
generator kept outside the repository, at `C:\dev\d47-design-sync`:

| Path | What |
| --- | --- |
| `gen/palette.mjs` | The four palettes and the derived-role arithmetic from `ThemeManager.Apply`. |
| `gen/kit.css` | The control kit, hand-translated from `ControlKitTheme.axaml`. |
| `gen/build.mjs` | Emits the bundle into `ds-bundle/`. |
| `gen/verify-tokens.mjs` | Diffs every generated token against Avalonia's own resolved brushes. |
| `gen/validate.mjs` | The upload contract: `@dsCard` lines, import closure, fonts, `.d.ts`. |
| `harness/` | A headless Avalonia app that renders the real control kit to PNGs. |
| `reference/` | Its output: 36 captures, `roles.json` and `sizes.json`. |

The whole loop is: `dotnet run` the harness, then `node gen/build.mjs && node gen/validate.mjs &&
node gen/verify-tokens.mjs`.

## The repository's own captures do not show the control kit

`tests/D47.App.Tests/HeadlessApp.cs` adds `FluentTheme`, `ScrollViewerTheme.axaml` and
`TypeScale.axaml`, but **not `ControlKitTheme.axaml`**, which `App.axaml` does add. So every PNG
under `%TEMP%\d47-ui-captures` draws `Button`, `ToggleSwitch`, `TextBox`, `ListBoxItem` and
`NumericUpDown` with Fluent's defaults — rounded blue toggles, grey buttons — and not d47's dress.
The captures are still a good reference for the palette, `CardChrome`, `ChamferedBorder` and the
type scale, all of which the tests apply directly.

`HeadlessApp` also omits the `FontManagerOptions { DefaultFamilyName = Fonts.BodyFamily }` that
`Program.cs:78` sets, so captured body text is Inter where the application draws Saira.

That is why the harness exists: it loads the real `d47.dll`, adds the control kit and sets the
font default, so the captures it makes are what a Commander actually sees. Worth considering as a
repository fix — see the "Worth filing" section.

## Things that bit, and what they cost

- **`Math.Round` is banker's rounding in .NET.** `ThemeManager.Mix(White, Accent, 0.65)` on
  Elite's accent lands on exactly 248.5 in the red channel, which C# rounds to 248 and JavaScript
  rounds to 249. `D47.AccentInk` came out `#F9B063` instead of `#F8B063` until `palette.mjs` grew
  a half-to-even rounder. Any future arithmetic ported out of C# needs the same treatment.
  (The comment on `ThemeManager.AccentInkKey` says `#FFB066`; the computed value is `#F8B063`.
  The comment is approximate — trust the arithmetic.)
- **Line heights have to be set, not inherited.** Avalonia lays a 14px line of Saira out in a
  17px box; a browser asked for `line-height: normal` uses the font's metrics and gives 22px.
  Left alone, every control came out four or five pixels taller than the app draws it. The ratios
  in `kit.css` (1.214 body, 1.643 for the label face, 1.286 in fields) reproduce the measured
  heights: button 33, segment 37, text box 32, number field 34.
- **A translucent fill inside a translucent border stacks.** `ChamferedBorder` is drawn as two
  clipped layers, and painting `D47.Rule` on the outer put 42% accent underneath the inner's 10%,
  so the interior came out far lighter than the app's. Both layers now mix down onto the
  background with `color-mix`, using the same two percentages. The cost: a chamfered border drawn
  on a surface other than `D47.Background` is a shade out.
- **The primary button really is two pixels shorter** than the secondary one, because its theme
  sets `BorderThickness` to 0 and Avalonia lays a border outside the padding. The kit leaves
  `box-sizing` at the CSS default so the same thing happens; only controls with an explicit
  `Width`/`Height` in the theme (the toggle track, the row's minimum) use `border-box`.

## Known render warns

None. All twelve cards render four theme bands, with the stylesheet loaded and Saira applied.

The `Palette` card's Light band is about 20px shorter than the others. That is correct: bloom and
the scanlines resolve to `none` in Light, so those swatch rows carry a shorter value string.

## What this sync decided

- **CSS, not React.** `_ds_bundle.js` is an empty-bodied IIFE carrying theme metadata and
  `setTheme`. Shipping React wrappers was considered and dropped: they would have been an invented
  API that no card exercises, and so nothing would have verified them. Everything shipped is
  proved by a card.
- **Twelve cards, not ten.** The ten asked for, plus `Palette` and `TypeScale` under Foundations,
  so the tokens and the scale are browsable rather than only readable in a stylesheet.
- **Four theme bands per card**, which is what makes each card directly comparable to the four
  reference captures of the same control.

## Re-sync risks

- `gen/palette.mjs` holds a **transcription** of `Palette.cs`. A new palette, a renamed role or a
  changed percentage in `ThemeManager.Apply` will not be picked up on its own —
  `verify-tokens.mjs` catches a changed *value* (it diffs against the live `roles.json`) but a
  **new role** only shows up as "no token generated", and only if the harness ran first. Always
  re-run the harness before the generator.
- `kit.css` is a **hand translation** of `ControlKitTheme.axaml`. Nothing detects a drift in that
  file. On re-sync, diff `ControlKitTheme.axaml`, `CardChrome.cs` and `ChamferedBorder.cs` against
  what the kit says before trusting the output.
- `harness/harness.csproj` points `D47Bin` at `src/D47.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64`
  and reads `d47.dll` from there, resolving the rest of the app's dependencies out of the same
  folder at run time. It needs a Debug build of `D47.App` present, and its
  `Microsoft.Extensions.Logging.Abstractions` reference has to stay on the major the app uses
  (10.x as of this sync) or the compile fails with CS1705.
- The harness applies the theme id `"elite"`, not `"elite-palette"`, so it never reads the
  Commander's HUD matrix and the captures are reproducible on any machine.
- The card CSS uses `color-mix`, `clip-path` and `mask`-free two-layer clipping. All fine in
  current Chrome, which is what renders both the cards and the designs.

## Worth filing

`HeadlessApp` diverging from `App.axaml` means `TheReworkedChromeRendersToACaptureTests` and every
other capture test photograph a chrome the application never shows. Adding the
`ControlKitTheme.axaml` style include and the `FontManagerOptions` default to `HeadlessApp` would
make the captures match the product. Not done here: it is a change under `tests/`.
