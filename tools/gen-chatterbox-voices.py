#!/usr/bin/env python3
"""Pick the shipped Chatterbox reference clips from a local LibriTTS-R download (#752).

Not part of the build and never run by CI. Its output, `assets/voices/chatterbox/*.wav` and
`voices.tsv`, is committed.

    python tools/gen-chatterbox-voices.py C:\\datasets\\LibriTTS_R\\train-clean-360

LibriTTS-R (openslr.org, resource 141) is CC BY 4.0. The argument is the `train-clean-360`
folder. The script takes the 12 speakers with the most utterances between 5.0 and 7.0 s, 6 female
and 6 male, and copies one such utterance from each. The utterances are already 24 kHz mono
16-bit.

The download carries no speaker metadata, so gender is read from the median fundamental
frequency of the speaker's clips. A speaker whose median falls between FEMALE_MIN_HZ and
MALE_MAX_HZ is skipped. Listen to the output before committing it.

Requires numpy.
"""

import argparse
import os
import shutil
import wave

import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUTPUT_DIR = os.path.join(ROOT, "assets", "voices", "chatterbox")

MIN_SECONDS = 5.0
MAX_SECONDS = 7.0
PER_GENDER = 6
FEMALE_MIN_HZ = 170.0
MALE_MAX_HZ = 140.0

FEMALE_NAMES = ["Marlow", "Isolde", "Tamsin", "Verity", "Odette", "Linnea"]
MALE_NAMES = ["Corwin", "Alder", "Benedict", "Tobias", "Emrys", "Lucan"]

ROLES = {
    "female": ["ShipAi", "Comms", "CarrierCaptain", "", "", ""],
    "male": ["ShipAi", "Narrator", "TowerControl", "Crew", "", ""],
}


def duration(path):
    with wave.open(path) as w:
        if w.getframerate() != 24000 or w.getnchannels() != 1 or w.getsampwidth() != 2:
            return None
        return w.getnframes() / w.getframerate()


def median_pitch(path):
    """Median autocorrelation pitch of the voiced frames, in Hz."""
    with wave.open(path) as w:
        samples = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64)
        rate = w.getframerate()

    frame = int(0.04 * rate)
    low, high = int(rate / 400), int(rate / 60)
    pitches = []

    for start in range(0, len(samples) - frame, frame // 2):
        chunk = samples[start:start + frame]
        chunk = chunk - chunk.mean()
        energy = float(np.dot(chunk, chunk))

        if energy < 1e6:
            continue

        corr = np.correlate(chunk, chunk, mode="full")[frame - 1:]
        lag = low + int(np.argmax(corr[low:high]))

        if corr[lag] > 0.5 * corr[0]:
            pitches.append(rate / lag)

    return float(np.median(pitches)) if pitches else None


def candidates(folder):
    """{speaker: [(utterance, path)]} for the clips of the right length and format."""
    found = {}

    for speaker in sorted(os.listdir(folder)):
        speaker_dir = os.path.join(folder, speaker)

        if not os.path.isdir(speaker_dir):
            continue

        for dirpath, _, files in os.walk(speaker_dir):
            for name in sorted(files):
                if not name.endswith(".wav"):
                    continue

                path = os.path.join(dirpath, name)
                seconds = duration(path)

                if seconds is not None and MIN_SECONDS <= seconds <= MAX_SECONDS:
                    found.setdefault(speaker, []).append((name[:-4], path))

    return found


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("folder", help="the train-clean-360 folder")
    folder = parser.parse_args().folder

    found = candidates(folder)
    ranked = sorted(found, key=lambda speaker: (-len(found[speaker]), speaker))
    chosen = {"female": [], "male": []}

    for speaker in ranked:
        if all(len(group) == PER_GENDER for group in chosen.values()):
            break

        utterance, path = found[speaker][0]
        hz = median_pitch(path)

        if hz is None:
            continue

        gender = "female" if hz >= FEMALE_MIN_HZ else "male" if hz <= MALE_MAX_HZ else None

        if gender and len(chosen[gender]) < PER_GENDER:
            chosen[gender].append((speaker, utterance, path, hz))

    for gender, group in chosen.items():
        if len(group) < PER_GENDER:
            raise SystemExit(f"Only {len(group)} {gender} speakers qualified.")

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    for name in os.listdir(OUTPUT_DIR):
        os.remove(os.path.join(OUTPUT_DIR, name))

    rows = ["id\tname\tgender\tlocale\trole\tsource"]

    for gender, names in (("female", FEMALE_NAMES), ("male", MALE_NAMES)):
        for index, (speaker, utterance, path, hz) in enumerate(chosen[gender]):
            voice_id = f"{names[index].lower()}"
            shutil.copyfile(path, os.path.join(OUTPUT_DIR, f"{voice_id}.wav"))
            source = f"LibriTTS-R speaker {speaker}, utterance {utterance}, CC BY 4.0"
            rows.append("\t".join(
                [voice_id, names[index], gender, "en", ROLES[gender][index], source]))
            print(f"{voice_id}: speaker {speaker}, {hz:.0f} Hz")

    with open(os.path.join(OUTPUT_DIR, "voices.tsv"), "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(rows) + "\n")


if __name__ == "__main__":
    main()
