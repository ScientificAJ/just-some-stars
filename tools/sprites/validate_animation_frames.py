#!/usr/bin/env python3
"""Validate and baseline-register extracted animation frames."""

from collections import deque

from PIL import Image


class FrameValidationError(ValueError):
    """Raised when one coherent animation row violates its declared contract."""


def validate_and_register(request, clip, frames):
    frame_count = clip.get("frameCount")
    if len(frames) != frame_count:
        raise FrameValidationError("Extracted frame count does not match the request.")
    contacts = clip.get("contacts")
    events = clip.get("events")
    if not isinstance(contacts, list) or len(contacts) != frame_count:
        raise FrameValidationError("Every frame must declare its contacts.")
    if not isinstance(events, list) or len(events) != frame_count:
        raise FrameValidationError("Every frame must declare its events.")
    _validate_alternating_gait(clip, contacts)

    alpha_threshold = request.get("alphaThreshold")
    if not isinstance(alpha_threshold, int) or not 0 <= alpha_threshold <= 255:
        raise FrameValidationError("alphaThreshold must be an integer from 0 to 255.")
    marker = request.get("facingMarker")
    if not isinstance(marker, dict) or len(marker.get("rgba", [])) != 4:
        raise FrameValidationError("A four-channel facingMarker is required.")
    maximum_hole_pixels = request.get("maximumInteriorAlphaHolePixels", 0)
    if not isinstance(maximum_hole_pixels, int) or maximum_hole_pixels < 0:
        raise FrameValidationError(
            "maximumInteriorAlphaHolePixels must be a non-negative integer."
        )

    analyses = []
    cleaned_frames = []
    for index, frame in enumerate(frames):
        source_analysis = _analyze_frame(frame, alpha_threshold)
        if source_analysis["touchesBorder"]:
            raise FrameValidationError(
                f"{clip.get('id')} frame {index} is clipped by its cell border."
            )
        _validate_facing(
            frame,
            source_analysis,
            marker,
            clip.get("facing"),
            index,
        )
        cleaned = _without_facing_marker(frame, tuple(marker["rgba"]))
        analysis = _analyze_frame(cleaned, alpha_threshold)
        if analysis["interiorHolePixels"] > maximum_hole_pixels:
            raise FrameValidationError(
                f"{clip.get('id')} frame {index} contains an interior alpha hole "
                f"of {analysis['interiorHolePixels']} pixels; the declared maximum "
                f"is {maximum_hole_pixels}."
            )
        analyses.append(analysis)
        cleaned_frames.append(cleaned)

    canonical_baseline = max(item["baselinePixels"] for item in analyses)
    maximum_correction = request.get("maximumBaselineCorrectionPixels")
    if not isinstance(maximum_correction, int) or maximum_correction < 0:
        raise FrameValidationError(
            "maximumBaselineCorrectionPixels must be a non-negative integer."
        )

    registered = []
    diagnostics = []
    for index, (frame, analysis) in enumerate(zip(cleaned_frames, analyses)):
        correction = canonical_baseline - analysis["baselinePixels"]
        if correction > maximum_correction:
            raise FrameValidationError(
                f"{clip.get('id')} frame {index} baseline correction "
                f"{correction}px exceeds {maximum_correction}px."
            )
        translated = Image.new("RGBA", frame.size)
        translated.alpha_composite(frame, (0, correction))
        post = _analyze_frame(translated, alpha_threshold)
        if post["touchesBorder"]:
            raise FrameValidationError(
                f"{clip.get('id')} frame {index} is clipped after baseline registration."
            )
        registered.append(translated)
        diagnostics.append(
            {
                "sourceBaselinePixels": analysis["baselinePixels"],
                "registrationOffsetPixels": correction,
                "registeredBaselinePixels": post["baselinePixels"],
                "alphaBoundsPixels": post["alphaBoundsPixels"],
                "interiorAlphaHolePixels": post["interiorHolePixels"],
            }
        )
    return registered, diagnostics


def _without_facing_marker(frame, marker_rgba):
    cleaned = frame.copy()
    pixels = cleaned.load()
    for y in range(cleaned.height):
        for x in range(cleaned.width):
            if pixels[x, y] == marker_rgba:
                pixels[x, y] = (0, 0, 0, 0)
    return cleaned


def _analyze_frame(frame, alpha_threshold):
    width, height = frame.size
    alpha = frame.getchannel("A")
    pixels = alpha.load()
    opaque = [
        (x, y)
        for y in range(height)
        for x in range(width)
        if pixels[x, y] > alpha_threshold
    ]
    if not opaque:
        raise FrameValidationError("Animation frame has no visible pixels.")
    min_x = min(x for x, _ in opaque)
    max_x = max(x for x, _ in opaque)
    min_y = min(y for _, y in opaque)
    max_y = max(y for _, y in opaque)
    touches = min_x == 0 or min_y == 0 or max_x == width - 1 or max_y == height - 1
    holes = _interior_transparent_pixels(
        pixels,
        alpha_threshold,
        min_x,
        min_y,
        max_x,
        max_y,
    )
    return {
        "alphaBoundsPixels": [min_x, min_y, max_x + 1, max_y + 1],
        "baselinePixels": max_y,
        "touchesBorder": touches,
        "interiorHolePixels": holes,
        "alphaCenterX": sum(x for x, _ in opaque) / len(opaque),
    }


def _interior_transparent_pixels(
    alpha_pixels,
    alpha_threshold,
    min_x,
    min_y,
    max_x,
    max_y,
):
    transparent = {
        (x, y)
        for y in range(min_y, max_y + 1)
        for x in range(min_x, max_x + 1)
        if alpha_pixels[x, y] <= alpha_threshold
    }
    outside = set()
    queue = deque()
    for x in range(min_x, max_x + 1):
        queue.append((x, min_y))
        queue.append((x, max_y))
    for y in range(min_y, max_y + 1):
        queue.append((min_x, y))
        queue.append((max_x, y))
    while queue:
        point = queue.popleft()
        if point not in transparent or point in outside:
            continue
        outside.add(point)
        x, y = point
        for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if min_x <= neighbor[0] <= max_x and min_y <= neighbor[1] <= max_y:
                queue.append(neighbor)
    return len(transparent - outside)


def _validate_facing(frame, analysis, marker, facing, frame_index):
    expected = tuple(marker["rgba"])
    marker_points = []
    for y in range(frame.height):
        for x in range(frame.width):
            if frame.getpixel((x, y)) == expected:
                marker_points.append((x, y))
    minimum = marker.get("minimumPixels")
    if not isinstance(minimum, int) or minimum <= 0 or len(marker_points) < minimum:
        raise FrameValidationError(
            f"Frame {frame_index} does not contain the declared facing marker."
        )
    marker_x = sum(x for x, _ in marker_points) / len(marker_points)
    center_x = analysis["alphaCenterX"]
    if facing == "Right" and marker_x <= center_x + 2:
        raise FrameValidationError(f"Frame {frame_index} has incorrect Right facing.")
    if facing == "Left" and marker_x >= center_x - 2:
        raise FrameValidationError(f"Frame {frame_index} has incorrect Left facing.")
    if facing not in {"Left", "Right", "Neutral"}:
        raise FrameValidationError(f"Unsupported facing: {facing!r}.")


def _validate_alternating_gait(clip, contacts):
    if ".run." not in clip.get("id", ""):
        return
    gait_contacts = [row[0] for row in contacts if len(row) == 1]
    if len(gait_contacts) < 2:
        raise FrameValidationError("Run clip has no alternating foot-contact sequence.")
    for previous, current in zip(gait_contacts, gait_contacts[1:]):
        if previous == current or current not in {"LeftFoot", "RightFoot"}:
            raise FrameValidationError("Run contacts must form an alternating gait.")
