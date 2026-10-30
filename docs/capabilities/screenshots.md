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
<p class="say">Open Settings and read the line "Saved to".</p>
<p class="expect">It shows the D47 folder. If Windows syncs your Pictures folder, as OneDrive can, the copies are synced too.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 saves a small copy of each screenshot and leaves Elite's alone.</p>
<section>
<h2><span class="num">1</span> What is converted.</h2>
<p class="body">Each screenshot you take in Elite, including high-resolution ones, is copied as a JPEG. Elite's BMP is never touched.</p>
</section>
<section>
<h2><span class="num">2</span> Where it goes and what it is called.</h2>
<p class="body">The copy goes in a D47 folder inside Elite's own pictures folder, named after the local time, the system and the body, such as 2026-10-12 19-40-03 Shinrarta Dezhra.jpg.</p>
</section>
<section>
<h2><span class="num">3</span> Why only you can switch it.</h2>
<p class="body">It writes files outside D47's data folder, so the switch is yours. The AI model is refused if it tries to change it.</p>
</section>
</div></div>
</details>

## The details

This is the first thing D47 writes into your Pictures folder, so it has a switch of its own, **Convert
Elite's screenshots**, on by default. The setting is `screenshots.convert`. **Privacy and egress**
lists it as **Your screenshots**: the copies are kept on this PC and sent nowhere.

A copy's name comes from the `Name` method of `EliteScreenshot`. A second screenshot in the same
second adds ` (2)`, and characters Windows does not allow in a file name are dropped from the system
and body.

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

A copy is made when the journal reports the screenshot, so one taken before D47 started is not
copied. D47 waits up to two minutes for Elite to finish writing the BMP, which takes a while for a
high-resolution screenshot, and skips one that does not arrive in that time or is moved away first.
Screenshots are copied one at a time, at their own size.

This capability has no tools. It is separate from the pictures of the window that
[Screen](screen.md) shows the AI model, which are never written to disk.
