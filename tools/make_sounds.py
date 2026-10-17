#!/usr/bin/env python3
"""Synthesises AVAMailOrderMonsters's sound effects and tunes into ../src/AVAMailOrderMonsters/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect is built here from square, triangle and noise voices, and the
tunes are new compositions: a bouncy shopping jingle for the title and a stomping fight tune for the arena.
Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave



RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAMailOrderMonsters", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out




def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # Weapons.
    write("Claw.wav", mix(tone(0.1, 2600, 900, "noise", attack=0.001, release=1.2), tone(0.08, 300, 140, "square", release=1.0, gain=0.5)), 0.24)
    write("Charge.wav", cat(tone(0.3, 120, 300, "saw", attack=0.01, release=0.2, gain=0.6), mix(tone(0.3, 90, 50, "sine", release=0.8), tone(0.2, 900, 200, "noise", release=1.0, gain=0.5))), 0.36)
    write("Flame.wav", tone(0.35, 900, 500, "noise", attack=0.02, release=0.4, gain=0.6), 0.18)
    write("Laser.wav", tone(0.22, 2400, 600, "square", attack=0.001, vibrato=0.06, vib_rate=60, release=0.8, gain=0.5), 0.2)
    write("Rocket.wav", mix(tone(0.4, 300, 900, "noise", attack=0.01, release=0.5), tone(0.3, 200, 500, "saw", release=0.6, gain=0.4)), 0.24)
    write("Spit.wav", cat(tone(0.06, 500, 900, "square", attack=0.001, release=0.6, gain=0.5), tone(0.1, 1800, 600, "noise", release=1.0, gain=0.5)), 0.2)
    write("Boom.wav", mix(tone(0.7, 2200, 70, "noise", attack=0.001, release=0.7), tone(0.5, 120, 35, "sine", release=0.7)), 0.5)
    write("Hit.wav", mix(tone(0.12, 220, 90, "square", attack=0.001, release=1.0, gain=0.6), tone(0.09, 2000, 500, "noise", release=1.3, gain=0.6)), 0.3)
    write("Thud.wav", mix(tone(0.12, 140, 70, "sine", attack=0.001, release=1.0), tone(0.08, 1200, 300, "noise", release=1.3, gain=0.4)), 0.26)
    write("Crumble.wav", cat(mix(tone(0.9, 1600, 80, "noise", attack=0.002, release=0.5), tone(0.7, 100, 30, "sine", release=0.6)), *[tone(0.05, 300 - i * 30, kind="noise", release=1.0, gain=0.5) for i in range(6)]), 0.46)
    write("Timber.wav", cat(tone(0.25, 700, 300, "noise", attack=0.01, release=0.6, gain=0.5), mix(tone(0.3, 90, 50, "sine", release=0.9), tone(0.2, 600, 150, "noise", release=1.0, gain=0.4))), 0.3)
    write("Swap.wav", cat(tone(0.04, 900, kind="square", release=0.5, gain=0.5), tone(0.05, 1300, kind="square", release=0.5, gain=0.5)), 0.14)
    write("Empty.wav", tone(0.06, 300, 200, "square", release=0.6, gain=0.5), 0.12)
    write("Knockout.wav", cat(*[tone(0.12, hz(n), kind="pulse", release=0.4) for n in ["C5", "G4", "E4", "C4"]], tone(0.5, hz("C3"), kind="pulse", vibrato=0.04, release=0.6)), 0.36)
    write("Bell.wav", mix(tone(1.2, 880, kind="sine", attack=0.001, release=0.5), tone(1.2, 1320, kind="sine", attack=0.001, release=0.6, gain=0.4), tone(0.9, 2210, kind="sine", attack=0.001, release=0.8, gain=0.25)), 0.4)
    write("Roar.wav", mix(tone(1.0, 180, 90, "saw", attack=0.05, vibrato=0.08, vib_rate=18, release=0.5), tone(0.9, 900, 300, "noise", attack=0.05, release=0.6, gain=0.5)), 0.42)
    # The shop.
    write("Cursor.wav", tone(0.025, 1400, kind="square", attack=0.001, release=1.0, gain=0.4), 0.1)
    write("Buy.wav", cat(tone(0.05, hz("E6"), kind="pulse", release=0.3), tone(0.05, hz("A6"), kind="pulse", release=0.3), tone(0.12, hz("C#7"), kind="pulse", release=0.5)), 0.24)
    write("Sell.wav", cat(tone(0.05, hz("A6"), kind="pulse", release=0.3), tone(0.1, hz("E6"), kind="pulse", release=0.5)), 0.22)
    write("Buzz.wav", cat(tone(0.1, 160, kind="square", release=0.2), silence(0.03), tone(0.14, 130, kind="square", release=0.4)), 0.22)
    write("Cash.wav", cat(*[tone(0.05, hz(n), kind="square", release=0.3) for n in ["C6", "E6", "G6", "C7"]], tone(0.4, 4000, 3000, "noise", attack=0.002, release=0.6, gain=0.3)), 0.3)
    write("Promoted.wav", cat(*[tone(0.1, hz(n), kind="pulse", release=0.3) for n in ["C5", "E5", "G5", "C6", "G5", "C6", "E6"]]), 0.34)

    # The title (an original composition): a bouncy catalogue jingle.
    e = 0.5
    jingle = [("C5", e), ("E5", e), ("G5", e), ("A5", e), ("G5", 1), ("E5", 1), ("F5", e), ("A5", e), ("C6", e), ("A5", e), ("G5", 2),
              ("E5", e), ("G5", e), ("C6", e), ("B5", e), ("A5", 1), ("F5", 1), ("G5", e), ("F5", e), ("E5", e), ("D5", e), ("C5", 2)]
    oompah = [("C3", 1), ("G3", 1), ("C3", 1), ("G3", 1), ("F2", 1), ("C3", 1), ("C3", 1), ("G3", 1),
              ("C3", 1), ("G3", 1), ("F2", 1), ("C3", 1), ("G2", 1), ("D3", 1), ("C3", 2)]
    write("TitleTune.wav", cat(tune(132, jingle, oompah, kind="pulse"), silence(0.8)), 0.34)
    # The arena (an original composition): a stomping riff that loops.
    riff = [("E4", e), ("E4", e), ("G4", e), ("E4", e), ("A4", e), ("G4", e), ("E4", 1),
            ("E4", e), ("E4", e), ("G4", e), ("E4", e), ("B4", e), ("A4", e), ("G4", 1),
            ("C5", e), ("B4", e), ("A4", e), ("G4", e), ("A4", e), ("G4", e), ("E4", e), ("D4", e), ("E4", 4)]
    stomp = [("E2", e), ("-", e)] * 12 + [("A2", e), ("-", e)] * 2 + [("E2", e), ("-", e)] * 2
    write("Music.wav", tune(140, riff * 2, stomp * 2, kind="pulse"), 0.24)
    write("Victory.wav", tune(150, [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 1.5), ("B5", 0.5), ("C6", 0.5), ("D6", 0.5), ("E6", 3)],
                              [("C3", 1), ("G2", 1), ("A2", 1), ("F2", 1), ("G2", 1), ("C3", 2.5)], kind="pulse"), 0.4)
    write("GameOver.wav", tune(66, [("G4", 1), ("E4", 1), ("D4", 1), ("B3", 1), ("C4", 4)], [("C3", 2), ("A2", 2), ("G2", 2), ("C2", 2)], kind="tri"), 0.42)


if __name__ == "__main__":
    main()
