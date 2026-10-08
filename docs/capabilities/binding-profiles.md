---
title: Binding profiles
group: Acting on the game
nav_order: 156
---

<!--
  The how-to band. Authoring rules are in the comments on utilities.md and engineers.md.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Save Elite's bindings under a name, and put them back when the hardware on the desk changes.</p>
<section>
<h2><span class="num">1</span> Save the bindings you have.</h2>
<p class="say">Close Elite, then say "save these bindings as sim pit".</p>
<p class="expect">D47 says it saved your bindings as "sim pit", and the name appears under Binding profiles in Settings › Macros and switches.</p>
</section>
<section>
<h2><span class="num">2</span> Load them back.</h2>
<p class="say">With Elite closed, say "load the sim pit bindings".</p>
<p class="expect">D47 says it loaded them, and Elite uses them the next time it starts.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Ask while Elite is running.</p>
<p class="expect">D47 refuses and says to close Elite first: Elite reads its bindings only when it starts.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A named copy of Elite's binding files.</p>
<section>
<h2><span class="num">1</span> A profile is a folder of copies.</h2>
<p class="body">Saving copies every StartPreset file in Elite's Bindings folder and every .binds file those files name into a folder of its own under data\binding-profiles. Loading copies them back.</p>
</section>
<section>
<h2><span class="num">2</span> Nothing is lost by loading.</h2>
<p class="body">Before a load writes anything, the set it replaces is saved as "before last load", so loading the wrong profile is undone by loading that one.</p>
</section>
</div></div>
</details>

## The details

Elite keeps its control bindings in
`%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Bindings`. A `StartPreset.4.start`
file names the preset in use, one line each for general, ship, SRV and on-foot controls, and each
custom preset is a `.binds` file:

```text
Custom
Custom
Custom
Custom
```

Elite reads these files only when it starts, and resets them when a device it expects is missing.
A binding profile is a named copy of the set.

### Ask for it

> "save these bindings as sim pit"
> "load the sim pit bindings"
> "list my binding profiles"

None of these needs an AI configured.

### Save

Saving copies every `StartPreset*.start` file, and every `.binds` file in the Bindings folder that
one of them names, into `data\binding-profiles\<name>\`. Saving under a name that already exists
replaces that profile. A name is letters, digits, spaces, hyphens and underscores, up to forty
characters.

### Load

Loading copies the profile's files back into the Bindings folder, then removes any `StartPreset`
file the profile does not carry, so a higher-numbered one cannot outrank the profile's own. A
profile with no `StartPreset` file is refused.

Before it writes, the set being replaced is saved as **before last load**. Loading that profile
puts the previous bindings back.

### Never while Elite is running

Elite reads its bindings at start and writes them while it runs, so both save and load are refused
while Elite is running, with the reason:

```text
Elite is running. It reads its bindings only when it starts, so close Elite first and ask again.
```

### Deleting one

**Settings › Acting on the game › Macros and switches**, under **Binding profiles**, lists every saved profile with a
**Delete** button. Deleting removes the copy under `data\`; Elite's own files are not touched.
There is no voice command for deleting.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `list_binding_profiles`

Report the names of the Commander's saved binding profiles.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `save_binding_profile`

Save Elite's current control bindings under a name, replacing a profile of that name. Refused
while Elite is running.

```json
{"type":"object","properties":{"name":{"type":"string","description":"What to call the profile: letters, digits, spaces, hyphens and underscores."}},"required":["name"],"additionalProperties":false}
```

#### `load_binding_profile`

Copy a saved binding profile back into Elite's bindings folder, keeping the set it replaces as
"before last load". Refused while Elite is running.

```json
{"type":"object","properties":{"name":{"type":"string","description":"Which saved profile."}},"required":["name"],"additionalProperties":false}
```

</details>
