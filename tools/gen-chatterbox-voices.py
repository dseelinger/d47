#!/usr/bin/env python3
"""Build the Chatterbox voice catalog from a local LibriTTS-R download (#752, #926).

Not part of the build and never run by CI. Its output, `assets/voices/chatterbox/catalog.tsv`, is
committed; the clips go to an output folder outside the repository, to be published with
`tools/publish-chatterbox-voices.ps1`. `voices.tsv` and the 12 shipped clips are read, never
written.

    python tools/gen-chatterbox-voices.py C:\\datasets\\LibriTTS_R\\train-clean-360 C:\\datasets\\chatterbox-voices ^
        --speakers speakers.tsv --failed train-clean-360_bad_sample_list.txt --names baby-names.csv

LibriTTS-R (openslr.org, resource 141) is CC BY 4.0. Inputs:

- the `train-clean-360` folder;
- `--speakers`: `LibriTTS_R/speakers.tsv` from `doc.tar.gz` (openslr 141), the source of gender;
- `--failed`: `train-clean-360_bad_sample_list.txt` from
  `libritts_r_failed_speech_restoration_examples.tar.gz` (openslr 141); those utterances are never
  chosen;
- `--names`: Social Security Administration baby names as CSV with the columns year, name,
  percent, sex (boy or girl), for example hadley/data-baby-names `baby-names.csv`.

Every speaker with an utterance of 5.0 to 7.0 s becomes a voice with id `lt<speaker>`. The
utterance is the one whose voiced frames cover the largest share of its length. `pitch` and `pace`
are terciles within the speaker's gender, of the median pitch and of the median characters per
second over the speaker's eligible utterances. A voice already in `voices.tsv` keeps its id, name,
role and utterance. Names are drawn from the SSA list with a fixed seed, so a re-run gives every
speaker the same name.

Requires numpy.
"""

import argparse
import csv
import hashlib
import os
import random
import re
import shutil
import wave
from collections import defaultdict

import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
VOICES_DIR = os.path.join(ROOT, "assets", "voices", "chatterbox")

MIN_SECONDS = 5.0
MAX_SECONDS = 7.0
NAME_SEED = 926
NAMES_PER_GENDER = 1500
SOURCE = "LibriTTS-R speaker {speaker}, utterance {utterance}, CC BY 4.0"
COLUMNS = ["id", "name", "gender", "locale", "pitch", "pace", "role", "source", "sha256", "bytes"]
PITCH_BANDS = ["low", "mid", "high"]
PACE_BANDS = ["slow", "even", "brisk"]


def duration(path):
    with wave.open(path) as w:
        if w.getframerate() != 24000 or w.getnchannels() != 1 or w.getsampwidth() != 2:
            return None
        return w.getnframes() / w.getframerate()


def pitch_and_voicing(path):
    """Median autocorrelation pitch of the voiced frames in Hz, and the share of frames voiced."""
    with wave.open(path) as w:
        samples = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64)
        rate = w.getframerate()

    frame = int(0.04 * rate)
    low, high = int(rate / 400), int(rate / 60)
    pitches = []
    frames = 0

    for start in range(0, len(samples) - frame, frame // 2):
        frames += 1
        chunk = samples[start:start + frame]
        chunk = chunk - chunk.mean()
        energy = float(np.dot(chunk, chunk))

        if energy < 1e6:
            continue

        spectrum = np.fft.rfft(chunk, 2 * frame)
        corr = np.fft.irfft(spectrum * np.conj(spectrum))[:frame]
        lag = low + int(np.argmax(corr[low:high]))

        if corr[lag] > 0.5 * corr[0]:
            pitches.append(rate / lag)

    if not pitches:
        return None, 0.0

    return float(np.median(pitches)), len(pitches) / frames


def characters_per_second(path, seconds):
    text_path = path[:-4] + ".normalized.txt"

    if not os.path.exists(text_path):
        return None

    with open(text_path, encoding="utf-8") as text:
        return len(text.read().strip()) / seconds


def read_failed(path):
    with open(path, encoding="utf-8") as failed:
        return {os.path.basename(line.strip())[:-4] for line in failed if line.strip().endswith(".wav")}


def read_genders(path):
    """{speaker: 'female' | 'male'} from LibriTTS-R speakers.tsv."""
    genders = {}

    with open(path, encoding="utf-8") as table:
        for row in csv.reader(table, delimiter="\t"):
            if len(row) >= 2 and row[0].isdigit() and row[1] in ("F", "M"):
                genders[row[0]] = "female" if row[1] == "F" else "male"

    return genders


def read_name_pools(path):
    """{gender: [name]}, the most common SSA names first, a name in the gender where it is commoner."""
    totals = {"female": defaultdict(float), "male": defaultdict(float)}

    with open(path, encoding="utf-8", newline="") as table:
        for row in csv.DictReader(table):
            gender = "female" if row["sex"] == "girl" else "male"
            totals[gender][row["name"]] += float(row["percent"])

    pools = {}

    for gender, other in (("female", "male"), ("male", "female")):
        names = [name for name, total in totals[gender].items()
                 if re.fullmatch(r"[A-Z][a-z]{2,9}", name) and total > totals[other].get(name, 0.0)]
        names.sort(key=lambda name: (-totals[gender][name], name))
        pools[gender] = names[:NAMES_PER_GENDER]

    return pools


def read_shipped():
    """The rows of voices.tsv, as dicts."""
    with open(os.path.join(VOICES_DIR, "voices.tsv"), encoding="utf-8", newline="") as table:
        return list(csv.DictReader(table, delimiter="\t"))


def speaker_of(source):
    return re.search(r"speaker (\d+)", source).group(1)


def utterance_of(source):
    return re.search(r"utterance (\S+?),", source).group(1)


def survey(folder, failed):
    """{speaker: [(utterance, path, seconds)]} for the clips of the right length and format."""
    found = defaultdict(list)

    for speaker in sorted(os.listdir(folder), key=lambda name: (len(name), name)):
        speaker_dir = os.path.join(folder, speaker)

        if not os.path.isdir(speaker_dir):
            continue

        for dirpath, _, files in os.walk(speaker_dir):
            for name in sorted(files):
                if not name.endswith(".wav") or name[:-4] in failed:
                    continue

                path = os.path.join(dirpath, name)
                seconds = duration(path)

                if seconds is not None and MIN_SECONDS <= seconds <= MAX_SECONDS:
                    found[speaker].append((name[:-4], path, seconds))

    return found


def terciles(values, labels):
    """{key: label} splitting the keys, ordered by value then key, into three equal groups."""
    ordered = sorted(values, key=lambda key: (values[key], key))
    return {key: labels[index * 3 // len(ordered)] for index, key in enumerate(ordered)}


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("folder", help="the train-clean-360 folder")
    parser.add_argument("output", help="the folder the clips are written to, outside the repository")
    parser.add_argument("--speakers", required=True)
    parser.add_argument("--failed", required=True)
    parser.add_argument("--names", required=True)
    args = parser.parse_args()

    root = os.path.abspath(ROOT)

    if os.path.commonpath([os.path.abspath(args.output), root]) == root:
        raise SystemExit("The output folder must be outside the repository.")

    genders = read_genders(args.speakers)
    pools = read_name_pools(args.names)
    shipped = {speaker_of(row["source"]): row for row in read_shipped()}
    found = survey(args.folder, read_failed(args.failed))

    voices = {}

    for speaker, clips in found.items():
        if speaker not in genders:
            print(f"speaker {speaker}: no gender in the metadata; left out")
            continue

        pitches, paces, best = [], [], None
        row = shipped.get(speaker)

        for utterance, path, seconds in clips:
            hz, voiced = pitch_and_voicing(path)
            pace = characters_per_second(path, seconds)

            if hz is None or pace is None:
                continue

            pitches.append(hz)
            paces.append(pace)

            if row is not None:
                if utterance == utterance_of(row["source"]):
                    best = (voiced, utterance, path)
            elif best is None or voiced > best[0]:
                best = (voiced, utterance, path)

        if best is None:
            continue

        voices[speaker] = {
            "gender": genders[speaker],
            "pitch": float(np.median(pitches)),
            "pace": float(np.median(paces)),
            "utterance": best[1],
            "path": best[2],
        }

    for speaker, row in shipped.items():
        if speaker not in voices:
            raise SystemExit(f"Shipped voice {row['id']} (speaker {speaker}) has no usable clip.")

        if voices[speaker]["gender"] != row["gender"]:
            print(f"{row['id']}: voices.tsv says {row['gender']}, the metadata says {voices[speaker]['gender']}")
            voices[speaker]["gender"] = row["gender"]

    rows = []
    taken = {row["name"] for row in shipped.values()}

    for gender in ("female", "male"):
        group = {speaker: voice for speaker, voice in voices.items() if voice["gender"] == gender}
        pitch = terciles({speaker: voice["pitch"] for speaker, voice in group.items()}, PITCH_BANDS)
        pace = terciles({speaker: voice["pace"] for speaker, voice in group.items()}, PACE_BANDS)

        pool = [name for name in pools[gender] if name not in taken]
        random.Random(NAME_SEED).shuffle(pool)
        fresh = [speaker for speaker in sorted(group, key=int) if speaker not in shipped]

        if len(fresh) > len(pool):
            raise SystemExit(f"Only {len(pool)} {gender} names for {len(fresh)} speakers.")

        names = dict(zip(fresh, pool))

        for speaker in sorted(group, key=int):
            voice = group[speaker]
            existing = shipped.get(speaker)
            rows.append({
                "id": existing["id"] if existing else f"lt{speaker}",
                "name": existing["name"] if existing else names[speaker],
                "gender": gender,
                "locale": "en",
                "pitch": pitch[speaker],
                "pace": pace[speaker],
                "role": existing["role"] if existing else "",
                "source": existing["source"] if existing else SOURCE.format(speaker=speaker, utterance=voice["utterance"]),
                "clip": voice["path"],
            })

    os.makedirs(args.output, exist_ok=True)

    for name in os.listdir(args.output):
        os.remove(os.path.join(args.output, name))

    for row in rows:
        target = os.path.join(args.output, row["id"] + ".wav")
        shutil.copyfile(row["clip"], target)

        with open(target, "rb") as clip:
            data = clip.read()

        row["sha256"] = hashlib.sha256(data).hexdigest()
        row["bytes"] = len(data)

    rows.sort(key=lambda row: (row["gender"], row["id"]))

    with open(os.path.join(VOICES_DIR, "catalog.tsv"), "w", encoding="utf-8", newline="\n") as out:
        out.write("\t".join(COLUMNS) + "\n")

        for row in rows:
            out.write("\t".join(str(row[column]) for column in COLUMNS) + "\n")

    print(f"{len(rows)} voices: {sum(row['gender'] == 'female' for row in rows)} female, "
          f"{sum(row['gender'] == 'male' for row in rows)} male; "
          f"{sum(row['bytes'] for row in rows) / 1e6:.0f} MB in {args.output}")


if __name__ == "__main__":
    main()
