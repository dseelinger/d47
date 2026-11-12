---
title: Screenshots
group: Foundation
nav_order: 161
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Turn the JPEG copies of your Elite screenshots on or off.</p>
<section>
<h2><span class="num">1</span> Stop the copies.</h2>
<p class="say">Say "stop converting my screenshots".</p>
<p class="expect">D47 stops copying new screenshots. The next one is not converted.</p>
</section>
<section>
<h2><span class="num">2</span> Start them again.</h2>
<p class="say">Say "start converting my screenshots".</p>
<p class="expect">D47 copies each new screenshot again.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Open Settings › You and your records › Logs and goals, group Screenshots, and read the Saved to row.</p>
<p class="expect">It shows the D47 folder inside Elite's pictures folder. If Windows syncs that folder, as OneDrive can, the copies are synced too.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 saves a small copy of each screenshot and leaves Elite's alone.</p>
<section>
<h2><span class="num">1</span> Each screenshot is copied as a JPEG.</h2>
<p class="body">Each screenshot you take in Elite, including high-resolution ones, is copied as a JPEG. Elite's BMP is left alone unless you tick the box when you delete a copy.</p>
</section>
<section>
<h2><span class="num">2</span> The copy is named for the time and the place.</h2>
<p class="body">The copy goes in a D47 folder inside Elite's own pictures folder. Its name is the local time, the system and the body, and a note beside it records where it was taken and the codex entries logged just before it.</p>
</section>
<section>
<h2><span class="num">3</span> Only you can switch the copies off.</h2>
<svg viewBox="0 0 880 130" role="img" aria-label="Saying stop converting my screenshots turns the Convert Elite's screenshots row off">
 <rect x="20" y="24" width="330" height="52" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="185" y="57" text-anchor="middle" font-size="17" fill="var(--text)">“stop converting my screenshots”</text>
 <line x1="362" y1="50" x2="408" y2="50" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="422,50 406,42 406,58" fill="var(--accent-muted)"/>
 <rect x="434" y="24" width="426" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="647" y="57" text-anchor="middle" font-size="16" fill="var(--text)">“Convert Elite's screenshots is now off.”</text>
 <text x="440" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">The row is protected: the AI model is refused if it tries to change it.</text>
</svg>
<p class="body">The copies are written outside D47's data folder, so you change the row, by voice or in Settings.</p>
</section>
</div></div>
</details>

## The details

This is the first thing D47 writes into your Pictures folder, so it has a switch of its own, **Convert
Elite's screenshots**, on by default. The setting is `screenshots.convert`. **Privacy and egress**
lists it as **Your screenshots**: the copies are kept on this PC and sent nowhere.

A copy's name comes from the `Name` method of `EliteScreenshot`, such as `2026-10-29 14-05-09 Sol.jpg`.
A second screenshot in the same second adds ` (2)`, and characters Windows does not allow in a file name
are dropped from the system and body.

```csharp
public string Name(DateTime localTime, int copy)
{
    var parts = new List<string> { localTime.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) };

    foreach (var part in new[] { System, Body })
    {
        if (Dropped(part) is { Length: > 0 } kept)
        {
            parts.Add(kept);
        }
    }

    var suffix = copy > 1 ? $" ({copy.ToString(CultureInfo.InvariantCulture)})" : "";

    return $"{string.Join(' ', parts)}{suffix}.jpg";
}
```

Beside each copy is a `.json` note of the same name, a `PictureNote`: the time, the size, the system
and body, the codex region number, the codex entries logged in the ten minutes before it, and the
name of Elite's BMP. The note is made from the game state on the tick the journal reports the
screenshot. The codex page's **Choose a picture** lists this folder, those noted with the entry first.

A copy is made when the journal reports the screenshot, so one taken before D47 started is not
copied. D47 waits up to 120 seconds for Elite to finish writing the BMP, which takes a while for a
high-resolution screenshot, and skips one that does not arrive in that time or is moved away first.
Screenshots are copied one at a time, at their own size.

Commander › Ledgers › Screenshots, after Codex, shows these pictures on the desktop main window: a grid
of tiles, each the whole picture with its system (cyan when it is the system you are in), body, local
date and time, and a yellow CODEX PICTURE tag when a Codex entry's picture was cut from it. **Newest
first** and **Oldest first** order the grid by date taken, a line gives the count and total size, and
**Show in folder** opens the screenshots folder. The grid is listed again each time the page is shown;
its thumbnails are cached in `data\screenshot-thumbnails` and made off the UI thread. With no
screenshots the page says to press Elite's screenshot key or say "take a picture". Pressing a tile opens
the viewer over the whole window: **Previous** and **Next** follow the grid's order and are disabled at
the ends, a mono "n of N" shows the place, **Show in folder** opens Explorer with the file selected,
**Close** puts it away, and under the picture are the place, date and time, pixel size and file size.
Left and Right move and Escape closes.

**Rename** in the viewer opens a dialog with the name in a field and a fixed `.jpg`. **Save** renames
the JPEG and its note, and the date taken stays with the picture, so its place in the grid does not
change. A name that is empty, holds a character Windows refuses, or is already taken shows an error under
the field and renames nothing. A Codex picture cut from the screenshot keeps working and still offers
Re-crop. **Delete** opens a dialog saying the JPEG goes to the Recycle Bin. When the note names an Elite
BMP that is still in Elite's pictures folder, a box, off by default, adds **Also delete Elite's original
BMP**; ticked, the BMP goes to the Recycle Bin too. When Codex pictures are cut from the screenshot the
dialog names up to three of them and counts the rest, and deleting removes them, so each entry shows its
stock picture or placeholder again. After a delete the viewer shows the next picture, or closes after the
last. A file that cannot be renamed or moved to the Recycle Bin leaves everything as it was, and the
dialog shows why.

The row is under Settings › You and your records › Logs and goals, in the Screenshots group, beside **Saved to**, which shows the folder.

Saying "take a picture" makes one: [Screen](screen.md) presses Elite's screenshot key, and the copy and
its note are made here like any other. Pictures that "take a picture" kept in `data\captures` are
moved into this folder, with their notes, the first time D47 starts with this row on.

This capability has no tools. It is separate from the pictures of the window that
[Screen](screen.md) shows the AI model, which are never written to disk.
