#!/usr/bin/env python3
"""Generate and validate the original Just Some Stars Task 29 audio package.

The score is deliberately authored from deterministic oscillators, filtered noise,
and physically inspired plucks. It uses no external recordings or musical works.
The generator publishes atomically and records exact content hashes so a failed
rerun cannot leave a half-written canonical media package.
"""

from __future__ import annotations

import argparse
import array
import csv
import hashlib
import json
import math
import os
import random
import shutil
import struct
import sys
import tempfile
import wave
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AUDIO_ROOT = ROOT / "Assets/_JustSomeStars/Audio"
MUSIC_ROOT = AUDIO_ROOT / "Music"
SFX_ROOT = AUDIO_ROOT / "SFX"
MANIFEST_PATH = AUDIO_ROOT / "task29-audio-manifest.json"
RIGHTS_PATH = ROOT / "docs/media/media-rights-ledger.csv"
SAMPLE_RATE = 44_100
SCHEMA_VERSION = 1
SIGNAL_INTERVALS = (0, 7, 10, 14, 19)
FACE_ATLAS_CHARACTERS = ("Captain", "Mira", "Juno", "Kai", "Bea", "Ori")


@dataclass(frozen=True)
class ScoreSpec:
    slug: str
    cue_id: str
    state_id: str
    bpm: float
    root_midi: int
    mode: tuple[int, ...]
    progression: tuple[int, ...]
    signal_level: float
    warmth: float


SCORES = (
    ScoreSpec("clubhouse-before-dinner", "cue.clubhouse.before-dinner",
              "music.clubhouse", 78.0, 48, (0, 2, 4, 5, 7, 9, 11),
              (0, 4, 5, 3, 0, 4, 1, 5), 0.10, 0.92),
    ScoreSpec("mirra-warm-cold-horizon", "cue.mirra.horizon",
              "music.mirra", 72.0, 50, (0, 2, 4, 6, 7, 9, 11),
              (0, 3, 6, 4, 0, 3, 1, 4), 0.22, 0.72),
    ScoreSpec("koro-vesper-orbit", "cue.koro-vesper.orbit",
              "music.koro-vesper", 86.0, 52, (0, 2, 3, 5, 7, 9, 10),
              (0, 5, 3, 6, 0, 5, 1, 4), 0.34, 0.48),
    ScoreSpec("aster-veil-signal", "cue.aster-veil.signal",
              "music.aster-veil", 68.0, 54, (0, 2, 3, 5, 7, 8, 10),
              (0, 6, 3, 5, 0, 6, 1, 4), 0.58, 0.30),
    ScoreSpec("dinner-homecoming", "cue.dinner.homecoming",
              "music.dinner", 74.0, 48, (0, 2, 4, 5, 7, 9, 11),
              (0, 3, 4, 0, 5, 3, 1, 0), 0.82, 0.88),
)

SFX_SPECS = (
    ("footstep-soil", "cue.sfx.footstep.soil", 0.24, 101),
    ("footstep-ice", "cue.sfx.footstep.ice", 0.28, 102),
    ("tool-attach", "cue.sfx.tool.attach", 0.32, 103),
    ("tool-detach", "cue.sfx.tool.detach", 0.30, 104),
    ("signal-pulse", "cue.sfx.signal.pulse", 0.85, 105),
    ("lens-focus", "cue.sfx.lens.focus", 0.62, 106),
    ("interaction-release", "cue.sfx.interaction.release", 0.40, 107),
    ("ui-positive", "cue.sfx.ui.positive", 0.36, 108),
)


def midi_frequency(note: int) -> float:
    return 440.0 * (2.0 ** ((note - 69) / 12.0))


def soft_clip(value: float) -> float:
    return math.tanh(value * 1.18) / math.tanh(1.18)


def pan(sample: float, position: float) -> tuple[float, float]:
    position = max(-1.0, min(1.0, position))
    angle = (position + 1.0) * math.pi * 0.25
    return sample * math.cos(angle), sample * math.sin(angle)


def periodic_phase(seconds: float, frequency: float) -> float:
    return math.tau * frequency * seconds


def pluck(seconds_since: float, frequency: float, decay: float) -> float:
    if seconds_since < 0.0 or seconds_since > 2.8:
        return 0.0
    envelope = math.exp(-seconds_since * decay)
    phase = periodic_phase(seconds_since, frequency)
    return envelope * (math.sin(phase) + 0.34 * math.sin(phase * 2.01) +
                       0.13 * math.sin(phase * 3.98)) / 1.47


def bell(seconds_since: float, frequency: float) -> float:
    if seconds_since < 0.0 or seconds_since > 3.5:
        return 0.0
    envelope = math.exp(-seconds_since * 1.45)
    phase = periodic_phase(seconds_since, frequency)
    return envelope * (math.sin(phase) + 0.38 * math.sin(phase * 2.71) +
                       0.16 * math.sin(phase * 4.09)) / 1.54


def _crossfade_loop(left: array.array, right: array.array, seconds: float) -> None:
    count = min(int(SAMPLE_RATE * seconds), len(left) // 4)
    if count <= 1:
        return
    start_left = left[:count]
    start_right = right[:count]
    offset = len(left) - count
    for index in range(count):
        amount = index / (count - 1)
        left[offset + index] = left[offset + index] * (1.0 - amount) + \
            start_left[index] * amount
        right[offset + index] = right[offset + index] * (1.0 - amount) + \
            start_right[index] * amount

    # The musical crossfade reconciles the final and opening phrases, while this
    # very short equal-power boundary seal guarantees a zero-crossing join. At
    # 12 ms it is shorter than a perceptible beat or pause but removes the click
    # caused by joining two otherwise valid samples at different amplitudes.
    seal_count = min(int(SAMPLE_RATE * 0.012), count)
    for index in range(seal_count):
        gain = math.sin((index / (seal_count - 1)) * math.pi * 0.5)
        left[index] *= gain
        right[index] *= gain
        left[-1 - index] *= gain
        right[-1 - index] *= gain


def _normalize(left: array.array, right: array.array, target_peak: float = 0.82) -> None:
    peak = max(max(abs(value) for value in left),
               max(abs(value) for value in right), 1e-9)
    gain = min(1.0, target_peak / peak)
    for index in range(len(left)):
        left[index] *= gain
        right[index] *= gain


def compose_score(spec: ScoreSpec, signal_only: bool) -> tuple[array.array, array.array]:
    beat_seconds = 60.0 / spec.bpm
    total_seconds = beat_seconds * 32.0
    sample_count = int(round(total_seconds * SAMPLE_RATE))
    left = array.array("f", [0.0]) * sample_count
    right = array.array("f", [0.0]) * sample_count
    rng = random.Random(29_000 + spec.root_midi + (10_000 if signal_only else 0))
    noise = 0.0

    for index in range(sample_count):
        t = index / SAMPLE_RATE
        beat = t / beat_seconds
        bar = int(beat // 4.0) % len(spec.progression)
        beat_in_bar = beat - math.floor(beat / 4.0) * 4.0
        degree = spec.progression[bar]
        root = spec.root_midi + spec.mode[degree % len(spec.mode)]
        l_value = 0.0
        r_value = 0.0

        if not signal_only:
            # Warm, slowly breathing triad foundation.
            pad_envelope = 0.78 + 0.22 * math.sin(math.tau * beat / 16.0) ** 2
            for voice, interval in enumerate((0, 4, 7)):
                note = root + interval + (12 if voice == 2 else 0)
                phase = periodic_phase(t, midi_frequency(note))
                tone = (math.sin(phase) + 0.16 * math.sin(phase * 2.002))
                pl, pr = pan(tone * 0.075 * pad_envelope,
                             (-0.42, 0.0, 0.42)[voice])
                l_value += pl
                r_value += pr

            # Homemade plucked-object pulse on eighth notes.
            subdivision = math.floor(beat * 2.0)
            local = t - subdivision * beat_seconds * 0.5
            pattern = (0, 2, 4, 2, 5, 4, 2, 1)
            note = spec.root_midi + spec.mode[pattern[int(subdivision) % 8]] + 12
            picked = pluck(local, midi_frequency(note), 4.0 - spec.warmth * 0.7)
            pl, pr = pan(picked * 0.16, -0.32 if int(subdivision) % 2 == 0 else 0.32)
            l_value += pl
            r_value += pr

            # Rounded bass on beats one and three.
            bass_local = (beat_in_bar % 2.0) * beat_seconds
            bass_env = math.exp(-bass_local * 2.4)
            bass = math.sin(periodic_phase(t, midi_frequency(root - 24))) * \
                bass_env * 0.17
            l_value += bass * 0.72
            r_value += bass * 0.72

            # Quiet brushed hardware/noise gives tactile motion without a drum kit.
            noise = noise * 0.91 + (rng.random() * 2.0 - 1.0) * 0.09
            pulse_local = (beat - math.floor(beat)) * beat_seconds
            brush = noise * math.exp(-pulse_local * 18.0) * 0.06
            l_value += brush * (0.6 + 0.4 * spec.warmth)
            r_value += brush * (0.9 - 0.25 * spec.warmth)
        else:
            # Five-note Signal motif. Its orchestration grows across destinations,
            # while the note identity remains unmistakably shared.
            motif_step = int(math.floor(beat * 0.5)) % len(SIGNAL_INTERVALS)
            motif_start = math.floor(beat * 0.5) * beat_seconds * 2.0
            local = t - motif_start
            frequency = midi_frequency(spec.root_midi + 19 +
                                       SIGNAL_INTERVALS[motif_step])
            motif = bell(local, frequency) * 0.22
            shimmer = math.sin(periodic_phase(t, frequency * 2.003)) * \
                (0.018 + spec.signal_level * 0.022)
            pl, pr = pan(motif + shimmer,
                         -0.5 + motif_step * (1.0 / (len(SIGNAL_INTERVALS) - 1)))
            l_value += pl
            r_value += pr
            if spec.signal_level >= 0.5:
                low = math.sin(periodic_phase(t, frequency * 0.25)) * 0.035
                l_value += low
                r_value += low

        left[index] = soft_clip(l_value)
        right[index] = soft_clip(r_value)

    _crossfade_loop(left, right, 0.45)
    _normalize(left, right, 0.80 if signal_only else 0.84)
    return left, right


def compose_sfx(slug: str, duration: float, seed: int) -> tuple[array.array, array.array]:
    sample_count = int(round(duration * SAMPLE_RATE))
    left = array.array("f", [0.0]) * sample_count
    right = array.array("f", [0.0]) * sample_count
    rng = random.Random(seed)
    noise = 0.0
    for index in range(sample_count):
        t = index / SAMPLE_RATE
        x = t / duration
        envelope = math.sin(math.pi * min(1.0, x)) ** 1.5
        white = rng.random() * 2.0 - 1.0
        noise = noise * 0.84 + white * 0.16
        if "footstep" in slug:
            base = 58.0 if "soil" in slug else 106.0
            body = math.sin(periodic_phase(t, base * (1.0 - 0.32 * x))) * \
                math.exp(-t * 17.0)
            grit = noise * math.exp(-t * (13.0 if "soil" in slug else 22.0))
            sample = body * 0.55 + grit * 0.42
        elif "tool" in slug:
            direction = 1.0 if "attach" in slug else -1.0
            frequency = 680.0 + direction * 260.0 * x
            sample = (math.sin(periodic_phase(t, frequency)) * 0.5 +
                      math.sin(periodic_phase(t, frequency * 2.43)) * 0.2 +
                      noise * 0.18) * math.exp(-t * 9.0)
        elif slug == "signal-pulse":
            frequency = 220.0 * (1.0 + 1.7 * x)
            sample = (math.sin(periodic_phase(t, frequency)) * 0.44 +
                      math.sin(periodic_phase(t, frequency * 2.5)) * 0.18) * \
                math.sin(math.pi * x) ** 0.8
        elif slug == "lens-focus":
            frequency = 340.0 + 920.0 * x * x
            sample = (math.sin(periodic_phase(t, frequency)) * 0.32 +
                      noise * 0.08) * envelope
        elif slug == "interaction-release":
            sample = (math.sin(periodic_phase(t, 520.0)) * 0.24 +
                      math.sin(periodic_phase(t, 780.0)) * 0.16) * \
                math.exp(-t * 6.5)
        else:
            sample = (math.sin(periodic_phase(t, 660.0)) * 0.28 +
                      math.sin(periodic_phase(t, 990.0)) * 0.17) * envelope
        sample = soft_clip(sample)
        pl, pr = pan(sample, math.sin(math.pi * 2.0 * x) * 0.12)
        left[index] = pl
        right[index] = pr
    _normalize(left, right, 0.78)
    return left, right


def write_wav(path: Path, left: array.array, right: array.array) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        block = bytearray()
        for l_value, r_value in zip(left, right):
            block += struct.pack(
                "<hh",
                int(max(-1.0, min(1.0, l_value)) * 32767),
                int(max(-1.0, min(1.0, r_value)) * 32767))
            if len(block) >= 262_144:
                output.writeframesraw(block)
                block.clear()
        if block:
            output.writeframesraw(block)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def inspect_wav(path: Path) -> dict[str, object]:
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        rate = source.getframerate()
        frames = source.getnframes()
        width = source.getsampwidth()
        payload = source.readframes(frames)
    if channels != 2 or rate != SAMPLE_RATE or width != 2 or frames <= 0:
        raise RuntimeError(f"Invalid production WAV contract: {path}")
    samples = array.array("h")
    samples.frombytes(payload)
    peak = max(abs(value) for value in samples) / 32767.0
    rms = math.sqrt(sum((value / 32767.0) ** 2 for value in samples) /
                    len(samples))
    if peak <= 0.08 or peak > 0.9 or rms <= 0.008:
        raise RuntimeError(
            f"Silent, clipped or underpowered audio {path}: peak={peak}, rms={rms}")
    seam = max(abs(samples[channel] - samples[-channels + channel]) / 32767.0
               for channel in range(channels))
    return {
        "channels": channels,
        "sampleRate": rate,
        "sampleFrames": frames,
        "durationSeconds": round(frames / rate, 6),
        "peak": round(peak, 6),
        "rms": round(rms, 6),
        "loopBoundaryDelta": round(seam, 6),
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
    }


def generate() -> None:
    AUDIO_ROOT.parent.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix="task29-audio-", dir=AUDIO_ROOT.parent))
    try:
        staged_audio = stage / "Audio"
        files: list[dict[str, object]] = []
        states: list[dict[str, object]] = []
        for spec in SCORES:
            foundation_path = staged_audio / "Music" / f"{spec.slug}.wav"
            signal_path = staged_audio / "Music" / f"{spec.slug}-signal-stem.wav"
            foundation = compose_score(spec, signal_only=False)
            signal = compose_score(spec, signal_only=True)
            if len(foundation[0]) != len(signal[0]):
                raise RuntimeError(f"Stem length mismatch for {spec.slug}")
            write_wav(foundation_path, *foundation)
            write_wav(signal_path, *signal)
            foundation_info = inspect_wav(foundation_path)
            signal_info = inspect_wav(signal_path)
            files.extend((
                {
                    "path": f"Assets/_JustSomeStars/Audio/Music/{spec.slug}.wav",
                    "cueId": spec.cue_id,
                    "bus": "Music",
                    "loop": True,
                    **foundation_info,
                },
                {
                    "path": f"Assets/_JustSomeStars/Audio/Music/"
                            f"{spec.slug}-signal-stem.wav",
                    "cueId": f"stem.signal.{spec.slug}",
                    "bus": "Music",
                    "loop": True,
                    **signal_info,
                },
            ))
            states.append({
                "stateId": spec.state_id,
                "foundationCueId": spec.cue_id,
                "signalStemCueId": f"stem.signal.{spec.slug}",
                "signalLevel": spec.signal_level,
                "crossfadeSeconds": 0.45,
                "bpm": spec.bpm,
            })

        for slug, cue_id, duration, seed in SFX_SPECS:
            path = staged_audio / "SFX" / f"{slug}.wav"
            write_wav(path, *compose_sfx(slug, duration, seed))
            files.append({
                "path": f"Assets/_JustSomeStars/Audio/SFX/{slug}.wav",
                "cueId": cue_id,
                "bus": "Effects",
                "loop": False,
                **inspect_wav(path),
            })

        manifest = {
            "schemaVersion": SCHEMA_VERSION,
            "packageId": "task29-original-audio-v1",
            "sampleRate": SAMPLE_RATE,
            "source": "Original project-authored deterministic synthesis",
            "license": "Copyright ScientificAJ; original project asset",
            "generationTool": "tools/media/generate_task29_audio.py",
            "creativeDirection": (
                "playful homemade instruments, warm orchestral harmony, restrained "
                "electronics, and one five-note Signal motif that resolves at dinner"
            ),
            "files": sorted(files, key=lambda item: str(item["path"])),
            "musicStates": states,
        }
        manifest_path = staged_audio / "task29-audio-manifest.json"
        manifest_path.write_text(
            json.dumps(manifest, indent=2, sort_keys=True) + "\n",
            encoding="utf-8")
        validate_tree(staged_audio)

        # Unity's GUID-bearing sidecars are part of the asset identity. A
        # deterministic media rerun may replace bytes, but it must never issue
        # new GUIDs and silently break AudioClip references already in scenes or
        # ScriptableObjects.
        if AUDIO_ROOT.exists():
            for meta in AUDIO_ROOT.rglob("*.meta"):
                relative = meta.relative_to(AUDIO_ROOT)
                staged_meta = staged_audio / relative
                represented = staged_audio / str(relative)[:-5]
                if represented.exists():
                    staged_meta.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(meta, staged_meta)

        backup = AUDIO_ROOT.with_name("Audio.task29-backup")
        if backup.exists():
            shutil.rmtree(backup)
        if AUDIO_ROOT.exists():
            os.replace(AUDIO_ROOT, backup)
        try:
            os.replace(staged_audio, AUDIO_ROOT)
            validate_tree(AUDIO_ROOT)
        except BaseException:
            if AUDIO_ROOT.exists():
                shutil.rmtree(AUDIO_ROOT)
            if backup.exists():
                os.replace(backup, AUDIO_ROOT)
            raise
        if backup.exists():
            shutil.rmtree(backup)
    finally:
        shutil.rmtree(stage, ignore_errors=True)


def validate_tree(root: Path = AUDIO_ROOT) -> None:
    manifest_path = root / "task29-audio-manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError(f"Missing Task 29 audio manifest: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != SCHEMA_VERSION or \
            manifest.get("packageId") != "task29-original-audio-v1":
        raise RuntimeError("Unexpected Task 29 audio manifest schema/package.")
    if len(manifest.get("files", [])) != len(SCORES) * 2 + len(SFX_SPECS):
        raise RuntimeError("Task 29 audio manifest has incomplete media inventory.")
    for record in manifest["files"]:
        relative = Path(record["path"]).relative_to(
            "Assets/_JustSomeStars/Audio")
        path = root / relative
        actual = inspect_wav(path)
        for key in ("sha256", "bytes", "sampleFrames", "sampleRate", "channels"):
            if actual[key] != record[key]:
                raise RuntimeError(f"Audio manifest mismatch {path}: {key}")
        if record["loop"] and actual["loopBoundaryDelta"] > 0.02:
            raise RuntimeError(
                f"Loop seam exceeds 0.02 full-scale at {path}: "
                f"{actual['loopBoundaryDelta']}")
    states = manifest.get("musicStates", [])
    if [state["stateId"] for state in states] != [spec.state_id for spec in SCORES]:
        raise RuntimeError("Task 29 music-state progression is not canonical.")


def write_rights_ledger() -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    RIGHTS_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = RIGHTS_PATH.with_suffix(".csv.tmp")
    with temporary.open("w", encoding="utf-8", newline="") as output:
        writer = csv.writer(output, lineterminator="\n")
        writer.writerow(("asset_path", "sha256", "source", "license",
                         "generation_tool", "edit_status"))
        for record in manifest["files"]:
            writer.writerow((
                record["path"],
                record["sha256"],
                "Original project-authored deterministic synthesis; no samples",
                "Copyright ScientificAJ; original project asset",
                "tools/media/generate_task29_audio.py",
                "generated-mixed-loop-checked",
            ))
        for display_name in FACE_ATLAS_CHARACTERS:
            character_id = display_name.lower()
            atlas = ROOT / (
                f"Assets/_JustSomeStars/Art/2D/Characters/{display_name}/"
                f"Atlases/neutral/{character_id}-face-speech.png")
            sprite_manifest = atlas.with_suffix("").with_suffix(
                ".sprite-manifest.json")
            if not atlas.is_file() or not sprite_manifest.is_file():
                raise RuntimeError(f"Missing inherited facial atlas: {atlas}")
            declaration = json.loads(sprite_manifest.read_text(encoding="utf-8"))
            declared_hash = declaration.get("atlas", {}).get("sha256")
            actual_hash = sha256(atlas)
            if declared_hash != actual_hash:
                raise RuntimeError(f"Stale inherited facial atlas hash: {atlas}")
            writer.writerow((
                atlas.relative_to(ROOT).as_posix(),
                actual_hash,
                "Approved project-authored expression and speech atlas",
                "Copyright ScientificAJ; original project asset",
                ("tools/media/generate_task29_captain_face.py"
                 if display_name == "Captain"
                 else "tools/sprites/create_crew_package.py"),
                ("generated-from-approved-expression-authority"
                 if display_name == "Captain"
                 else "reused-validated-no-edit"),
            ))
    os.replace(temporary, RIGHTS_PATH)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--rights-only", action="store_true")
    arguments = parser.parse_args()
    if arguments.validate_only and arguments.rights_only:
        parser.error("choose only one validation mode")
    if arguments.rights_only:
        validate_tree()
        write_rights_ledger()
    elif arguments.validate_only:
        validate_tree()
    else:
        generate()
        write_rights_ledger()
        validate_tree()
    print("Task 29 audio package is complete and validated.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Task 29 audio generation failed: {error}", file=sys.stderr)
        raise
