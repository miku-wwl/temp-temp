"""Phase 5.2-B4 asset audit: alpha/halo checks on four backgrounds, tile-seam checks, run-sheet pose detection.

python tools/art/phase52b4/audit_assets.py
Reads the unmodified owner-supplied PNGs in game/Assets/Echoes/Art/Phase52B4/Source and writes measurements and review sheets to
artifacts/art-work/phase52b4/audit/ (nothing in the source folder is touched). The A/B/C verdicts are made by looking at these sheets.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'game/Assets/Echoes/Art/Phase52B4/Source'
OUT = ROOT / 'artifacts/art-work/phase52b4/audit'
BACKGROUNDS = {'white': (255, 255, 255), 'black': (0, 0, 0), 'grass': (92, 140, 64), 'earth': (190, 160, 112)}


def halo_stats(rgba):
    """Semi-transparent pixels outside the solid figure: how many there are, how far they reach and what colour they carry."""
    a = rgba[..., 3].astype(np.float32) / 255
    solid = a > 0.95
    semi = (a > 0.03) & (a <= 0.95)
    dist = ndimage.distance_transform_edt(~solid)
    far = semi & (dist > 3)                      # more than 3 px away from any solid pixel: haze, glow or a background remnant
    rgb = rgba[..., :3].astype(np.float32)
    red = semi & (rgb[..., 0] > rgb[..., 1] * 1.6) & (rgb[..., 0] > 90)
    return dict(solidFraction=round(float(solid.mean()), 4), semiFraction=round(float(semi.mean()), 4), farHazeFraction=round(float(far.mean()), 4),
                farHazeMaxDistancePx=round(float(dist[far].max()) if far.any() else 0.0, 1), redFringePixels=int(red.sum()),
                hazeMeanColour=[round(float(v)) for v in (rgb[far].mean(0) if far.any() else np.zeros(3))])


def seam_stats(rgb):
    """Discontinuity at the wrap edges compared with ordinary neighbouring columns/rows (1.0 = seamless)."""
    f = rgb.astype(np.float32)
    interior_x = np.abs(np.diff(f, axis=1)).mean()
    interior_y = np.abs(np.diff(f, axis=0)).mean()
    wrap_x = np.abs(f[:, 0] - f[:, -1]).mean()
    wrap_y = np.abs(f[0] - f[-1]).mean()
    # low-frequency mismatch: mean colour of the outer bands
    band = max(8, f.shape[0] // 32)
    lf_x = np.abs(f[:, :band].mean((0, 1)) - f[:, -band:].mean((0, 1))).mean()
    lf_y = np.abs(f[:band].mean((0, 1)) - f[-band:].mean((0, 1))).mean()
    return dict(wrapRatioX=round(float(wrap_x / interior_x), 2), wrapRatioY=round(float(wrap_y / interior_y), 2), bandMeanDiffX=round(float(lf_x), 1), bandMeanDiffY=round(float(lf_y), 1))


def poses(rgba, min_area=20000):
    a = rgba[..., 3] > 40
    a = ndimage.binary_closing(a, iterations=6)
    lab, n = ndimage.label(a)
    out = []
    for i, sl in enumerate(ndimage.find_objects(lab), start=1):
        area = int((lab[sl] == i).sum())
        if area < min_area:
            continue
        out.append(dict(x0=sl[1].start, x1=sl[1].stop, y0=sl[0].start, y1=sl[0].stop, area=area))
    return sorted(out, key=lambda b: b['x0'])


def on_backgrounds(rgba, height=300):
    im = Image.fromarray(rgba)
    scale = height / im.size[1]
    im = im.resize((max(1, int(im.size[0] * scale)), height), Image.LANCZOS)
    tiles = []
    for name, c in BACKGROUNDS.items():
        bg = Image.new('RGBA', im.size, c + (255,))
        bg.alpha_composite(im)
        tiles.append(bg.convert('RGB'))
    sheet = Image.new('RGB', (im.size[0] * 4, height))
    for i, t in enumerate(tiles):
        sheet.paste(t, (i * im.size[0], 0))
    return sheet


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    report = {}
    sheets = []
    for p in sorted(SRC.rglob('*.png')):
        rel = p.relative_to(SRC).as_posix()
        im = Image.open(p)
        rgba = np.asarray(im.convert('RGBA'))
        entry = dict(size=list(im.size), mode=im.mode)
        if im.mode == 'RGBA':
            entry['alpha'] = halo_stats(rgba)
            sheet = on_backgrounds(rgba)
            sheet.save(OUT / (rel.replace('/', '__') + '.backgrounds.png'))
            sheets.append((rel, sheet))
        else:
            entry['alpha'] = 'opaque (no alpha channel)'
        if rel.startswith('environment/materials/'):
            entry['seam'] = seam_stats(np.asarray(im.convert('RGB')))
            tile = Image.open(p).convert('RGB').resize((512, 512), Image.LANCZOS)
            t2 = Image.new('RGB', (1024, 1024))
            for dx in (0, 512):
                for dy in (0, 512):
                    t2.paste(tile, (dx, dy))
            t2.save(OUT / (rel.replace('/', '__') + '.tiled2x2.png'))
        if rel.startswith('characters/run-keyposes/'):
            entry['poses'] = poses(rgba)
        report[rel] = entry
        print(rel, json.dumps({k: v for k, v in entry.items() if k != 'poses'}), ('poses=%d' % len(entry['poses'])) if 'poses' in entry else '')
    (OUT / 'audit.json').write_text(json.dumps(report, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
