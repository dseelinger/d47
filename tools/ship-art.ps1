<#
.SYNOPSIS
    Collects a hull's rendered art out of the turntable pipeline and into `assets\ships\`.

.DESCRIPTION
    The renders come from a separate repository — `dseelinger/3d-tools`, `RenderDoc2obj` — which
    captures Elite's own shipyard geometry, poses it in the game's camera and renders a Freestyle
    line-art turntable. That pipeline writes into its own `work\<hull>\`. This is the step that
    takes what it produced and puts it where d47 reads it, so "where does a hull's picture come
    from" has one answer that can be run again rather than a folder somebody once copied by hand.

    **Three files per hull, and they reach a Commander three different ways.**

        <hull>.png        the card still, 1280x720. SHIPPED, inside the installer and the zip.
        <hull>.4k.png     the Ship Details picture, 3840x2160. Fetched on demand.
        <hull>.spin.mp4   the turntable. Fetched on demand.

    See `ShipArt` for the reading side and `ShipArtStore` for the fetching side.

    **The two PNGs are quantised to 256 colours on the way in, and that is not a size hack.**
    EEVEE's dithered shading does not compress, so a raw render is 0.7 to 8 MB of noise nobody
    can see: a median-cut palette with Floyd-Steinberg dithering leaves the orange lines untouched
    and turns the hull's tone into a fine dither, at a third of the bytes. Judged on the Panther
    Clipper at 1:1 on 2026-09-04 and adopted as the stopgap until the hull viewer (#287) replaces
    the picture. The raw renders stay in the pipeline; this writes copies.

    The card still is the turntable's own first frame rather than a separate render, so the
    picture that rests on a card is exactly the frame its rotation starts and ends on.

    **The files are renamed on the way in, and that is the step this exists for.** The pipeline
    names its work folders for people — `python-mk2`, `type-9-heavy`, `fer-de-lance`. d47 names
    hull art for the symbol Elite's journal writes — `python_nx`, `type9`, `ferdelance` — because
    `ShipArt` finds a hull's picture by that symbol and nothing else. Collecting the art without
    renaming it looks like it worked and silently leaves twelve hulls with no picture: the ones
    whose readable name happens to equal their symbol still draw. `TheShipArtIsNamedForItsHull`
    is the test that stops that shipping again.

.PARAMETER Hull
    The hulls to collect, by pipeline name. All of them when omitted.

.PARAMETER Pipeline
    Where the pipeline's `work\` folder is. Defaults to the sibling checkout.

.EXAMPLE
    tools\ship-art.ps1
    tools\ship-art.ps1 -Hull corsair, python-mk2
#>
[CmdletBinding()]
param(
    [string[]] $Hull,
    [string] $Pipeline = 'C:\dev\tools\RenderDoc2obj\work'
)

$ErrorActionPreference = 'Stop'

$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\ships'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

if (-not (Test-Path $Pipeline)) { throw "No pipeline work folder at $Pipeline" }

# One inline quantiser rather than a call out to the pipeline's own shrink_still.py: this repo
# should be able to say what its assets are without a second checkout being present to read.
$shrink = @'
import sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
image = Image.open(src).convert("RGB")
image.quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(dst, optimize=True)
'@
$shrinkFile = Join-Path ([System.IO.Path]::GetTempPath()) 'd47-ship-art-shrink.py'
Set-Content -Path $shrinkFile -Value $shrink -Encoding ascii

# The pipeline's folder name against the symbol Elite's journal writes. Only the hulls whose two
# names differ would strictly need a row, but every hull has one so that a missing entry is an
# error rather than a silent pass-through - which is exactly how the first collection shipped
# twelve hulls whose pictures nothing could find.
$symbols = @{
    'adder'                = 'adder'
    'alliance-challenger'  = 'typex_3'
    'alliance-chieftain'   = 'typex'
    'alliance-crusader'    = 'typex_2'
    'anaconda'             = 'anaconda'
    'asp-explorer'         = 'asp'
    'asp-scout'            = 'asp_scout'
    'beluga-liner'         = 'belugaliner'
    'caspian-explorer'     = 'explorer_nx'
    'cobra-mk3'            = 'cobramkiii'
    'cobra-mk5'            = 'cobramkv'
    'corsair'              = 'corsair'
    'diamondback-explorer' = 'diamondbackxl'
    'diamondback-scout'    = 'diamondback'
    'dolphin'              = 'dolphin'
    'eagle'                = 'eagle'
    'federal-assault-ship' = 'federation_dropship_mkii'
    'federal-corvette'     = 'federation_corvette'
    'federal-dropship'     = 'federation_dropship'
    'federal-gunship'      = 'federation_gunship'
    'fer-de-lance'         = 'ferdelance'
    'hauler'               = 'hauler'
    'imperial-clipper'     = 'empire_trader'
    'imperial-courier'     = 'empire_courier'
    'imperial-cutter'      = 'cutter'
    'imperial-eagle'       = 'empire_eagle'
    'keelback'             = 'independant_trader'
    'kestrel-mk2'          = 'smallcombat01_nx'
    'krait-mk2'            = 'krait_mkii'
    'krait-phantom'        = 'krait_light'
    'lynx-highliner'       = 'mediumtransport01'
    'mamba'                = 'mamba'
    'mandalay'             = 'mandalay'
    'orca'                 = 'orca'
    'panther-clipper-mk2'  = 'panthermkii'
    'python'               = 'python'
    'python-mk2'           = 'python_nx'
    'sidewinder'           = 'sidewinder'
    'type-10-defender'     = 'type9_military'
    'type-11-prospector'   = 'lakonminer'
    'type-6-transporter'   = 'type6'
    'type-7-transporter'   = 'type7'
    'type-8-transporter'   = 'type8'
    'type-9-heavy'         = 'type9'
    'viper-mk3'            = 'viper'
    'viper-mk4'            = 'viper_mkiv'
    'vulture'              = 'vulture'
}

$hulls = if ($Hull) { $Hull } else {
    Get-ChildItem -Path $Pipeline -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'spin.mp4') } |
        ForEach-Object { $_.Name }
}

$done = 0
foreach ($symbol in $hulls) {
    $work = Join-Path $Pipeline $symbol
    $frame = Join-Path $work 'spin\frame_0001.png'
    $still = Join-Path $work 'still.4k.png'
    $video = Join-Path $work 'spin.mp4'

    foreach ($needed in @($frame, $still, $video)) {
        if (-not (Test-Path $needed)) { throw "$symbol has no $needed; run the pipeline's turntable.ps1 first" }
    }

    $named = $symbols[$symbol]
    if (-not $named) { throw "No hull symbol for '$symbol'. Add it to the table in this file." }

    & python $shrinkFile $frame (Join-Path $assets "$named.png")
    & python $shrinkFile $still (Join-Path $assets "$named.4k.png")
    Copy-Item $video (Join-Path $assets "$named.spin.mp4") -Force

    Write-Host "  $symbol -> $named"
    $done++
}

Remove-Item $shrinkFile -ErrorAction SilentlyContinue

$shipped = (Get-ChildItem -Path $assets -Filter '*.png' |
    Where-Object { $_.Name -notlike '*.4k.png' } |
    Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host ("$done hull(s) collected into assets\ships.")
Write-Host ("Shipped in the installer: {0:N1} MB of card stills." -f ($shipped / 1MB))
