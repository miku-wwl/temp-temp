"""Phase 5.2-B4 static environment art from the owner-supplied B4 pack (no drawing; cleanup, crops, recolour, packing only).

python tools/art/phase52b4/make_b4_props.py
Outputs (game/Assets/Echoes/Art/Phase52B4/):
  Props/Cottage.png, Props/Willow.png, Props/Spring.png, Props/SpringDormant.png   cleaned static 2.5D props (+ .json with the base points)
  Props/Foliage.png + .json                                                       ground foliage atlas (grass clump, wildflowers, fern)
  UI/MinimapFrame.png                                                              cleaned minimap frame
  Terrain/PlazaCobble.png                                                          mossy cobble ground texture (seam-blended)
SpringDormant is Spring with the water recoloured to a still, murky grey-green and the waterfall and crystals dimmed: the silent valley's
spring before the tonic. It is a script recolour of the same painting, not new art.
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import charlib as C  # noqa: E402

ROOT = HERE.parents[2]
OUT = ROOT / 'game/Assets/Echoes/Art/Phase52B4'
WORK = ROOT / 'artifacts/art-work/phase52b4/props'


def crop_alpha(rgba, pad=6):
    x0, x1, y0, y1 = C.bbox(rgba, 8)
    return rgba[max(0, y0 - pad):y1 + pad, max(0, x0 - pad):x1 + pad], (max(0, x0 - pad), max(0, y0 - pad))


def bottom_profile(rgba):
    """Lowest opaque row of every column (the painting's own ground contact), -1 where the column is empty."""
    a = rgba[..., 3] > 128
    h = a.shape[0]
    rows = np.where(a.any(0), h - 1 - np.argmax(a[::-1], 0), -1)
    return rows


def dormant(rgba):
    """Still, murky water: turquoise/cyan pixels lose saturation and brightness; bright waterfall foam greys out."""
    rgb = rgba[..., :3].astype(np.float32) / 255
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)       # h in degrees
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    water = (h > 160) & (h < 215) & (s > 0.22) & (v > 0.25)
    foam = (s < 0.18) & (v > 0.86)
    water = ndimage.binary_closing(water, iterations=2)
    wsoft = np.clip(ndimage.gaussian_filter(water.astype(np.float32), 1.2), 0, 1)
    fsoft = np.clip(ndimage.gaussian_filter((foam & ndimage.binary_dilation(water, iterations=6)).astype(np.float32), 1.0), 0, 1)
    murky = np.stack([0.30 + 0.25 * v, 0.33 + 0.25 * v, 0.27 + 0.18 * v], -1)   # dull grey-green, keeps the painted ripples
    stone = np.stack([0.50 + 0.12 * v, 0.50 + 0.12 * v, 0.46 + 0.10 * v], -1)
    out = rgb * (1 - wsoft[..., None]) + murky * wsoft[..., None]
    out = out * (1 - fsoft[..., None]) + stone * fsoft[..., None]
    res = rgba.copy()
    res[..., :3] = np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)
    return res, float(water.mean())


def wrap_blend(rgb, band=64):
    """Seam fix by cross-fading each border band with the matching band from the opposite side (so the wrap is continuous)."""
    f = rgb.astype(np.float32)
    h, w = f.shape[:2]
    out = f.copy()
    t = (np.linspace(0, 1, band) ** 1.0)[None, :, None]
    left, right = out[:, :band].copy(), out[:, -band:].copy()
    # at the very edge both sides take the average, fading back to their own pixels over the band
    avg = 0.5 * (left[:, :1] + right[:, -1:])
    out[:, :band] = left * t + (0.5 * left + 0.5 * np.flip(right, 1)) * (1 - t)
    out[:, -band:] = right * np.flip(t, 1) + (0.5 * right + 0.5 * np.flip(left, 1)) * (1 - np.flip(t, 1))
    t = t.transpose(1, 0, 2)
    top, bottom = out[:band].copy(), out[-band:].copy()
    out[:band] = top * t + (0.5 * top + 0.5 * np.flip(bottom, 0)) * (1 - t)
    out[-band:] = bottom * np.flip(t, 0) + (0.5 * bottom + 0.5 * np.flip(top, 0)) * (1 - np.flip(t, 0))
    return np.clip(out, 0, 255).astype(np.uint8)


def _in_triangle(x, y, a, b, c):
    def side(p, q):
        return (x - q[0]) * (p[1] - q[1]) - (p[0] - q[0]) * (y - q[1])
    d1, d2, d3 = side(a, b), side(b, c), side(c, a)
    neg = (d1 < 0) | (d2 < 0) | (d3 < 0)
    pos = (d1 > 0) | (d2 > 0) | (d3 > 0)
    return ~(neg & pos)


def shape_icon(kind, size=96, ss=4):
    """Simple anti-aliased UI glyphs for the minimap (geometric shapes, supersampled). Image rows run downwards: 'up' is a negative y."""
    n = size * ss
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / ss - size / 2 + 0.5
    r = np.hypot(x, y)
    k = size
    if kind == 'arrow':      # navigation arrow with a notched tail, tip up
        inside = _in_triangle(x, y, (0, -0.42 * k), (-0.30 * k, 0.38 * k), (0.30 * k, 0.38 * k)) & ~_in_triangle(x, y, (-0.30 * k, 0.40 * k), (0, 0.16 * k), (0.30 * k, 0.40 * k))
        fill, edge = (255, 248, 230), (40, 30, 24)
    elif kind == 'ring':     # objective: a gold ring with a dot
        inside = ((r < k * 0.40) & (r > k * 0.27)) | (r < k * 0.12)
        fill, edge = (255, 205, 80), (60, 36, 10)
    elif kind == 'edge':     # small triangle pointing up, for an objective outside the window
        inside = _in_triangle(x, y, (0, -0.32 * k), (-0.26 * k, 0.26 * k), (0.26 * k, 0.26 * k))
        fill, edge = (255, 205, 80), (60, 36, 10)
    else:                    # landmark dot
        inside = r < k * 0.30
        fill, edge = (255, 255, 255), (30, 30, 34)
    inside = inside.astype(np.float32)
    outline = ndimage.binary_dilation(inside > 0.5, iterations=int(3 * ss)).astype(np.float32)
    rgba = np.zeros((n, n, 4), np.float32)
    rgba[..., :3] = np.array(edge, np.float32)
    rgba[..., 3] = outline
    rgba[inside > 0.5, :3] = np.array(fill, np.float32)
    im = Image.fromarray(np.clip(rgba * [1, 1, 1, 255], 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)
    return np.asarray(im)


def icons():
    (OUT / 'UI').mkdir(parents=True, exist_ok=True)
    for kind in ('arrow', 'ring', 'edge', 'dot'):
        Image.fromarray(C.bleed(np.ascontiguousarray(shape_icon(kind)))).save(OUT / 'UI' / ('Icon_%s.png' % kind))
    print('minimap icons written')


def main():
    for sub in ('Props', 'UI', 'Terrain'):
        (OUT / sub).mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    meta = {}
    for name, rel, haze in (('Cottage', 'environment/isometric-static-props/valley_apothecary_cottage_iso.png', 0.35),
                            ('Willow', 'environment/isometric-static-props/willow_tree_iso.png', 0.45),
                            ('Spring', 'environment/isometric-static-props/spring_ruins_iso.png', 0.35)):
        img = C.clean_alpha(C.load(rel), haze_alpha=haze, haze_distance=2.0, min_island=120)
        img, origin = crop_alpha(img)
        Image.fromarray(C.bleed(img)).save(OUT / 'Props' / (name + '.png'), optimize=True)
        prof = bottom_profile(img)
        meta[name] = dict(source=rel, size=list(img.shape[1::-1]), cropOrigin=list(origin), bottomProfile=[int(v) for v in prof[::8]], bottomStep=8)
        C.on(img, (255, 255, 255)).save(WORK / (name + '-on-white.png'))
        C.on(img, (0, 0, 0)).save(WORK / (name + '-on-black.png'))
        if name == 'Spring':
            dim, frac = dormant(img)
            Image.fromarray(C.bleed(dim)).save(OUT / 'Props' / 'SpringDormant.png', optimize=True)
            C.on(dim, (104, 140, 84)).save(WORK / 'SpringDormant-on-green.png')
            meta['SpringDormant'] = dict(source=rel, size=list(dim.shape[1::-1]), recolouredWaterFraction=round(frac, 4), note='same painting, water and foam recoloured by script')
        print(name, meta[name]['size'])
    (OUT / 'Props' / 'Props.json').write_text(json.dumps(meta, indent=1), encoding='utf-8')

    # ground foliage atlas: three clumps at 384 px height each (about 0.5-0.9 m in the world)
    items = []
    for key, rel in (('grass', 'environment/foliage/meadow_grass_clump.png'), ('flowers', 'environment/foliage/valley_wildflowers.png'), ('fern', 'environment/foliage/forest_fern.png')):
        img = C.clean_alpha(C.load(rel), haze_alpha=0.45, haze_distance=1.5, min_island=200)
        img, _ = crop_alpha(img, 2)
        img = C.resize(img, 384.0 / img.shape[0])
        items.append((key, img))
    W = sum(i.shape[1] for _, i in items) + 4 * len(items)
    W2 = 1 << int(np.ceil(np.log2(W)))
    atlas = np.zeros((512, W2, 4), np.uint8)
    x = 0
    rects = {}
    for key, img in items:
        h, w = img.shape[:2]
        atlas[512 - h:512, x:x + w] = img
        rects[key] = dict(x=x, y=0, w=w, h=h)         # y from the bottom (Unity UV)
        x += w + 4
    Image.fromarray(C.bleed(atlas)).save(OUT / 'Props' / 'Foliage.png', optimize=True)
    (OUT / 'Props' / 'Foliage.json').write_text(json.dumps(dict(atlas=[W2, 512], items=rects), indent=1), encoding='utf-8')
    C.on(atlas, (104, 140, 84)).save(WORK / 'Foliage-on-green.png')
    print('foliage atlas', W2, 512, list(rects))

    # minimap frame: alpha cleaned (no red fringe, no faint blobs in the open centre), 512 px
    frame = C.clean_alpha(C.load('ui/minimap_frame.png'), haze_alpha=0.5, haze_distance=1.0, min_island=400)
    frame = C.resize(frame, 512.0 / frame.shape[0])
    Image.fromarray(C.bleed(frame)).save(OUT / 'UI' / 'MinimapFrame.png', optimize=True)
    a = frame[..., 3]
    centre = a[int(0.25 * a.shape[0]):int(0.75 * a.shape[0]), int(0.25 * a.shape[1]):int(0.75 * a.shape[1])]
    print('minimap frame centre max alpha', int(centre.max()))
    # the open window inside the frame: the largest centred square whose pixels are all transparent
    n = a.shape[0]
    half = 2
    while half < n // 2 and a[n // 2 - half:n // 2 + half, n // 2 - half:n // 2 + half].max() < 8:
        half += 1
    (OUT / 'UI' / 'MinimapFrame.json').write_text(json.dumps(dict(size=n, windowFraction=round((2 * half - 6) / n, 4)), indent=1), encoding='utf-8')
    print('minimap window fraction', round((2 * half - 6) / n, 4))
    icons()

    # cobble ground texture, seam-blended, 1024 px
    stone = np.asarray(Image.open(C.SRC / 'environment/materials/mossy_stone_tile.png').convert('RGB'))
    stone = cv2.resize(stone, (1024, 1024), interpolation=cv2.INTER_AREA)
    fixed = wrap_blend(stone, 40)
    Image.fromarray(fixed).save(OUT / 'Terrain' / 'PlazaCobble.png', optimize=True)
    tile = Image.fromarray(fixed).resize((400, 400))
    t2 = Image.new('RGB', (800, 800))
    for dx in (0, 400):
        for dy in (0, 400):
            t2.paste(tile, (dx, dy))
    t2.save(WORK / 'PlazaCobble-tiled2x2.png')
    f = fixed.astype(np.float32)
    print('cobble wrap ratio x %.2f y %.2f' % (np.abs(f[:, 0] - f[:, -1]).mean() / np.abs(np.diff(f, axis=1)).mean(), np.abs(f[0] - f[-1]).mean() / np.abs(np.diff(f, axis=0)).mean()))


if __name__ == '__main__':
    main()
