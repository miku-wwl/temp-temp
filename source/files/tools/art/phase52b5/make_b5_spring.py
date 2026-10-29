"""Phase 5.2-B5 spring: four registered stage paintings from the owner's spring_S0_dormant and spring_S3_restored (no new drawing:
alignment, masks, recolouring, clone fill and blending of the two supplied paintings).

python -I tools/art/phase52b5/make_b5_spring.py [--src folder] [--out folder] [--audit folder]

The two paintings share their design but are separate images. SIFT matches (RANSAC similarity) put S0 onto S3's pixel grid; after
that the stone rim, pillars and arch agree to about a pixel, so one card, one footprint and one interaction area serve every stage.
The game's own stages decide which painting shows (ResonanceView, see StagePainting25D):

  stage 0  Silence    the supplied S0 is "dimmed", not dry: it still has two waterfalls and a full pool. The game's silent spring is a
                      dry basin with a murky trickle, so the pool and the channel are drained here: the pool floor keeps S0's own painted
                      stones, re-toned from wet teal to dry stone with a damp middle and one small murky puddle; the floating leaves turn
                      brown; the two waterfalls are filled with the rock face beside them (clone fill) and a thin dark trickle is drawn
                      along the upper one. Plants are slightly wilted. Lanterns unlit (as S0).
  stage 1  Awakening  the stream is back: S3's clear water, waterfalls and crystals in the pool; elsewhere half way from S0 to S3; lanterns
                      still unlit (S0's lanterns).
  stage 2  Garden     85 % of the way to S3 outside the lanterns; lanterns still unlit.
  stage 3  Restored   the supplied S3 (lanterns lit), saturation 0.94 so the painting stays under the golden-hour scene light.

Outputs (default game/Assets/Echoes/Art/Phase52B5/Props/):
  SpringStages.png    the four stages stacked top to bottom (stage 0 first), imported as a 4-slice Texture2DArray
  SpringStages.json   crop, slice size, ground line (bottom profile), registration and per-stage notes
Audit (default artifacts/visual-qa/phase-5.2/b5/asset-audit/spring/): every stage on grass and black, the masks, a stage strip.
"""
import argparse
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / 'tools/art/phase52b4'))
import charlib as C  # noqa: E402  (alpha cleanup, premultiplied resampling, bleed)

# hand-picked regions of the 1456 x 1080 paintings (checked on gridded crops; x0, y0, x1, y1)
WATERFALLS = [(712, 468, 776, 626), (680, 628, 732, 752)]          # upper and lower fall, with their splash rings
CLONE_SHIFT = [(-56, 0), (-50, -6)]                                 # the rock face each fall is filled from
LANTERNS = [(222, 548, 318, 690), (1112, 480, 1214, 616), (634, 196, 724, 320)]   # left, right, hanging (with their glow)
PUDDLE = (1010, 790, 46, 18)                                        # centre x, centre y, half axes: the last murky water


def load(src, name):
    return np.asarray(Image.open(src / (name + '.png')).convert('RGBA'))


def register(s0, s3):
    """Similarity transform taking S0 onto S3 (SIFT on the opaque drawing, Lowe ratio 0.72, RANSAC 2.5 px)."""
    a0, a3 = s0[..., 3] > 128, s3[..., 3] > 128
    g0 = cv2.cvtColor(s0[..., :3], cv2.COLOR_RGB2GRAY)
    g3 = cv2.cvtColor(s3[..., :3], cv2.COLOR_RGB2GRAY)
    sift = cv2.SIFT_create(6000)
    k0, d0 = sift.detectAndCompute(g0, a0.astype(np.uint8) * 255)
    k3, d3 = sift.detectAndCompute(g3, a3.astype(np.uint8) * 255)
    good = [m for m, n in cv2.BFMatcher().knnMatch(d0, d3, k=2) if m.distance < 0.72 * n.distance]
    p0 = np.float32([k0[m.queryIdx].pt for m in good])
    p3 = np.float32([k3[m.trainIdx].pt for m in good])
    M, inl = cv2.estimateAffinePartial2D(p0, p3, method=cv2.RANSAC, ransacReprojThreshold=2.5, maxIters=20000, confidence=0.999, refineIters=20)
    inl = inl.ravel().astype(bool)
    res = np.linalg.norm((p0[inl] @ M[:, :2].T + M[:, 2]) - p3[inl], axis=1)
    info = dict(matches=len(good), inliers=int(inl.sum()), scale=float(np.hypot(M[0, 0], M[1, 0])), rotationDeg=float(np.degrees(np.arctan2(M[1, 0], M[0, 0]))),
                translationPx=[float(M[0, 2]), float(M[1, 2])], residualMedianPx=float(np.median(res)), residualP95Px=float(np.percentile(res, 95)))
    return M, info


def warp_rgba(rgba, M, size):
    """Premultiplied warp (no dark fringe where the alpha is resampled)."""
    f = C.premul(rgba)
    w = cv2.warpAffine(f, M, size, flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
    return C.unpremul(np.clip(w, 0, 1))


def hsv(rgb_u8):
    return cv2.cvtColor(rgb_u8.astype(np.float32) / 255, cv2.COLOR_RGB2HSV)


def box_mask(shape, boxes, feather):
    m = np.zeros(shape, np.float32)
    for x0, y0, x1, y1 in boxes:
        m[y0:y1, x0:x1] = 1
    return np.clip(cv2.GaussianBlur(m, (0, 0), feather), 0, 1) if feather else m


def water_mask(s3):
    """The pool, the channel and the falls in S3 (clear turquoise water, its white falls and foam), as one soft mask."""
    h = hsv(s3[..., :3])
    cyan = (h[..., 0] > 160) & (h[..., 0] < 215) & (h[..., 1] > 0.16) & (h[..., 2] > 0.18) & (s3[..., 3] > 200)
    cyan = ndimage.binary_opening(cyan, iterations=1)
    cyan = ndimage.binary_closing(cyan, iterations=6)
    lab, n = ndimage.label(cyan)
    sizes = ndimage.sum(cyan, lab, range(1, n + 1))
    big = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 2500])
    # holes inside the water: floating leaves and foam (small) are water; the stones standing in the pool (large) are not
    holes, nh = ndimage.label(ndimage.binary_fill_holes(big) & ~big)
    hole_sizes = ndimage.sum(np.ones_like(big), holes, range(1, nh + 1))
    big |= np.isin(holes, [i + 1 for i, s in enumerate(hole_sizes) if s < 1200])
    falls = box_mask(big.shape, WATERFALLS, 0) > 0.5
    white = (h[..., 1] < 0.32) & (h[..., 2] > 0.62)
    big |= falls & (white | ndimage.binary_dilation(big, iterations=3))
    return big


def drain(s0w, water, rng):
    """The silent spring's dry basin from S0's own pool: the painted floor stones keep their light and dark, re-toned from wet teal to
    dry stone and earth; a damp, darker middle; one small murky puddle; the floating leaves turn brown."""
    rgb = s0w[..., :3].astype(np.float32) / 255
    h = hsv(s0w[..., :3])
    lum = rgb @ np.float32([0.299, 0.587, 0.114])
    # S0's water is dark: stretch its luminance to a dry floor's range, after taking out the small painted ripples
    lw = lum[water]
    lo, hi = np.percentile(lw, 3), np.percentile(lw, 97)
    floor = np.clip((lum - lo) / max(1e-4, hi - lo), 0, 1)
    floor = cv2.bilateralFilter(floor.astype(np.float32), 5, 0.07, 2)
    dry_dark = np.float32([0.30, 0.27, 0.22])
    dry_light = np.float32([0.66, 0.61, 0.52])
    dry = dry_dark + (dry_light - dry_dark) * floor[..., None] ** 0.9
    # damp middle: darker and a little greener towards the pool's centre of mass
    ys, xs = np.where(water)
    cy, cx = ys.mean(), xs.mean()
    yy, xx = np.mgrid[0:water.shape[0], 0:water.shape[1]].astype(np.float32)
    dist_edge = ndimage.distance_transform_edt(water)
    damp = np.clip(dist_edge / 38.0, 0, 1) * 0.35
    dry = dry * (1 - damp[..., None]) + dry * np.float32([0.62, 0.66, 0.60]) * damp[..., None]
    # leaves floating in S0 (green) lie dead on the floor: brown
    leaf = water & (h[..., 0] > 55) & (h[..., 0] < 150) & (h[..., 1] > 0.35) & (h[..., 2] > 0.25)
    leaf = ndimage.binary_opening(leaf, iterations=1)
    leaf_col = np.stack([0.42 + 0.30 * lum, 0.30 + 0.22 * lum, 0.16 + 0.10 * lum], -1)
    # the last murky water at the deepest point
    px, py, ax, ay = PUDDLE
    pud = (((xx - px) / ax) ** 2 + ((yy - py) / ay) ** 2)
    pud = np.clip(1.15 - pud, 0, 1) ** 0.6 * water
    murky = np.stack([0.20 + 0.25 * lum, 0.23 + 0.25 * lum, 0.19 + 0.18 * lum], -1)
    out = rgb.copy()
    wm = np.clip(cv2.GaussianBlur(water.astype(np.float32), (0, 0), 1.2), 0, 1)
    out = out * (1 - wm[..., None]) + dry * wm[..., None]
    lm = np.clip(cv2.GaussianBlur((leaf).astype(np.float32), (0, 0), 0.8), 0, 1) * wm
    out = out * (1 - lm[..., None]) + leaf_col * lm[..., None]
    out = out * (1 - pud[..., None]) + murky * pud[..., None]
    res = s0w.copy()
    res[..., :3] = np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)
    return res, dict(floorPercentiles=[float(lo), float(hi)], leafPx=int(leaf.sum()), puddlePx=int((pud > 0.5).sum()))


def remove_falls(img, rng):
    """Clone fill: each fall's box takes the rock face beside it (shifted copy, feathered), then a thin dark trickle runs down the
    upper fall's line and a darker wet stain around it."""
    out = img.astype(np.float32)
    h, w = img.shape[:2]
    for (x0, y0, x1, y1), (dx, dy) in zip(WATERFALLS, CLONE_SHIFT):
        m = np.zeros((h, w), np.float32)
        m[y0 + 4:y1 - 4, x0 + 4:x1 - 4] = 1
        m = np.clip(cv2.GaussianBlur(m, (0, 0), 3.5), 0, 1)
        shifted = np.roll(np.roll(out, -dy, axis=0), -dx, axis=1)   # shifted[y, x] = out[y + dy, x + dx]
        out[..., :3] = out[..., :3] * (1 - m[..., None]) + shifted[..., :3] * m[..., None]
    # the murky trickle: a narrow dark line down the upper fall's centre, wobbling slightly, with a wet stain
    x0, y0, x1, y1 = WATERFALLS[0]
    xs = (x0 + x1) / 2 + 2.0 * np.sin(np.linspace(0, 5.0, y1 - y0))
    stain = np.zeros((h, w), np.float32)
    line = np.zeros((h, w), np.float32)
    for i, y in enumerate(range(y0 + 6, y1 - 10)):
        cx = int(round(xs[i]))
        stain[y, cx - 7:cx + 8] = 1
        line[y, cx - 1:cx + 2] = 1
    stain = np.clip(cv2.GaussianBlur(stain, (0, 0), 3.0), 0, 1) * 0.45
    line = np.clip(cv2.GaussianBlur(line, (0, 0), 0.9), 0, 1) * 0.85
    out[..., :3] *= (1 - stain[..., None] * 0.55)
    trickle = np.float32([58, 66, 54])
    glint = np.float32([150, 160, 140])
    out[..., :3] = out[..., :3] * (1 - line[..., None]) + trickle * line[..., None]
    # a few faint highlights on the trickle so it reads as water, not a crack
    for y in range(y0 + 12, y1 - 14, 9):
        cx = int(round(xs[y - y0 - 6]))
        out[y:y + 2, cx:cx + 1, :3] = out[y:y + 2, cx:cx + 1, :3] * 0.4 + glint * 0.6
    return np.clip(out + 0.5, 0, 255).astype(np.uint8)


def wilt(img, water):
    """A little less life for the silent valley: foliage less saturated and slightly darker (stone and the dry pool untouched)."""
    h = hsv(img[..., :3])
    plant = (h[..., 0] > 50) & (h[..., 0] < 160) & (h[..., 1] > 0.25) & ~water
    flower = (h[..., 1] < 0.2) & (h[..., 2] > 0.8) & ~water
    k = np.clip(cv2.GaussianBlur(plant.astype(np.float32), (0, 0), 1.0), 0, 1)
    h2 = h.copy()
    h2[..., 1] *= 1 - 0.18 * k
    h2[..., 0] = h[..., 0] - 6 * k                 # a touch towards olive
    h2[..., 2] *= 1 - 0.06 * k
    h2[..., 2] *= 1 - 0.12 * np.clip(cv2.GaussianBlur(flower.astype(np.float32), (0, 0), 1.0), 0, 1)
    h2[..., 0] %= 360
    out = img.copy()
    out[..., :3] = np.clip(cv2.cvtColor(h2, cv2.COLOR_HSV2RGB) * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


def mix(a, b, t, mask_b=None, mask_a=None):
    """Straight blend of two registered paintings (premultiplied), with regions forced to one of them."""
    fa, fb = C.premul(a), C.premul(b)
    k = np.full(a.shape[:2], t, np.float32)
    if mask_b is not None:
        k = np.maximum(k, mask_b)
    if mask_a is not None:
        k = k * (1 - mask_a)
    return C.unpremul(fa * (1 - k[..., None]) + fb * k[..., None])


def saturate(img, factor):
    h = hsv(img[..., :3])
    h[..., 1] *= factor
    out = img.copy()
    out[..., :3] = np.clip(cv2.cvtColor(h, cv2.COLOR_HSV2RGB) * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


def bottom_profile(rgba, step=8):
    a = rgba[..., 3] > 128
    hgt = a.shape[0]
    rows = np.where(a.any(0), hgt - 1 - np.argmax(a[::-1], 0), -1)
    return [int(v) for v in rows[::step]]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Source'))
    ap.add_argument('--out', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Props'))
    ap.add_argument('--audit', default=str(ROOT / 'artifacts/visual-qa/phase-5.2/b5/asset-audit/spring'))
    args = ap.parse_args()
    src, out, audit = Path(args.src), Path(args.out), Path(args.audit)
    out.mkdir(parents=True, exist_ok=True)
    audit.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(5205)

    s0 = load(src, 'spring_S0_dormant')
    s3 = load(src, 'spring_S3_restored')
    M, reg = register(s0, s3)
    h, w = s3.shape[:2]
    s0w = warp_rgba(s0, M, (w, h))
    # background glow and red / yellow fringes go before anything is mixed (both paintings, same cleanup)
    s0w = C.clean_alpha(s0w, haze_alpha=0.35, haze_distance=2.0, min_island=150)
    s3c = C.clean_alpha(s3, haze_alpha=0.35, haze_distance=2.0, min_island=150)

    water = water_mask(s3c)
    water_soft = np.clip(cv2.GaussianBlur(water.astype(np.float32), (0, 0), 1.5), 0, 1)
    lanterns = box_mask(water.shape, LANTERNS, 9)

    st0, drain_info = drain(s0w, water, rng)
    st0 = remove_falls(st0, rng)
    st0 = wilt(st0, water)
    st1 = mix(s0w, s3c, 0.5, mask_b=water_soft, mask_a=lanterns)
    st2 = mix(s0w, s3c, 0.85, mask_b=water_soft, mask_a=lanterns)
    st3 = saturate(s3c, 0.94)
    stages = [st0, st1, st2, st3]

    # one crop for all stages (multiples of 4 for block compression), 6 px margin
    alpha = np.max(np.stack([s[..., 3] for s in stages]), 0)
    ys, xs = np.where(alpha > 8)
    x0, y0 = max(0, xs.min() - 6), max(0, ys.min() - 6)
    x1, y1 = min(w, xs.max() + 7), min(h, ys.max() + 7)
    cw, ch = (x1 - x0 + 3) // 4 * 4, (y1 - y0 + 3) // 4 * 4
    x1, y1 = x0 + cw, y0 + ch
    pad = lambda img: np.pad(img, ((0, max(0, y1 - h)), (0, max(0, x1 - w)), (0, 0)))
    crops = [pad(s)[y0:y1, x0:x1] for s in stages]
    crops = [C.bleed(C.decontaminate(c)) for c in crops]
    strip = np.concatenate(crops, 0)
    Image.fromarray(strip).save(out / 'SpringStages.png', optimize=True)
    meta = dict(source=['spring_S0_dormant.png', 'spring_S3_restored.png'], registration=reg, crop=[int(x0), int(y0), int(cw), int(ch)], sliceSize=[int(cw), int(ch)], slices=4,
                bottomProfile=bottom_profile(crops[3]), bottomStep=8, waterMaskPx=int(water.sum()), drain=drain_info,
                stages=['0 Silence: dry basin, murky trickle, lanterns unlit (derived from S0)', '1 Awakening: S3 water, half way S0->S3, lanterns unlit',
                        '2 Garden: S3 water, 85 % S3, lanterns unlit', '3 Restored: S3, saturation 0.94'])
    (out / 'SpringStages.json').write_text(json.dumps(meta, indent=1), encoding='utf-8')
    for i, c in enumerate(crops):
        C.on(c, (96, 128, 72)).save(audit / ('stage%d-on-grass.png' % i))
    C.on(crops[0], (0, 0, 0)).save(audit / 'stage0-on-black.png')
    vis = np.zeros((h, w, 3), np.uint8)
    vis[..., 1] = (water * 200).astype(np.uint8)
    vis[..., 0] = (lanterns * 255).astype(np.uint8)
    vis[..., 2] = (box_mask(water.shape, WATERFALLS, 0) * 255).astype(np.uint8)
    Image.fromarray(vis).save(audit / 'masks-water-green-lanterns-red-falls-blue.png')
    small = [Image.fromarray(np.asarray(C.on(c, (96, 128, 72)))).resize((c.shape[1] // 2, c.shape[0] // 2), Image.LANCZOS) for c in crops]
    sheet = Image.new('RGB', (sum(s.width for s in small) + 30, small[0].height), (30, 30, 30))
    x = 0
    for s in small:
        sheet.paste(s, (x, 0))
        x += s.width + 10
    sheet.save(audit / 'stages-0-1-2-3.png')
    print('registration', json.dumps(reg))
    print('crop', meta['crop'], 'water px', meta['waterMaskPx'], 'drain', json.dumps(drain_info))


if __name__ == '__main__':
    main()
