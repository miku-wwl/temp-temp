"""Phase 5.2-B5 garden plants from the owner's farm_raised_beds.png (no drawing: plants cut from the painting, recoloured, packed).

python -I tools/art/phase52b5/make_b5_garden.py [--src folder] [--out folder] [--audit folder]

The painting shows three raised beds drawn for a lower view than the game's 48 degree camera, in a layout that is not the garden's
(four beds in a fenced 8 x 6 m square). Laid over the garden as one picture, its bed edges would cross the real fence lines and its
plants would not stand where the beds are. So the painting is split, as the owner's brief allows: the beds and the soil are built in
3D on the real garden layout (Phase52B5Builder), and the plants are cut from the painting one by one (each box below holds one plant
or a tight group, checked on gridded crops) and stand upright on the real beds as camera-facing cards.

A plant is everything in its box that is not soil, not plank and not background, connected to the middle of the box; its edge takes
the colour of the plant inside (no brown soil halo). The withered set is the same cut-outs recoloured for the silent valley (greens to
straw and brown, flowers greyed, leaves thinned), shown on the dormant beds until the garden blooms.

Outputs (default game/Assets/Echoes/Art/Phase52B5/Props/):
  GardenPlants.png    atlas: every plant lush and withered, and a mossy boulder (cut on a soft ellipse), straight alpha, colour bled
                      into transparent texels
  GardenPlants.json   a list of plants: name, kind, height in metres, atlas rects (y from the bottom, Unity UV) of the lush and withered cut-out
Audit (default artifacts/visual-qa/phase-5.2/b5/asset-audit/garden/): the cut-outs on soil, grass and black, and the boxes on the painting.
"""
import argparse
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / 'tools/art/phase52b4'))
import charlib as C  # noqa: E402

# name, kind, box (x0, y0, x1, y1) in farm_raised_beds.png, height in metres when it stands on a bed
PLANTS = [
    ('carrotA', 'carrot', (105, 325, 240, 458), 0.42),
    ('carrotB', 'carrot', (265, 300, 400, 432), 0.42),
    ('lettuceA', 'lettuce', (402, 385, 520, 482), 0.30),
    ('lettuceB', 'lettuce', (248, 460, 340, 540), 0.26),
    ('redLettuce', 'redlettuce', (520, 425, 595, 508), 0.30),
    ('seedlingA', 'seedling', (448, 543, 497, 598), 0.18),
    ('seedlingB', 'seedling', (528, 503, 582, 562), 0.18),
    ('lavender', 'lavender', (570, 62, 845, 232), 0.78),
    ('daisies', 'daisy', (682, 236, 795, 322), 0.36),
    ('rosemary', 'rosemary', (822, 148, 902, 322), 0.62),
    ('oregano', 'herb', (612, 232, 685, 302), 0.28),
    ('chives', 'chives', (990, 456, 1094, 596), 0.46),
    ('basil', 'basil', (884, 540, 1046, 652), 0.32),
    ('purpleBasil', 'purplebasil', (1088, 508, 1192, 602), 0.32),
    ('thyme', 'thyme', (840, 590, 930, 720), 0.40),
    ('seedlingC', 'seedling', (1098, 598, 1212, 672), 0.20),
]


def hsv(rgb_u8):
    return cv2.cvtColor(rgb_u8.astype(np.float32) / 255, cv2.COLOR_RGB2HSV)


def plant_mask(rgba):
    """True where the box shows plant: not background, not soil, not plank."""
    h = hsv(rgba[..., :3])
    H, S, V = h[..., 0], h[..., 1], h[..., 2]
    a = rgba[..., 3] > 100
    soil = (H >= 8) & (H <= 40) & (V < 0.50) & (S > 0.30)
    dark = V < 0.13
    plank = (H >= 16) & (H <= 44) & (S > 0.22) & (S < 0.62) & (V >= 0.38)
    carrot_root = (H >= 14) & (H <= 40) & (S >= 0.62) & (V > 0.55)
    path = (H >= 20) & (H <= 42) & (S > 0.30) & (V > 0.50) & (S < 0.62)
    return a & ~((soil | dark | plank | path) & ~carrot_root)


def cut(painting, box):
    x0, y0, x1, y1 = box
    rgba = painting[y0:y1, x0:x1].copy()
    m = plant_mask(rgba)
    m = ndimage.binary_opening(m, iterations=1)
    lab, n = ndimage.label(ndimage.binary_closing(m, iterations=2))
    hh, ww = m.shape
    seed = np.zeros_like(m)
    seed[int(hh * 0.25):int(hh * 0.85), int(ww * 0.25):int(ww * 0.75)] = True
    keep = np.unique(lab[seed & (lab > 0)])
    if len(keep) == 0:
        raise ValueError('no plant found in box %s' % (box,))
    sizes = ndimage.sum(m, lab, keep)
    keep = [k for k, s in zip(keep, sizes) if s > 0.03 * m.sum()]
    m = np.isin(lab, keep) & m
    # only pin-holes are filled: soil, planks and gaps between stems stay open
    holes, nh = ndimage.label(ndimage.binary_fill_holes(m) & ~m)
    if nh:
        hole_sizes = ndimage.sum(np.ones_like(m), holes, range(1, nh + 1))
        m |= np.isin(holes, [i + 1 for i, s in enumerate(hole_sizes) if s < 40])
    # soft edge: one pixel of falloff, and a fade where the box cuts through a neighbouring plant (sides and top 10 px, the foot 14 px)
    soft = np.clip(cv2.GaussianBlur(m.astype(np.float32), (0, 0), 0.7) * 1.25 - 0.1, 0, 1)
    yy, xx = np.mgrid[0:hh, 0:ww].astype(np.float32)
    edge = np.minimum(np.minimum(xx, ww - 1 - xx), yy) / 10.0
    edge = np.minimum(edge, (hh - 1 - yy) / 14.0)   # a soft foot: on open grass the cut would read as a sticker's edge
    soft *= np.clip(edge, 0, 1) ** 0.8
    # the lower part narrows towards the foot (a clump, not a block), round the plant's own centre column
    cols = (soft > 0.5).sum(0).astype(np.float32)
    cx = float((cols * np.arange(ww)).sum() / max(1.0, cols.sum()))
    t = np.clip((yy - 0.5 * hh) / (0.5 * hh), 0, 1)
    allowed = 1.0 - 0.42 * t ** 1.5
    dist = np.abs(xx - cx) / max(1.0, 0.5 * ww)
    soft *= np.clip((allowed + 0.08 - dist) / 0.16, 0, 1)
    # and the box's corners are rounded (a superellipse): a dense clump must not keep the rectangle it was cut with
    r = (np.abs((xx - (ww - 1) / 2) / (0.5 * ww)) ** 2.6 + np.abs((yy - (hh - 1) / 2) / (0.5 * hh)) ** 2.6) ** (1 / 2.6)
    soft *= np.clip((1.04 - r) / 0.14, 0, 1)
    out = rgba.copy()
    out[..., 3] = np.clip(np.minimum(rgba[..., 3] / 255.0, soft) * 255 + 0.5, 0, 255).astype(np.uint8)
    out = C.decontaminate(out, solid=230)
    # crop to the plant
    x0c, x1c, y0c, y1c = C.bbox(out, 8)
    return np.ascontiguousarray(out[max(0, y0c - 3):y1c + 3, max(0, x0c - 3):x1c + 3])


# name, centre (x, y), half axes (x, y) in farm_raised_beds.png, height in metres: mossy boulders for the valley's rocks
ROCKS = [
    ('rockA', (1030, 893), (58, 54), 1.0),   # the grey mossy boulder (the two sandy stones of the painting read as bread rolls on grass)
]


def cut_ellipse(painting, centre, axes):
    """A boulder: everything inside a soft ellipse round it (its moss and the grass at its foot fade out with the edge)."""
    cx, cy = centre
    ax, ay = axes
    x0, y0, x1, y1 = cx - ax - 4, cy - ay - 4, cx + ax + 5, cy + ay + 5
    rgba = painting[y0:y1, x0:x1].copy()
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    r = np.sqrt(((xx - cx) / ax) ** 2 + ((yy - cy) / ay) ** 2)
    soft = np.clip((1.0 - r) / 0.16, 0, 1)
    out = rgba.copy()
    out[..., 3] = np.clip(np.minimum(rgba[..., 3] / 255.0, soft) * 255 + 0.5, 0, 255).astype(np.uint8)
    return C.decontaminate(out, solid=230)


def wither(rgba, rng):
    """The silent valley's garden: greens to straw and brown, purple and white flowers greyed, the leaves thinned a little."""
    h = hsv(rgba[..., :3])
    H, S, V = h[..., 0], h[..., 1], h[..., 2]
    green = (H > 45) & (H < 170)
    purple = (H >= 230) & (H < 335)
    red = (H >= 335) | (H < 12)
    white = (S < 0.22) & (V > 0.7)
    noise = cv2.GaussianBlur(rng.random(H.shape).astype(np.float32), (0, 0), 2.0)
    noise = (noise - noise.mean()) / max(1e-4, noise.std())
    H2, S2, V2 = H.copy(), S.copy(), V.copy()
    H2[green] = 34 + 9 * noise[green]                      # straw to brown, mottled
    S2[green] = np.clip(S[green] * 0.55 + 0.08, 0, 1)
    V2[green] = V[green] * 0.78
    H2[purple | red] = 28
    S2[purple | red] = S[purple | red] * 0.32
    V2[purple | red] = V[purple | red] * 0.66
    S2[white] = 0.22
    H2[white] = 38
    V2[white] = V[white] * 0.62
    out = rgba.copy()
    out[..., :3] = np.clip(cv2.cvtColor(np.stack([H2 % 360, S2, V2], -1).astype(np.float32), cv2.COLOR_HSV2RGB) * 255 + 0.5, 0, 255).astype(np.uint8)
    # thinner: erode the alpha a pixel and drop a few speckles of leaf
    a = out[..., 3].astype(np.float32) / 255
    a = cv2.erode(a, np.ones((2, 2), np.uint8))
    holes = (cv2.GaussianBlur(rng.random(a.shape).astype(np.float32), (0, 0), 1.2) > 0.62)
    a = np.where(holes, a * 0.2, a)
    out[..., 3] = np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


def pack(items, width=2048):
    """Shelf packing; returns the atlas and the rect of every item (x, y from the top)."""
    items = sorted(items, key=lambda kv: -kv[1].shape[0])
    x = y = shelf = 0
    rects = {}
    for key, img in items:
        h, w = img.shape[:2]
        if x + w + 4 > width:
            x, y = 0, y + shelf + 4
            shelf = 0
        rects[key] = (x, y, w, h)
        x += w + 4
        shelf = max(shelf, h)
    height = 1 << int(np.ceil(np.log2(y + shelf + 4)))
    atlas = np.zeros((height, width, 4), np.uint8)
    for key, img in items:
        x0, y0, w, h = rects[key]
        atlas[y0:y0 + h, x0:x0 + w] = img
    return atlas, rects


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Source'))
    ap.add_argument('--out', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Props'))
    ap.add_argument('--audit', default=str(ROOT / 'artifacts/visual-qa/phase-5.2/b5/asset-audit/garden'))
    args = ap.parse_args()
    src, out, audit = Path(args.src), Path(args.out), Path(args.audit)
    out.mkdir(parents=True, exist_ok=True)
    audit.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(5206)
    painting = np.asarray(Image.open(src / 'farm_raised_beds.png').convert('RGBA'))
    painting = C.clean_alpha(painting, haze_alpha=0.35, haze_distance=2.0, min_island=150)
    items = []
    meta = dict(source='farm_raised_beds.png', plants=[])
    for name, kind, box, height in PLANTS:
        lush = cut(painting, box)
        dry = wither(lush, rng)
        items.append((name, lush))
        items.append((name + '_withered', dry))
        meta['plants'].append(dict(name=name, kind=kind, box=list(box), heightMetres=height, pixels=[int(lush.shape[1]), int(lush.shape[0])]))
    for name, centre, axes, height in ROCKS:
        rock = cut_ellipse(painting, centre, axes)
        items.append((name, rock))
        items.append((name + '_withered', rock))   # a stone does not wither: both rects show the same cut-out
        meta['plants'].append(dict(name=name, kind='rock', box=[centre[0] - axes[0], centre[1] - axes[1], centre[0] + axes[0], centre[1] + axes[1]], heightMetres=height,
                                   pixels=[int(rock.shape[1]), int(rock.shape[0])]))
    atlas, rects = pack(items)
    H = atlas.shape[0]
    for plant in meta['plants']:
        for key, label in ((plant['name'], 'lush'), (plant['name'] + '_withered', 'withered')):
            x, y, w, h = rects[key]
            plant[label] = dict(x=int(x), y=int(H - y - h), w=int(w), h=int(h))   # y from the bottom (UV)
    meta['atlas'] = [int(atlas.shape[1]), int(H)]
    Image.fromarray(C.bleed(atlas)).save(out / 'GardenPlants.png', optimize=True)
    (out / 'GardenPlants.json').write_text(json.dumps(meta, indent=1), encoding='utf-8')
    soil = (70, 48, 32)
    C.on(atlas, soil).save(audit / 'GardenPlants-on-soil.png')
    C.on(atlas, (96, 128, 72)).save(audit / 'GardenPlants-on-grass.png')
    C.on(atlas, (0, 0, 0)).save(audit / 'GardenPlants-on-black.png')
    boxes = Image.fromarray(painting).convert('RGBA')
    bg = Image.new('RGBA', boxes.size, (40, 40, 40, 255))
    bg.alpha_composite(boxes)
    d = ImageDraw.Draw(bg)
    for name, _, (x0, y0, x1, y1), _ in PLANTS:
        d.rectangle([x0, y0, x1, y1], outline=(255, 0, 255, 255), width=2)
        d.text((x0 + 3, y0 + 2), name, fill=(255, 255, 0, 255))
    bg.convert('RGB').save(audit / 'plant-boxes-on-painting.png')
    print('atlas', meta['atlas'], 'plants', len(meta['plants']))


if __name__ == '__main__':
    main()
