"""Phase 5.2-B5 ground textures from the owner's meadow grass and tilled soil paintings (no drawing: seam repair, grading, resampling).

python -I tools/art/phase52b5/make_b5_ground.py [--src folder] [--out folder] [--audit folder]

The supplied textures do not wrap (their opposite edges differ 1.2-1.7 times more than neighbouring pixels inside). Each is made
tileable by a minimum-error cut through a wrap-around overlap band (image quilting applied to the texture's own edges): the output
loses the band's width, every pixel is still a painted pixel, and nothing is cross-faded into a blur. The horizontal pass cuts
top to bottom; the vertical pass cuts left to right along a path that starts and ends on the same row, so the horizontal wrap
survives. The band is as wide as the painting is larger than 1024 px (230 px for the 1254 px sources), so the cut result is the
final 1024 x 1024 texture with no resampling (a non-integer resize averages the wrap pair differently from the inner pairs). Then
the colours are graded (hue, saturation, value about the texture's own mean, so the painted variation stays).

Outputs (default game/Assets/Echoes/Art/Phase52B5/Terrain/):
  MeadowA.png   the lush grass layer (graded from meadow_grass_albedo: less yellow and less saturated, to sit with the B4 drawings)
  MeadowB.png   a warmer, drier grass for the large meadow patches (the same painting turned 90 degrees, so the two never repeat together)
  TilledSoil.png   garden soil (from tilled_soil_albedo, slightly less saturated)
  PlazaCobbleWarm.png   the B4 plaza cobble (B4's own seam-blended texture) as warm grey stone for the B5 plaza (terrain and ruins)
  Ground.json   the parameters and the wrap measurements of every output
Audit images (default artifacts/visual-qa/phase-5.2/b5/asset-audit/ground/): 4 x 4 repeats before and after, and the seam crossings.
"""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
SIZE = 1024         # final texture; the overlap band is the source size minus this
FEATHER = 2.0       # px of soft transition along the cut (the cut itself already runs where both bands agree)


def cut_path(err, periodic=False):
    """Minimum-cost path through err (rows x cols), one column index per row, moving at most one column per row.
    periodic: the path must end in the column it started from (tried for every start column)."""
    rows, cols = err.shape
    lo, hi = 6, cols - 6                      # keep the feather inside the band
    big = np.float32(1e12)
    e = err.astype(np.float32).copy()
    e[:, :lo] = big
    e[:, hi:] = big
    if not periodic:
        cost = e[0].copy()
        back = np.zeros((rows, cols), np.int8)
        for y in range(1, rows):
            left = np.concatenate([[big], cost[:-1]])
            right = np.concatenate([cost[1:], [big]])
            stack = np.stack([left, cost, right])
            k = np.argmin(stack, 0)
            back[y] = (k - 1).astype(np.int8)
            cost = e[y] + stack[k, np.arange(cols)]
        x = int(np.argmin(cost))
        path = np.zeros(rows, np.int32)
        for y in range(rows - 1, -1, -1):
            path[y] = x
            x += int(back[y, x])
        return path
    starts = np.arange(lo, hi)
    S = len(starts)
    cost = np.full((S, cols), big, np.float32)
    cost[np.arange(S), starts] = e[0, starts]
    back = np.zeros((rows, S, cols), np.int8)
    for y in range(1, rows):
        left = np.concatenate([np.full((S, 1), big, np.float32), cost[:, :-1]], 1)
        right = np.concatenate([cost[:, 1:], np.full((S, 1), big, np.float32)], 1)
        stack = np.stack([left, cost, right])
        k = np.argmin(stack, 0)
        back[y] = (k - 1).astype(np.int8)
        cost = e[y][None, :] + np.take_along_axis(stack, k[None], 0)[0]
    finals = cost[np.arange(S), starts]
    best = int(np.argmin(finals))
    x = int(starts[best])
    path = np.zeros(rows, np.int32)
    for y in range(rows - 1, -1, -1):
        path[y] = x
        x += int(back[y, best, x])
    return path


def wrap_x(img, band, periodic):
    """Width shrinks by band; the left band of the result is the original's right band left of the cut and its left band right of it."""
    h, w = img.shape[:2]
    a = img[:, w - band:]                     # continues the result's last column (original column w - band - 1)
    b = img[:, :band]                         # continues into the result's column band (original column band)
    err = ((a - b) ** 2).sum(-1)
    path = cut_path(err, periodic)
    xs = np.arange(band)[None, :]
    t = np.clip((xs - path[:, None] + 0.5) / FEATHER + 0.5, 0, 1)[..., None]     # 0 left of the cut (a), 1 right of it (b)
    seam = a * (1 - t) + b * t
    return np.concatenate([seam, img[:, band:w - band]], 1), path


def make_tileable(rgb, size=SIZE):
    """A size x size tile: the overlap band of each pass is the painting's extra width (or height) over size."""
    h, w = rgb.shape[:2]
    if min(h, w) - size < 64 or max(h, w) - size > min(h, w) // 2:
        raise ValueError('expected a square-ish painting 64 px or more larger than %d px, got %d x %d' % (size, w, h))
    f = rgb.astype(np.float32)
    f, _ = wrap_x(f, w - size, periodic=False)
    g, _ = wrap_x(np.ascontiguousarray(f.transpose(1, 0, 2)), h - size, periodic=True)
    return np.clip(g.transpose(1, 0, 2), 0, 255)


def circular_mean_hue(h_deg, weight):
    a = np.deg2rad(h_deg)
    return float(np.rad2deg(np.arctan2((np.sin(a) * weight).sum(), (np.cos(a) * weight).sum())) % 360)


def grade(rgb, hue, sat, val, warm=0.0):
    """Moves the mean hue to `hue` (degrees), scales saturation so its mean becomes `sat` and value so its mean becomes `val`;
    each pixel keeps its offset from the mean, so the painted light and dark strokes stay. warm adds a little red to the lights."""
    f = rgb.astype(np.float32) / 255
    hsv = cv2.cvtColor(f, cv2.COLOR_RGB2HSV)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    mh = circular_mean_hue(h, s * v)
    hsv[..., 0] = (h + (hue - mh)) % 360
    hsv[..., 1] = np.clip(s * (sat / max(1e-4, s.mean())), 0, 1)
    hsv[..., 2] = np.clip(v * (val / max(1e-4, v.mean())), 0, 1)
    out = cv2.cvtColor(hsv, cv2.COLOR_HSV2RGB)
    if warm:
        out[..., 0] = np.clip(out[..., 0] + warm * out[..., 1], 0, 1)
    return np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)


def wrap_metrics(rgb):
    f = rgb.astype(np.float32)
    ix = np.abs(np.diff(f, axis=1)).mean()
    iy = np.abs(np.diff(f, axis=0)).mean()
    return dict(seamRatioX=round(float(np.abs(f[:, 0] - f[:, -1]).mean() / ix), 3), seamRatioY=round(float(np.abs(f[0] - f[-1]).mean() / iy), 3))


def hsv_mean(rgb):
    f = rgb.astype(np.float32) / 255
    hsv = cv2.cvtColor(f, cv2.COLOR_RGB2HSV)
    return dict(hueDeg=round(circular_mean_hue(hsv[..., 0], hsv[..., 1] * hsv[..., 2]), 1), saturation=round(float(hsv[..., 1].mean()), 3), value=round(float(hsv[..., 2].mean()), 3),
                meanRGB=[round(float(c), 1) for c in rgb.reshape(-1, 3).mean(0)])


def tiled(rgb, n=4, grid=True):
    h, w = rgb.shape[:2]
    im = Image.fromarray(np.tile(rgb, (n, n, 1)))
    if grid:
        d = ImageDraw.Draw(im)
        for i in range(1, n):
            d.line([(i * w, 0), (i * w, n * h)], fill=(255, 0, 255), width=2)
            d.line([(0, i * h), (n * w, i * h)], fill=(255, 0, 255), width=2)
    return im


def crossing(rgb, r=140):
    h, w = rgb.shape[:2]
    return tiled(rgb, 2, False).crop((w - r, h - r, w + r, h + r)).resize((4 * r, 4 * r), Image.NEAREST)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Source'))
    ap.add_argument('--out', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Terrain'))
    ap.add_argument('--audit', default=str(ROOT / 'artifacts/visual-qa/phase-5.2/b5/asset-audit/ground'))
    args = ap.parse_args()
    src, out, audit = Path(args.src), Path(args.out), Path(args.audit)
    out.mkdir(parents=True, exist_ok=True)
    audit.mkdir(parents=True, exist_ok=True)
    recipes = {
        'MeadowA': dict(source='meadow_grass_albedo', rotate=0, hue=88.0, sat=0.50, val=0.50, warm=0.0),
        'MeadowB': dict(source='meadow_grass_albedo', rotate=1, hue=72.0, sat=0.44, val=0.53, warm=0.04),
        'TilledSoil': dict(source='tilled_soil_albedo', rotate=0, hue=27.0, sat=0.52, val=0.38, warm=0.0),
    }
    report = {}
    tiles = {}
    for name, r in recipes.items():
        rgb = np.asarray(Image.open(src / (r['source'] + '.png')).convert('RGB'))
        if r['source'] not in tiles:
            fixed = make_tileable(rgb).astype(np.uint8)
            tiles[r['source']] = fixed
            # before / after repeats at the same scale (half size), the source first
            for label, img in (('original', rgb), ('tileable', fixed)):
                im = tiled(img)
                im.resize((im.width // 3, im.height // 3), Image.LANCZOS).save(audit / ('%s-4x4-%s.jpg' % (r['source'], label)), quality=92)
                crossing(img).save(audit / ('%s-seam-crossing-%s.png' % (r['source'], label)))
        fixed = np.rot90(tiles[r['source']], r['rotate']).copy()
        graded = grade(fixed, r['hue'], r['sat'], r['val'], r['warm'])
        final = graded
        Image.fromarray(final).save(out / (name + '.png'), optimize=True)
        im = tiled(final, 4, False)
        im.resize((im.width // 2, im.height // 2), Image.LANCZOS).save(audit / ('%s-4x4-final-no-grid.jpg' % name), quality=92)
        report[name] = dict(recipe=r, sourceWrap=wrap_metrics(rgb), finalWrap=wrap_metrics(final), sourceColour=hsv_mean(rgb), finalColour=hsv_mean(final),
                            size=[SIZE, SIZE], bandPx=[int(rgb.shape[1] - SIZE), int(rgb.shape[0] - SIZE)])
        print(name, json.dumps(report[name]['finalWrap']), json.dumps(report[name]['finalColour']))
    # the B4 plaza cobble (already seam-blended by make_b4_props.py), warmed so the plaza's stone sits with the warmer meadow
    cobble = np.asarray(Image.open(ROOT / 'game/Assets/Echoes/Art/Phase52B4/Terrain/PlazaCobble.png').convert('RGB'))
    # warm grey by luminance: B4's cobble has blue-grey stones among the brown ones, and a hue/saturation grade only made them bluer; here every
    # stone keeps its light and dark and a quarter of its own colour, over one warm grey (the silent valley's cool filter still reads it as stone)
    f = cobble.astype(np.float32) / 255
    lum = f @ np.float32([0.299, 0.587, 0.114])
    warm = np.clip((lum[..., None] * np.float32([1.10, 1.00, 0.84]) * 0.98 + 0.25 * (f - lum[..., None])) * 255 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(warm).save(out / 'PlazaCobbleWarm.png', optimize=True)
    report['PlazaCobbleWarm'] = dict(recipe=dict(source='Phase52B4/Terrain/PlazaCobble.png', method='luminance x (1.10, 1.00, 0.84) x 0.98 + 25 % of the original chroma'), sourceColour=hsv_mean(cobble),
                                     finalColour=hsv_mean(warm), finalWrap=wrap_metrics(warm), size=list(warm.shape[1::-1]))
    print('PlazaCobbleWarm', json.dumps(report['PlazaCobbleWarm']['finalColour']))
    (out / 'Ground.json').write_text(json.dumps(report, indent=1), encoding='utf-8')
    (audit / 'ground.json').write_text(json.dumps(report, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
