#!/usr/bin/env python3
"""Regenerate d47's shipped audio cues and thinking beds.

This is *not* part of the build. Its output is committed under `assets/`, and the app
never runs it — it exists so the shipped set is reproducible rather than a pile of
binaries nobody can account for. Run it only when the cue design changes:

    python tools/gen-cues.py

Requires numpy. The recipe is inherited from the generated defaults of two earlier
attempts at this app (directive-47 and COVAS++): 48 kHz mono 16-bit, sine plus a
little harmonic, exponential decay, two-note intervals carrying the meaning. Rising
intervals read as progress, falling ones as trouble, and the beds sit ~16 dB below the
cues so they never compete with speech.

Loop-state cue names are the state ids themselves. That is deliberate: `CueLibrary`
discovers them from disk and asserts one per state, so a state added in code without a
cue fails loudly at startup instead of going wrong as silence nobody notices
(list.md Phase 5, #20). Alert cues are named for the `AlertCue` members under the same
rule (list.md Phase 15).
"""

import os
import wave

import numpy as np

SAMPLE_RATE = 48_000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets")

# Equal temperament, A4 = 440. Named rather than numeric so the intervals below read as
# intervals instead of as arithmetic.
NOTES = {
    "A2": 110.00, "D3": 146.83, "E4": 329.63, "F#4": 369.99, "G4": 392.00,
    "A4": 440.00, "C5": 523.25, "D4": 293.66, "G5": 783.99, "E5": 659.26,
}


def _tone(freq, duration, harmonics=(1.0, 0.18, 0.06), decay=0.14, attack=0.004):
    """One struck note: harmonic stack under an exponential decay.

    The attack ramp is what keeps a cue from clicking. Four milliseconds is inaudible as
    an attack but is ~200 samples of ramp, which is plenty to avoid the discontinuity.
    """
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), endpoint=False)
    wave_out = np.zeros_like(t)
    for i, amp in enumerate(harmonics, start=1):
        wave_out += amp * np.sin(2 * np.pi * freq * i * t)

    envelope = np.exp(-t / decay)
    ramp = int(SAMPLE_RATE * attack)
    envelope[:ramp] *= np.linspace(0, 1, ramp)
    return wave_out * envelope


def _sequence(notes, total, peak=0.5):
    """Notes struck in order, each allowed to ring into the next.

    Overlapping the tails rather than butting the notes together is the difference
    between a chime and a doorbell.
    """
    out = np.zeros(int(SAMPLE_RATE * total))
    step = total / (len(notes) + 1)
    for i, (name, decay) in enumerate(notes):
        start = int(SAMPLE_RATE * step * i)
        tone = _tone(NOTES[name], total - step * i, decay=decay)
        out[start:start + len(tone)] += tone[:len(out) - start]
    return _normalise(out, peak)


def _normalise(signal, peak):
    highest = np.abs(signal).max()
    return signal * (peak / highest) if highest > 0 else signal


def _bed(freq, duration, cycles_am, peak=0.08, depth=0.35):
    """A seamless loop.

    Both the carrier and the amplitude modulation complete a whole number of cycles over
    the buffer, so the last sample joins the first with no discontinuity. Get this wrong
    and the bed ticks once per loop — which sounds like a bug in the audio device rather
    than like a bug in a table of frequencies.
    """
    n = int(SAMPLE_RATE * duration)
    t = np.linspace(0, duration, n, endpoint=False)
    carrier = np.sin(2 * np.pi * freq * t) + 0.30 * np.sin(2 * np.pi * freq * 2 * t)
    am = 1.0 - depth + depth * np.sin(2 * np.pi * (cycles_am / duration) * t)
    return _normalise(carrier * am, peak)


def _ticks(freq, count, duration, gap, peak=0.5):
    out = np.zeros(int(SAMPLE_RATE * duration))
    for i in range(count):
        start = int(SAMPLE_RATE * gap * i)
        tone = _tone(freq, duration - gap * i, harmonics=(1.0, 0.25), decay=0.020)
        out[start:start + len(tone)] += tone[:len(out) - start]
    return _normalise(out, peak)


def _write(relative_path, signal, fade_tail=True):
    path = os.path.normpath(os.path.join(ROOT, relative_path))
    os.makedirs(os.path.dirname(path), exist_ok=True)

    # A hard fade over the last 3 ms on top of whatever envelope produced the signal.
    # Every cue here already decays to near zero, but "near zero" is still a step, and a
    # step at the end of a buffer is a click on some devices and not on others — which is
    # the worst kind of audio bug to be told about second-hand.
    #
    # Beds opt out: they are looped, so their last sample joining their first *is* the
    # design, and fading it to zero would put a dip in the loop once per cycle — the
    # exact artifact _bed's integer-cycle arithmetic exists to prevent.
    signal = signal.copy()
    if fade_tail:
        tail = min(int(SAMPLE_RATE * 0.003), len(signal))
        signal[-tail:] *= np.linspace(1, 0, tail)

    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(np.clip(signal, -1, 1).astype(np.float32).__mul__(32767).astype("<i2").tobytes())

    print(f"{relative_path:44s} {len(signal) / SAMPLE_RATE:5.2f}s")


def main():
    # One cue per loop state. The filename is the state id, and CueLibrary asserts the
    # set matches the LoopState enum exactly.
    _write("cues/idle.wav", _sequence([("A4", 0.13), ("E4", 0.20)], 0.35, peak=0.30))
    _write("cues/listening.wav", _sequence([("E4", 0.11), ("A4", 0.18)], 0.39))
    _write("cues/transcribing.wav", _ticks(NOTES["G4"], 2, 0.15, 0.055))
    _write("cues/thinking.wav", _ticks(NOTES["D4"], 1, 0.12, 0.0, peak=0.38))
    _write("cues/speaking.wav", _ticks(NOTES["E5"], 1, 0.09, 0.0, peak=0.28))
    _write("cues/answered.wav", _sequence([("C5", 0.12), ("G5", 0.19)], 0.40))
    _write("cues/unsure.wav", _sequence([("A4", 0.13), ("F#4", 0.22)], 0.44, peak=0.35))
    _write("cues/failed.wav", _sequence([("E4", 0.14), ("D4", 0.26)], 0.51))

    # One cue per alert. Same rule as the loop states — the filename is the AlertCue member
    # and CueLibrary asserts the set matches the enum — but a different job, so a different
    # sound. These play over the top of whatever was being said, ahead of a warning that has
    # a median of six to eight seconds to be useful in, so they are louder than the loop cues
    # and shorter than the states they interrupt.
    #
    # Every one of them falls. The loop cues use rising intervals for progress and falling for
    # trouble, and all four of these are trouble; what separates them is *which* trouble,
    # carried by the interval rather than by the volume. A Commander should be able to tell a
    # bounty hunter from a pirate without waiting for the sentence.
    _write("alerts/interdiction.wav", _sequence([("A4", 0.09), ("F#4", 0.09), ("D3", 0.30)], 0.55, peak=0.62))
    _write("alerts/piracy.wav", _sequence([("G4", 0.11), ("D4", 0.26)], 0.46, peak=0.52))
    _write("alerts/bountyhunter.wav", _sequence([("C5", 0.07), ("C5", 0.07), ("F#4", 0.26)], 0.52, peak=0.55))
    _write("alerts/rivalterritory.wav", _sequence([("D4", 0.16), ("A2", 0.40)], 0.70, peak=0.34))

    # Beds loop for as long as the state lasts, so they are quiet, low, and seamless.
    # 440 carrier cycles and 3 AM cycles over 3.00 s; 264 and 3 over 2.40 s.
    _write("beds/thinking-hum.wav", _bed(440 / 3, 3.00, cycles_am=3), fade_tail=False)
    _write("beds/thinking-pulse.wav", _bed(110.0, 2.40, cycles_am=3), fade_tail=False)


if __name__ == "__main__":
    main()
