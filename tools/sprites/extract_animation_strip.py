#!/usr/bin/env python3
"""Extract fixed-size frames from one coherent horizontal animation strip."""

from pathlib import Path

from PIL import Image


class StripExtractionError(ValueError):
    """Raised when a coherent source strip cannot be extracted safely."""


def resolve_source(request_path, relative_source):
    request_root = request_path.resolve().parent
    source_path = (request_root / relative_source).resolve()
    if request_root != source_path and request_root not in source_path.parents:
        raise StripExtractionError("Source strip escaped the request directory.")
    if not source_path.is_file():
        raise StripExtractionError(f"Source strip is missing: {relative_source}")
    return source_path


def extract_clip_frames(request_path, request, clip):
    frame_width = positive_integer(request, "frameWidth")
    frame_height = positive_integer(request, "frameHeight")
    frame_count = positive_integer(clip, "frameCount")
    source_path = resolve_source(request_path, clip.get("sourceStrip", ""))
    with Image.open(source_path) as opened:
        strip = opened.convert("RGBA")
    expected_size = (frame_width * frame_count, frame_height)
    if strip.size != expected_size:
        raise StripExtractionError(
            f"{clip.get('id', '<unknown>')} frame count/size mismatch: "
            f"expected strip {expected_size}, got {strip.size}."
        )
    frames = []
    for index in range(frame_count):
        left = index * frame_width
        frames.append(strip.crop((left, 0, left + frame_width, frame_height)))
    return source_path, frames


def positive_integer(mapping, key):
    value = mapping.get(key)
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise StripExtractionError(f"{key} must be a positive integer.")
    return value
