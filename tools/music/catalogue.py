"""Write catalogue.tsv: one row per track to generate, built from the per-folder briefs below.

Each track's seed is derived from its id, so re-running this gives the same catalogue. A track whose
id appears n times in rejected.tsv takes the seed of "<id>#<n>", so its replacement differs from the
tracks deleted. Changing a brief changes the prompts of tracks not yet generated; tracks already on disk are kept.

Captions name a melody, its accompaniment and a mood, with no negations. Lyrics carry
instrumental section tags only: no [Verse] or [Chorus], which invite singing.
"""

import csv
import hashlib
import os
import random
import sys


PRODUCTION = "rich chord progression, lush hall reverb, polished cinematic production"

MINOR = ["C minor", "D minor", "E minor", "F minor", "G minor", "A minor", "B minor"]
KEYS = MINOR + ["C major", "D major", "E major", "F major", "G major", "A major", "Bb major", "Eb major"]

# Combat folders: minor keys only, and a heavier mix than PRODUCTION.
COMBAT = {"combat-dogfight", "combat-large", "combat-unknown", "combat-srv", "interdiction", "cqc", "capital-ship"}
COMBAT_PRODUCTION = "dark minor harmony, punchy low end, powerful cinematic mix"

# Section shapes. {lead} and {second} are filled with the track's lead and second instruments.
GENTLE = [
    "[Intro - {second}]\n\n[Main Theme - {lead}]\n\n[Interlude - {second}]\n\n[Main Theme - {lead}]\n\n[Outro - fade out]",
    "[Intro - ambient]\n\n[Main Theme - {lead}]\n\n[Variation - {second}]\n\n[Main Theme - {lead}]\n\n[Outro - fade out]",
    "[Intro - {second}]\n\n[Main Theme - {lead}]\n\n[Breakdown]\n\n[Main Theme - {lead}]\n\n[Outro - fade out]",
]
DRIVING = [
    "[Intro - {second}]\n\n[Main Theme - {lead}]\n\n[Build]\n\n[Climax - full orchestra]\n\n[Main Theme - {lead}]\n\n[Outro]",
    "[Intro - percussion]\n\n[Main Theme - {lead}]\n\n[Build - {second}]\n\n[Climax - powerful]\n\n[Outro]",
]
GRAND = [
    "[Intro - {second}]\n\n[Main Theme - {lead}]\n\n[Build - {second}]\n\n[Climax - full orchestra]\n\n[Main Theme - {lead}]\n\n[Outro - fade out]",
]
CUE = ["[Intro - {second}]\n\n[Main Theme - {lead}]\n\n[Outro]"]

# folder: (count, (min s, max s), (min bpm, max bpm), style, [moods], [(lead, second, accompaniment)], shapes)
BRIEFS = {
    "supercruise": (215, (150, 240), (70, 100), "cinematic space soundtrack",
        ["hopeful and wondrous", "calm and adventurous", "nostalgic", "serene and majestic", "bittersweet", "warm and optimistic"],
        [("soaring string melody", "warm synth pads", "pulsing synth arpeggios, soft orchestral percussion"),
         ("expressive piano melody", "string ensemble", "gentle synth pulse, low cello"),
         ("lyrical french horn melody", "shimmering synth", "string ostinato, soft timpani"),
         ("lead synth melody", "analog pads", "arpeggiated bass, light electronic drums, string layers"),
         ("solo cello melody", "harp", "warm pads, soft strings")], GENTLE),
    "normal-space": (210, (150, 240), (60, 90), "atmospheric space soundtrack",
        ["mysterious", "contemplative", "lonely and beautiful", "curious", "eerie but calm", "awe-struck"],
        [("haunting piano melody", "deep synth drones", "slow strings, distant percussion"),
         ("solo cello melody", "granular synth textures", "low strings, soft bells"),
         ("glassy synth melody", "ambient pads", "sub bass, slow pulse"),
         ("celesta melody", "string ensemble", "warm pads, soft timpani"),
         ("flute melody", "dark synth pads", "harp, low strings")], GENTLE),
    "docked": (185, (150, 240), (75, 110), "space station lounge soundtrack",
        ["relaxed", "warm and welcoming", "content", "laid-back", "gently busy", "cosy"],
        [("electric piano melody", "soft synth pads", "smooth bass, brushed drums"),
         ("vibraphone melody", "muted guitar", "jazzy bass, light percussion"),
         ("mellow lead synth", "warm keys", "lofi beat, round bass"),
         ("marimba melody", "strings", "light percussion, soft bass"),
         ("saxophone melody", "rhodes piano", "upright bass, brushed drums")], GENTLE),
    "main-menu": (95, (150, 240), (70, 100), "epic space film score main theme",
        ["majestic", "heroic and hopeful", "wistful", "awe-inspiring", "adventurous"],
        [("soaring string melody", "french horns", "full orchestra, timpani, synth arpeggios"),
         ("piano theme", "string ensemble", "warm synth pads, orchestral percussion"),
         ("heroic brass melody", "strings", "big drums, synth layers"),
         ("lead synth melody", "orchestra", "pulsing bass, cinematic drums")], GRAND),
    "on-foot": (90, (150, 240), (80, 115), "tense sci-fi exploration soundtrack",
        ["alert", "covert", "curious", "focused", "industrial"],
        [("muted synth melody", "low strings", "ticking percussion, pulsing bass"),
         ("plucked synth motif", "dark pads", "electronic drums, sub bass"),
         ("piano motif", "synth textures", "steady pulse, light percussion"),
         ("guitar motif", "ambient pads", "minimal electronic beat")], GENTLE),
    "galaxy-map": (75, (150, 240), (60, 90), "cosmic ambient soundtrack",
        ["dreamy", "infinite wonder", "reflective", "starlit", "peaceful"],
        [("celesta melody", "shimmering pads", "soft strings, harp"),
         ("piano melody", "glassy synth arpeggios", "warm pads, low strings"),
         ("soft lead synth melody", "string ensemble", "slow pulse, bells"),
         ("harp melody", "airy pads", "cello drone, soft chimes")], GENTLE),
    "combat-dogfight": (70, (120, 210), (125, 160), "dark hybrid orchestral space battle music",
        ["menacing", "relentless", "desperate", "brutal", "high intensity"],
        [("low brass melody", "driving staccato strings", "pounding taiko, war drums, distorted synth bass"),
         ("aggressive staccato string motif", "brass stabs", "heavy cinematic drums, deep sub bass"),
         ("dark horn theme", "string ostinato", "thunderous percussion, gritty synth bass"),
         ("heavy distorted guitar riff", "low brass", "hard-hitting drums, orchestral hits")], DRIVING),
    "docking-computer": (70, (120, 200), (80, 120), "elegant classical waltz",
        ["graceful", "playful", "elegant", "light-hearted", "charming"],
        [("violin melody", "harp", "string quartet, light woodwinds"),
         ("flute melody", "pizzicato strings", "chamber orchestra"),
         ("piano melody", "strings", "gentle triple meter accompaniment"),
         ("clarinet melody", "string ensemble", "soft horns, harp")], GENTLE),
    "combat-large": (50, (150, 240), (115, 150), "epic large-scale battle film score",
        ["epic", "overwhelming", "triumphant struggle", "grim", "heroic"],
        [("heroic brass melody", "string ensemble","massive drums, string runs"),
         ("soaring string melody", "brass", "taiko, synth risers, heavy percussion"),
         ("low brass theme", "aggressive strings", "timpani, war drums")], DRIVING),
    "fleet-carrier": (50, (150, 240), (70, 100), "proud space command soundtrack",
        ["proud", "steady", "industrious", "confident", "spacious"],
        [("noble brass melody", "strings", "synth pulse, measured percussion"),
         ("piano melody", "warm pads", "mechanical rhythm, deep bass"),
         ("string melody", "soft horns", "slow march percussion")], GENTLE),
    "combat-unknown": (45, (120, 210), (100, 140), "alien threat battle soundtrack",
        ["dread", "alien hostility", "panic", "dissonant tension"],
        [("dissonant string motif", "alien synth", "pounding drums, sub bass"),
         ("menacing brass motif", "metallic textures", "heavy percussion"),
         ("eerie synth motif", "string clusters", "driving drums")], DRIVING),
    "system-map": (45, (150, 240), (65, 95), "calm sci-fi interface soundtrack",
        ["analytical", "calm curiosity", "orderly", "gentle wonder"],
        [("clean synth melody", "soft pads", "light arpeggios, soft bass"),
         ("plucked synth melody", "strings", "bells, light percussion"),
         ("piano melody", "warm synth", "minimal pulse")], GENTLE),
    "scanner": (45, (150, 240), (65, 100), "sci-fi discovery soundtrack",
        ["focused", "searching", "discovery", "curious"],
        [("glassy synth melody", "ambient pads", "sonar-like pulses, soft bass"),
         ("piano motif", "low drones", "light percussion"),
         ("synth arpeggio melody", "strings", "electronic textures")], GENTLE),
    "squadrons": (30, (150, 240), (75, 105), "warm space camaraderie soundtrack",
        ["camaraderie", "proud", "warm", "loyal"],
        [("brass melody", "strings", "light drums"), ("piano melody", "strings", "soft march percussion")], GENTLE),
    "arrival-from-supercruise": (20, (20, 45), (70, 100), "short cinematic arrival cue",
        ["arrival", "relief", "anticipation resolved"],
        [("string swell melody", "brass chord", "synth shimmer"), ("piano phrase", "pads", "soft timpani")], CUE),
    "guardian-sites": (20, (150, 240), (55, 85), "ancient alien ruins soundtrack",
        ["ancient", "sacred", "mournful", "haunting"],
        [("ethereal flute melody", "low drones", "resonant chimes, deep strings"),
         ("solo cello melody", "ancient textures", "bells, soft percussion")], GENTLE),
    "powerplay": (20, (150, 240), (70, 100), "political intrigue soundtrack",
        ["scheming", "powerful", "imperial", "tense"],
        [("low string melody", "brass", "measured percussion"), ("piano melody", "dark synth pads", "timpani")], GENTLE),
    "arrival-from-hyperspace": (20, (20, 45), (70, 100), "short cinematic arrival cue after a jump",
        ["new star", "wonder", "arrival"],
        [("shimmering synth phrase", "string chord", "deep boom"), ("brass swell", "harp", "pads")], CUE),
    "unknown-encounter": (15, (120, 210), (60, 100), "uneasy alien encounter soundtrack",
        ["alien presence", "uncanny", "dread"],
        [("eerie synth melody", "bowed metal", "sub drones"), ("dissonant string motif", "alien textures", "low pulse")], GENTLE),
    "codex": (15, (120, 180), (65, 95), "calm archival soundtrack",
        ["learning", "curious", "reflective"],
        [("soft piano melody", "pads", "light bells"), ("clean synth melody", "strings", "soft pulse")], GENTLE),
    "combat-srv": (10, (120, 200), (115, 145), "gritty ground combat soundtrack",
        ["gritty", "urgent"],
        [("electric guitar riff", "brass hits", "rock drums, distorted bass"),
         ("aggressive synth melody", "staccato strings", "hard drums")], DRIVING),
    "unknown-exploration": (10, (150, 240), (55, 85), "alien mystery soundtrack",
        ["alien mystery", "unsettling wonder"],
        [("strange synth melody", "eerie pads", "low strings, odd textures")], GENTLE),
    "fog-cloud": (10, (120, 210), (55, 85), "otherworldly life soundtrack",
        ["otherworldly", "hazy wonder"],
        [("breathy flute melody", "organic textures", "soft bells, warm pads")], GENTLE),
    "interdiction": (10, (45, 90), (125, 150), "sudden space danger cue",
        ["alarm", "tension"],
        [("rising string motif", "brass stabs", "pounding drums, synth alarm tones")], DRIVING),
    "cqc-menu": (10, (120, 200), (100, 130), "arena electronic soundtrack",
        ["competitive", "energetic"],
        [("lead synth melody", "synth stabs", "electronic beat, synth bass")], DRIVING),
    "cqc": (10, (120, 200), (125, 150), "fast arena combat soundtrack",
        ["competitive", "intense"],
        [("aggressive lead synth", "orchestral hits", "electronic drums, driving bass")], DRIVING),
    "capital-ship": (10, (150, 240), (80, 120), "naval space warfare film score",
        ["imposing", "military"],
        [("low brass theme", "strings", "war drums"), ("string melody", "brass", "orchestral march, deep synth")], DRIVING),
    "unknown-settlement": (10, (150, 240), (55, 85), "abandoned outpost soundtrack",
        ["abandoned", "eerie quiet"],
        [("sparse piano melody", "wind textures", "low drones")], GENTLE),
    "general": (20, (150, 240), (65, 100), "cinematic space soundtrack",
        ["calm", "spacious", "wistful", "hopeful"],
        [("piano melody", "warm pads", "soft strings"), ("synth melody", "strings", "light pulse, cello")], GENTLE),
}


def seed_of(track_id):
    return int(hashlib.sha256(track_id.encode()).hexdigest()[:8], 16)


def short(instrument):
    """The instrument named in a section tag: 'soaring string melody' becomes 'strings'."""
    for word, tag in [("string", "strings"), ("piano", "piano"), ("horn", "horns"), ("brass", "brass"),
                      ("synth", "synth"), ("cello", "cello"), ("flute", "flute"), ("harp", "harp"),
                      ("celesta", "celesta"), ("guitar", "guitar"), ("violin", "violin"), ("clarinet", "clarinet"),
                      ("vibraphone", "vibraphone"), ("marimba", "marimba"), ("saxophone", "saxophone"),
                      ("rhodes", "keys"), ("keys", "keys"), ("pads", "pads"), ("orchestra", "orchestra")]:
        if word in instrument:
            return tag
    return "ambient"


def rejected_counts(data):
    """How many times each id appears in rejected.tsv; empty when the file is missing."""
    path = os.path.join(data, "rejected.tsv")
    counts = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            for line in f:
                track_id = line.strip()
                if track_id:
                    counts[track_id] = counts.get(track_id, 0) + 1
    return counts


def rows(rejected=None):
    rejected = rejected or {}
    for folder, (count, dur, bpm, style, moods, voicings, shapes) in BRIEFS.items():
        for n in range(1, count + 1):
            track_id = f"{folder}-{n:03d}"
            times = rejected.get(track_id, 0)
            seed = seed_of(f"{track_id}#{times}" if times else track_id)
            rng = random.Random(seed)
            mood = rng.choice(moods)
            lead, second, accompaniment = rng.choice(voicings)
            combat = folder in COMBAT
            caption = f"{style}, {lead}, {second}, {accompaniment}, {mood}, {COMBAT_PRODUCTION if combat else PRODUCTION}"
            lyrics = rng.choice(shapes).format(lead=short(lead), second=short(second))
            yield {"id": track_id, "folder": folder, "n": n, "seed": seed,
                   "duration": rng.randrange(dur[0], dur[1] + 1, 5), "bpm": rng.randint(*bpm),
                   "key": rng.choice(MINOR if combat else KEYS), "prompt": caption, "lyrics": lyrics.replace("\n", "\\n")}


if __name__ == "__main__":
    data = os.environ.get("D47_MUSIC_SOURCE")
    if not data:
        sys.exit("D47_MUSIC_SOURCE is not set.")
    out = os.path.join(data, "catalogue.tsv")
    with open(out, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["id", "folder", "n", "seed", "duration", "bpm", "key", "prompt", "lyrics"],
                           delimiter="\t")
        w.writeheader()
        total = 0
        for r in rows(rejected_counts(data)):
            w.writerow(r)
            total += 1
    print(f"{total} tracks -> {out}")
