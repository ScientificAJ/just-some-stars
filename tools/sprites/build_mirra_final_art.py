#!/usr/bin/env python3
"""Build the final Mirra semantic sky/far-world plates and clean HUD art."""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


PROJECT_ROOT = Path(__file__).resolve().parents[2]
TARGET = PROJECT_ROOT / "outputs/just-some-stars-2.5d-gameplay-target-v1.png"
TARGET_SHA256 = "72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed"
ART_ROOT = PROJECT_ROOT / "Assets/_JustSomeStars/Art/2D/Environments/Mirra"
LAYER_ROOT = ART_ROOT / "Layers"
HUD_ROOT = ART_ROOT / "Hud"
SKY_SOURCE = LAYER_ROOT / "Source/MirraSkyPlateSource.png"
SKY_SOURCE_SHA256 = "f92036f9870163e303001293ead66772945235c0c1a7cde04f3b72928d03483f"
ENVIRONMENT_SOURCE = LAYER_ROOT / "Source/MirraEnvironmentPlateSource.png"
ENVIRONMENT_SOURCE_SHA256 = "230132aed34111b245aba902103ea558679efcb619a54d1a7bd0e78582146e3e"
AUTHORED_SIZE = (1672, 941)
PADDED_WIDTH = 2508


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _require_authority() -> None:
    if _sha256(TARGET) != TARGET_SHA256:
        raise RuntimeError("The locked Mirra target hash changed.")
    if _sha256(SKY_SOURCE) != SKY_SOURCE_SHA256:
        raise RuntimeError("The approved sky-only source hash changed.")
    if _sha256(ENVIRONMENT_SOURCE) != ENVIRONMENT_SOURCE_SHA256:
        raise RuntimeError("The approved clean environment source hash changed.")


def _center_crop(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    if image.size == AUTHORED_SIZE:
        return image
    if image.height != AUTHORED_SIZE[1] or image.width < AUTHORED_SIZE[0]:
        return image.resize(AUTHORED_SIZE, Image.Resampling.LANCZOS)
    inset = (image.width - AUTHORED_SIZE[0]) // 2
    return image.crop((inset, 0, inset + AUTHORED_SIZE[0], image.height))


def _mirror_pad(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    if image.size != AUTHORED_SIZE:
        image = image.resize(AUTHORED_SIZE, Image.Resampling.LANCZOS)
    pad = (PADDED_WIDTH - AUTHORED_SIZE[0]) // 2
    output = Image.new("RGBA", (PADDED_WIDTH, AUTHORED_SIZE[1]))
    output.paste(image, (pad, 0))
    # Reflect a full travel-width slice so overscan retains natural cloud
    # frequency instead of stretching a narrow strip into visible bands. The
    # one authored sun is placed only after this padding step.
    left = image.crop((0, 0, pad, image.height)).transpose(
        Image.Transpose.FLIP_LEFT_RIGHT)
    right = image.crop((image.width - pad, 0, image.width, image.height)).transpose(
        Image.Transpose.FLIP_LEFT_RIGHT)
    output.paste(left, (0, 0))
    output.paste(right, (pad + image.width, 0))
    return output


def _remove_low_sun(image: Image.Image) -> Image.Image:
    """Remove the generated low sun before constructing travel overscan."""
    source = image.convert("RGB")
    output = source.copy()
    source_pixels = source.load()
    output_pixels = output.load()
    old_x, old_y = 82, 540
    removal_radius = 82
    for y in range(max(0, old_y - removal_radius),
                   min(source.height, old_y + removal_radius + 1)):
        for x in range(max(0, old_x - removal_radius),
                       min(source.width, old_x + removal_radius + 1)):
            distance = ((x - old_x) ** 2 + (y - old_y) ** 2) ** 0.5
            if distance > removal_radius:
                continue
            amount = (1.0 - distance / removal_radius) ** 2 * 0.96
            replacement = source_pixels[min(source.width - 1, x + 118), y]
            original = source_pixels[x, y]
            output_pixels[x, y] = tuple(
                round(original[channel] * (1.0 - amount) +
                      replacement[channel] * amount)
                for channel in range(3))

    return output.convert("RGBA")


def _add_target_sun(image: Image.Image) -> Image.Image:
    """Place one small sun in the authored center after overscan is built."""
    output = image.convert("RGBA")
    glow = Image.new("RGBA", output.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow, "RGBA")
    pad = (PADDED_WIDTH - AUTHORED_SIZE[0]) // 2
    new_x, new_y = pad + 51, 370
    glow_draw.ellipse(
        (new_x - 54, new_y - 54, new_x + 54, new_y + 54),
        fill=(255, 184, 92, 112))
    glow = glow.filter(ImageFilter.GaussianBlur(25.0))
    output = Image.alpha_composite(output, glow)
    sun_draw = ImageDraw.Draw(output, "RGBA")
    sun_draw.ellipse(
        (new_x - 16, new_y - 16, new_x + 16, new_y + 16),
        fill=(255, 250, 224, 255))
    return output


def _build_sky() -> Image.Image:
    sky = Image.open(SKY_SOURCE).convert("RGB")
    sky = sky.resize(AUTHORED_SIZE, Image.Resampling.LANCZOS)
    sky = _remove_low_sun(sky)
    sky = sky.filter(ImageFilter.GaussianBlur(3.5)).convert("RGBA")
    sky.putalpha(Image.new("L", sky.size, 255))
    return _add_target_sun(_mirror_pad(sky))


def _build_atmosphere() -> Image.Image:
    """Paint low-frequency haze without duplicating world landmarks."""
    atmosphere = Image.new(
        "RGBA", (PADDED_WIDTH, AUTHORED_SIZE[1]), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atmosphere, "RGBA")
    pad = (PADDED_WIDTH - AUTHORED_SIZE[0]) // 2
    draw.ellipse(
        (pad - 300, 350, pad + 1050, 890),
        fill=(255, 147, 98, 24))
    draw.ellipse(
        (pad + 720, 300, pad + 1980, 850),
        fill=(111, 129, 235, 20))
    draw.ellipse(
        (pad + 250, 470, pad + 1520, 800),
        fill=(205, 149, 199, 16))
    return atmosphere.filter(ImageFilter.GaussianBlur(82.0))


def _terrain_boundary(x: int) -> float:
    points = (
        (0, 475),
        (220, 470),
        (430, 450),
        (650, 465),
        (850, 455),
        (1030, 430),
        (1200, 405),
        (1400, 380),
        (1671, 355),
    )
    for index in range(1, len(points)):
        left_x, left_y = points[index - 1]
        right_x, right_y = points[index]
        if x <= right_x:
            amount = (x - left_x) / max(1, right_x - left_x)
            return left_y + (right_y - left_y) * amount
    return float(points[-1][1])


def _build_far_world(environment: Image.Image) -> Image.Image:
    environment = environment.convert("RGBA")
    near_masks = []
    for filename in (
        "MirraMidgroundFinal.png",
        "MirraGameplayFinal.png",
        "MirraForegroundFinal.png",
    ):
        near_masks.append(_center_crop(Image.open(LAYER_ROOT / filename)).getchannel("A"))

    near_pixels = [mask.load() for mask in near_masks]
    alpha = Image.new("L", AUTHORED_SIZE, 0)
    alpha_pixels = alpha.load()
    width, height = AUTHORED_SIZE
    edge_fade_width = 256
    for y in range(height):
        for x in range(width):
            boundary = _terrain_boundary(x)
            base = 0.0
            if y >= boundary:
                base = min(1.0, (y - boundary + 20.0) / 55.0)

            edge = min(1.0, x / edge_fade_width, (width - 1 - x) / edge_fade_width)
            edge = edge * edge
            near = max(mask[x, y] for mask in near_pixels) / 255.0
            owned = base * edge * (1.0 - near)
            alpha_pixels[x, y] = max(0, min(255, round(owned * 255.0)))

    alpha = alpha.filter(ImageFilter.GaussianBlur(1.5))

    # The signal tower and its beacon rise above the distant terrain boundary.
    # Give their irregular silhouette explicit FarWorld ownership without a
    # rectangular patch that could reveal a parallax seam.
    signal = Image.new("L", AUTHORED_SIZE, 0)
    signal_draw = ImageDraw.Draw(signal)
    signal_draw.polygon(
        (
            (1205, 475),
            (1235, 445),
            (1265, 424),
            (1286, 330),
            (1302, 306),
            (1308, 243),
            (1338, 235),
            (1344, 305),
            (1371, 350),
            (1392, 426),
            (1426, 470),
        ),
        fill=255)
    signal_draw.line((1324, 0, 1324, 258), fill=255, width=13)
    signal_draw.ellipse((1294, 214, 1355, 279), fill=255)
    signal = signal.filter(ImageFilter.GaussianBlur(5.0))
    alpha = ImageChops.lighter(alpha, signal)
    far_world = environment.copy()
    far_world.putalpha(alpha)
    output = Image.new("RGBA", (PADDED_WIDTH, height), (0, 0, 0, 0))
    output.paste(far_world, ((PADDED_WIDTH - width) // 2, 0), far_world)
    return output


def _canvas(size: tuple[int, int], scale: int = 4):
    image = Image.new("RGBA", (size[0] * scale, size[1] * scale), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image), scale


def _finish_icon(image: Image.Image, size: tuple[int, int], scale: int) -> Image.Image:
    return image.resize(size, Image.Resampling.LANCZOS)


def _line(draw: ImageDraw.ImageDraw, points, fill, width, scale):
    draw.line([(round(x * scale), round(y * scale)) for x, y in points],
              fill=fill, width=round(width * scale), joint="curve")


def _ellipse(draw: ImageDraw.ImageDraw, box, outline, width, scale, fill=None):
    draw.ellipse(tuple(round(value * scale) for value in box), fill=fill,
                 outline=outline, width=round(width * scale))


def _build_joystick() -> Image.Image:
    size = (245, 245)
    image, draw, scale = _canvas(size)
    cyan = (154, 213, 255, 178)
    dim = (96, 151, 205, 72)
    _ellipse(draw, (12, 12, 233, 233), cyan, 2, scale, fill=(17, 39, 72, 34))
    _ellipse(draw, (57, 57, 188, 188), dim, 2, scale, fill=(8, 20, 48, 24))
    _ellipse(draw, (88, 88, 157, 157), (110, 175, 230, 85), 2, scale)
    arrows = {
        "up": [(122, 30), (106, 48), (115, 48), (115, 62), (129, 62), (129, 48), (138, 48)],
        "down": [(122, 215), (106, 197), (115, 197), (115, 183), (129, 183), (129, 197), (138, 197)],
        "left": [(30, 122), (48, 106), (48, 115), (62, 115), (62, 129), (48, 129), (48, 138)],
        "right": [(215, 122), (197, 106), (197, 115), (183, 115), (183, 129), (197, 129), (197, 138)],
    }
    for points in arrows.values():
        draw.polygon([(x * scale, y * scale) for x, y in points], fill=(163, 220, 255, 154))
    return _finish_icon(image, size, scale)


def _build_lens() -> Image.Image:
    size = (175, 175)
    image, draw, scale = _canvas(size)
    cyan = (120, 219, 255, 230)
    _ellipse(draw, (8, 8, 167, 167), (140, 214, 255, 150), 2, scale,
             fill=(9, 27, 63, 42))
    _ellipse(draw, (26, 26, 149, 149), (80, 175, 242, 78), 2, scale)
    _line(draw, [(52, 54), (52, 121), (123, 121)], cyan, 7, scale)
    _line(draw, [(52, 54), (100, 54)], cyan, 7, scale)
    _ellipse(draw, (73, 69, 113, 109), cyan, 6, scale)
    _line(draw, [(101, 101), (124, 124)], cyan, 7, scale)
    _ellipse(draw, (108, 39, 139, 70), (178, 238, 255, 235), 4, scale,
             fill=(35, 120, 215, 126))
    _line(draw, [(123, 45), (123, 63)], (224, 249, 255, 240), 3, scale)
    _line(draw, [(114, 54), (132, 54)], (224, 249, 255, 240), 3, scale)
    return _finish_icon(image, size, scale)


def _build_interact() -> Image.Image:
    size = (168, 168)
    image, draw, scale = _canvas(size)
    cyan = (139, 224, 255, 238)
    _ellipse(draw, (6, 6, 162, 162), (128, 207, 255, 155), 2, scale,
             fill=(8, 24, 58, 40))
    _ellipse(draw, (23, 23, 145, 145), (70, 169, 230, 72), 2, scale)
    # A clean readable pointing-hand silhouette.
    palm = [(71, 119), (65, 91), (67, 72), (75, 70), (79, 86),
            (79, 43), (87, 39), (93, 44), (93, 77), (97, 61),
            (105, 60), (109, 67), (109, 80), (114, 69), (122, 70),
            (125, 79), (124, 103), (114, 121), (99, 132), (82, 130)]
    draw.polygon([(x * scale, y * scale) for x, y in palm], fill=cyan)
    _line(draw, [(80, 89), (95, 93), (108, 101)], (22, 93, 164, 210), 3, scale)
    return _finish_icon(image, size, scale)


def _build_jump() -> Image.Image:
    size = (160, 160)
    image, draw, scale = _canvas(size)
    violet = (202, 126, 255, 242)
    _ellipse(draw, (7, 7, 153, 153), (176, 109, 244, 158), 2, scale,
             fill=(36, 12, 70, 42))
    _ellipse(draw, (24, 24, 136, 136), (140, 76, 224, 76), 2, scale)
    _ellipse(draw, (69, 42, 88, 61), None, 0, scale, fill=violet)
    _line(draw, [(78, 62), (69, 91), (91, 99)], violet, 10, scale)
    _line(draw, [(73, 71), (52, 83), (39, 76)], violet, 8, scale)
    _line(draw, [(72, 89), (52, 111), (39, 108)], violet, 9, scale)
    _line(draw, [(87, 96), (111, 110), (126, 101)], violet, 9, scale)
    return _finish_icon(image, size, scale)


def _build_crew_route(target: Image.Image) -> Image.Image:
    size = (520, 72)
    image, draw, scale = _canvas(size)
    cyan = (115, 219, 255, 218)
    violet = (186, 101, 255, 218)
    _line(draw, [(16, 36), (492, 36)], (100, 191, 234, 178), 2, scale)
    _ellipse(draw, (9, 29, 23, 43), cyan, 2, scale, fill=(22, 48, 78, 215))
    portrait_centers = (88, 164, 240, 316)
    source_centers = ((686, 51), (740, 51), (794, 51), (848, 51))
    for center_x, (source_x, source_y) in zip(portrait_centers, source_centers):
        radius = 21
        crop = target.crop((source_x - 18, source_y - 18,
                            source_x + 18, source_y + 18)).resize(
                                (radius * 2 * scale, radius * 2 * scale),
                                Image.Resampling.LANCZOS)
        mask = Image.new("L", crop.size, 0)
        ImageDraw.Draw(mask).ellipse((1, 1, crop.width - 2, crop.height - 2), fill=255)
        crop.putalpha(mask)
        image.alpha_composite(
            crop.convert("RGBA"),
            (center_x * scale - radius * scale,
             36 * scale - radius * scale))
        # The source crop was composited at high resolution; redraw a clean ring.
        _ellipse(draw, (center_x - 22, 14, center_x + 22, 58), cyan, 3, scale)
    _ellipse(draw, (362, 28, 378, 44), cyan, 3, scale, fill=(15, 42, 80, 230))
    _line(draw, [(370, 19), (370, 53)], cyan, 2, scale)
    _line(draw, [(353, 36), (387, 36)], cyan, 2, scale)
    _ellipse(draw, (478, 16, 512, 50), violet, 3, scale,
             fill=(47, 18, 94, 190))
    _line(draw, [(495, 50), (495, 66)], violet, 3, scale)
    return _finish_icon(image, size, scale)


def _build_actor_shadow() -> Image.Image:
    width, height = 256, 64
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            nx = (x - width * 0.5) / (width * 0.5)
            ny = (y - height * 0.5) / (height * 0.5)
            distance = nx * nx + ny * ny
            alpha = max(0.0, 1.0 - distance)
            pixels[x, y] = (5, 7, 18, round(112 * alpha * alpha))
    return image.filter(ImageFilter.GaussianBlur(1.2))


def build() -> None:
    _require_authority()
    target = Image.open(TARGET).convert("RGBA")
    if target.size != AUTHORED_SIZE:
        raise RuntimeError(f"Unexpected target size: {target.size}")
    environment = Image.open(ENVIRONMENT_SOURCE).convert("RGBA")
    if environment.size != AUTHORED_SIZE:
        raise RuntimeError(f"Unexpected environment size: {environment.size}")
    outputs = {
        LAYER_ROOT / "MirraSkyFinal.png": _build_sky(),
        LAYER_ROOT / "MirraFarWorldFinal.png": _build_far_world(environment),
        LAYER_ROOT / "MirraAtmosphereFinal.png": _build_atmosphere(),
        HUD_ROOT / "MirraJoystick.png": _build_joystick(),
        HUD_ROOT / "MirraLensButton.png": _build_lens(),
        HUD_ROOT / "MirraInteractButton.png": _build_interact(),
        HUD_ROOT / "MirraJumpButton.png": _build_jump(),
        HUD_ROOT / "MirraCrewRoute.png": _build_crew_route(target),
        ART_ROOT / "VFX/MirraActorShadow.png": _build_actor_shadow(),
    }
    for path, image in outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path, format="PNG", optimize=True)
        print(f"{path.relative_to(PROJECT_ROOT)}  {image.size}  {_sha256(path)}")


if __name__ == "__main__":
    build()
