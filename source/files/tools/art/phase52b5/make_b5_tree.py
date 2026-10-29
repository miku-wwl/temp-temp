"""Phase 5.2-B5 painted broadleaf tree card from the owner's broadleaf_tree.png (no drawing: alpha cleanup, crop, bleed).

python -I tools/art/phase52b5/make_b5_tree.py [--src folder] [--out folder] [--audit folder]
Outputs (default game/Assets/Echoes/Art/Phase52B5/Props/): BroadleafTree.png and BroadleafTree.json (size and the trunk's foot in
image pixels, y down: the point that stands on the old tree's root). The red and yellow edge glow of the generated image is removed
(charlib.clean_alpha: weak haze away from the drawing goes, edge texels take the nearest solid colour).
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / 'tools/art/phase52b4'))
import charlib as C  # noqa: E402

TRUNK_FOOT = (690, 1185)     # the centre of the root flare where the trunk meets the ground (checked on a gridded crop)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Source'))
    ap.add_argument('--out', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Props'))
    ap.add_argument('--audit', default=str(ROOT / 'artifacts/visual-qa/phase-5.2/b5/asset-audit/tree'))
    args = ap.parse_args()
    src, out, audit = Path(args.src), Path(args.out), Path(args.audit)
    out.mkdir(parents=True, exist_ok=True)
    audit.mkdir(parents=True, exist_ok=True)
    img = np.asarray(Image.open(src / 'broadleaf_tree.png').convert('RGBA'))
    clean = C.clean_alpha(img, haze_alpha=0.40, haze_distance=2.0, min_island=150)
    x0, x1, y0, y1 = C.bbox(clean, 8)
    x0, y0 = max(0, x0 - 6), max(0, y0 - 6)
    w = (min(clean.shape[1], x1 + 6) - x0 + 3) // 4 * 4
    h = (min(clean.shape[0], y1 + 6) - y0 + 3) // 4 * 4
    padded = np.pad(clean, ((0, 8), (0, 8), (0, 0)))
    crop = C.bleed(C.decontaminate(padded[y0:y0 + h, x0:x0 + w]))
    Image.fromarray(crop).save(out / 'BroadleafTree.png', optimize=True)
    foot = [TRUNK_FOOT[0] - x0, TRUNK_FOOT[1] - y0]
    a = crop[..., 3] > 128
    meta = dict(source='broadleaf_tree.png', size=[int(w), int(h)], cropOrigin=[int(x0), int(y0)], trunkFootPx=[int(foot[0]), int(foot[1])],
                opaqueTopPx=int(np.argmax(a.any(1))), opaqueBottomPx=int(h - 1 - np.argmax(a.any(1)[::-1])),
                removedHazeTexels=int(((img[..., 3] > 0) & (clean[..., 3] == 0)).sum()))
    (out / 'BroadleafTree.json').write_text(json.dumps(meta, indent=1), encoding='utf-8')
    for name, colour in (('grass', (96, 128, 72)), ('black', (0, 0, 0)), ('white', (255, 255, 255))):
        C.on(crop, colour).save(audit / ('BroadleafTree-on-%s.png' % name))
    print(json.dumps(meta))


if __name__ == '__main__':
    main()
