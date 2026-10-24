---
name: new-ship
description: Add a new Elite Dangerous hull to d47 in stages — detect it against the specs table, regenerate the data, update the lists the tests name, then take it through the art pipeline to a card still and a release upload. Reports which stages are done and what each remaining one waits on; writes on the maintainer's word where a change leaves this repository, and never pushes. Use when the user invokes /new-ship, /new-ship <symbol>, or says "new ship", "Frontier added a hull", "add the new hull", "is there a new ship".
---

# New ship

Adding a hull means a regenerated specs table, up to four hand-kept entries, art captured in game and
a release upload. Each hand-kept entry is enforced by a test, so the stages below follow the tests.
Read upstream, report, write on acknowledgement. Never push.

An argument names the hull symbol (`/new-ship anaconda_mkii`). With none, run stage 1 and report.

Report at the start and end of every run which stages are done and what each remaining one waits on.
One commit per stage, each with a `CHANGELOG.md` entry when the change is user-visible. Stage 4's
changes to the pipeline checkout and release upload leave this repository: show each, write on the
maintainer's word.

## 1. Detect

1. Read `shipyard.csv` from the naming repository `tools/gen-elite-specs.py` reads (`FDEV_IDS`) and
   compare its hull symbols with the `[ships]` section of `src/D47.Core/Knowledge/EliteSpecifications.tsv`.
2. List hull symbols in the local journals that the table does not know: `LoadGame.Ship`,
   `ShipyardBuy.ShipType` and `ShipyardNew.ShipType`. Read the journals where the code reads them; do
   not search the disk.
3. Report each hull's symbol, id, name and SKU.

If the hull is not in `shipyard.csv`, stop and say so. The generator loops over that file alone
(`build_ships` in `gen-elite-specs.py`), so stage 2 cannot produce a row for it.

## 2. Data

1. `python tools/gen-elite-specs.py`
2. `python tools/gen-blueprints.py`, which reads the new bulkheads from `[modules]`.
3. Report which `[ships]` columns are blank for the new hull. A blank column means a figure source
   has not caught up, not that the run failed.
4. Run:

   ```bash
   dotnet test tests/D47.Core.Tests --filter "FullyQualifiedName~Knowledge&Category!=Integration&Category!=Gate"
   dotnet test tests/D47.Core.Tests --filter "Category=Gate"
   ```

Generated data is generated: edit the generator, never the table.

## 3. Lists, as the tests name them

- **ARX early access.** While the hull is ARX-only, add it to `ARX_EARLY_ACCESS` in
  `tools/curated_ships.py` (#1364), then re-run stage 2.
- **Outfitting names.** Add the hull to `OutfittingCatalogue.Ships` only once the outside station
  search's own field values list it; those names belong to that service and cannot be derived from the
  table. Update the count, and the date in the comment, in
  `OutfittingCatalogueTests.TheCataloguesAreTheOnesTheServiceActuallyHas`, and the ship count in
  `docs/capabilities/galaxy.md` (search for "modules and"). Where the field values do not list the hull
  yet, say that this entry waits.

## 4. Art

The maintainer takes a RenderDoc capture of the hull on the shipyard screen. Everything after that is
this skill's, in the render pipeline at `C:\dev\tools\RenderDoc2obj` (dseelinger/3d-tools). That checkout
is outside this repository: show each change there and write only on the maintainer's word.

1. **Find the capture.** New captures land in `%LOCALAPPDATA%\Temp\RenderDoc` as
   `EDLaunch_<date>_frame<N>.rdc`. When more than one is new, tell them apart with `rd_thumbs.py`
   (`RDC_DIR=<folder> OUT_DIR=<folder> qrenderdoc.exe --python rd_thumbs.py`) and show the maintainer the
   thumbnail of the one picked. Move it into the pipeline's `captures\` folder, where the carrier captures
   are kept, because Temp gets cleared.
2. **Register it.** Name the hull in the pipeline's style, lower case with hyphens, such as
   `anaconda-mk2`. Add it to `ships.ps1` in a table of hulls captured by full file name, read by
   `Get-ShipCapture` alongside `$carriers`. Not in `$ships`: every entry there is a frame number under
   one capture session's file prefix.
3. **Render.** Run `.\ship.ps1 -Ship <name> -Stills` and show the maintainer the two stills in
   `work\<name>\spin\test\` before the long render. On their word, run `.\ship.ps1 -Ship <name> -Video`
   in the background; it renders the 180-frame turntable, the MP4 and the 4K still, and takes several
   minutes. Then run `.\mesh.ps1 -Ship <name>`.
4. **Collect.** Add `'<name>' = '<symbol>'` to the map in `tools\ship-art.ps1` and run
   `tools\ship-art.ps1 -Hull <name>`. Commit the card still and remove the hull from the card picture
   test's list (#1363). Commit the card still only after the hull has its `[ships]` row:
   `EveryDrawingNamesAHullEliteWrites` fails on art for a symbol the table does not know.
5. **Publish.** Uploading `<symbol>.4k.png`, `<symbol>.spin.mp4` and `<symbol>.mesh` to the `ship-art-1`
   release (`ShipArtStore.Source`) is a GitHub write. Name the three files and their sizes, then stop;
   upload only on the maintainer's word.

Steps 1 to 3 do not depend on `shipyard.csv` and can run as soon as the capture exists.

## 5. Later passes

- Re-run stage 2 when a figure source fills a blank column.
- Remove the hull from `ARX_EARLY_ACCESS` when it reaches the shipyard for credits, and re-run stage 2.

## Weekly check

Stage 1 can run weekly as a report-only task saved outside the repository, as `/refresh-models` can.
Describe it when asked. Never create the scheduled task from this skill.
