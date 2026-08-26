#!/usr/bin/env python3
"""Render deterministic contact-sheet and animated WebP pipeline evidence."""

from PIL import Image, ImageDraw


def render_evidence(request, clip_rows, staging_root):
    character_id = request["characterId"]
    frame_width = request["frameWidth"]
    frame_height = request["frameHeight"]
    longest = max(len(row["frames"]) for row in clip_rows)
    label_height = 20
    contact = Image.new(
        "RGBA",
        (longest * frame_width, len(clip_rows) * (frame_height + label_height)),
        (18, 24, 40, 255),
    )
    draw = ImageDraw.Draw(contact)
    for row_index, row in enumerate(clip_rows):
        top = row_index * (frame_height + label_height)
        draw.text((5, top + 4), row["clip"]["id"], fill=(175, 220, 255, 255))
        for frame_index, frame in enumerate(row["frames"]):
            checker = Image.new("RGBA", frame.size, (42, 50, 68, 255))
            checker.alpha_composite(frame)
            contact.alpha_composite(
                checker,
                (frame_index * frame_width, top + label_height),
            )
    contact.save(
        staging_root / f"{character_id}-contact-sheet.png",
        format="PNG",
        optimize=False,
        compress_level=9,
    )

    preview_row = next(
        (row for row in clip_rows if ".run." in row["clip"]["id"]),
        clip_rows[0],
    )
    preview_frames = []
    for frame in preview_row["frames"]:
        canvas = Image.new("RGBA", frame.size, (18, 24, 40, 255))
        canvas.alpha_composite(frame)
        preview_frames.append(canvas.convert("RGB"))
    duration = round(1000 / preview_row["clip"]["cadenceFps"])
    preview_frames[0].save(
        staging_root / f"{character_id}-preview.webp",
        format="WEBP",
        save_all=True,
        append_images=preview_frames[1:],
        duration=duration,
        loop=0,
        lossless=True,
        method=6,
    )
