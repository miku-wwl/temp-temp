"""Assembles and zips the Phase 5.2-B5 review package (artifacts/review/phase52-b5/).

python -I tools/art/phase52b5/make_b5_review.py <evidence-root> <tests-folder> <build-dir> <build-log> <baseline-commit> [player-folder] [survey-folder]
Every image is labelled by category in its folder name and in comparison/index.html: SOURCE_ART (the owner's six candidate paintings,
unmodified, never game footage), PRODUCTION_ART (what the game draws, made from them by script), EDITOR_PREVIEW (Unity Editor renders,
diagnostics only), REAL_PLAYER_SCREENSHOT and REAL_PLAYER_VIDEO (actual Windows player, AUTOMATED input). To stay reviewable the package
holds the key real-player stills as the unmodified PNGs and the rest as labelled side-by-side sheets (JPEG); every still left out is listed
with its SHA-256 in OMITTED-FILES-SHA256.txt and stays on disk. Never overwrites an existing package; never includes Unity caches, the
official theme recording or unrelated files.
"""
import hashlib
import html
import shutil
import json
import posixpath
import re
import subprocess
import sys
import zipfile
from datetime import datetime
from pathlib import Path
from urllib.parse import unquote

root = Path(__file__).resolve().parents[3]
ev = (root / sys.argv[1]).resolve()
tests = (root / sys.argv[2]).resolve()
build_dir = (root / sys.argv[3]).resolve()
build_log = (root / sys.argv[4]).resolve()
base = sys.argv[5]
stage = root / 'artifacts/review/phase52-b5/staging'
out_dir = root / 'artifacts/review/phase52-b5'
name = 'Echoes-Phase52-B5-Painted-Environment-Review'
pl = ev / (sys.argv[6] if len(sys.argv) > 6 else 'player')
t = pl / 'tour1080-unrecorded'
video = pl / 'tour1080-video'
sheets = ev / 'sheets'


def git(*a):
    return subprocess.check_output(['git', *a], cwd=root, text=True).strip()


if stage.exists():
    stage = out_dir / ('staging-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
if stage.exists():
    raise FileExistsError('Preserve earlier evidence: ' + str(stage))
stage.mkdir(parents=True)
staged, omitted = {}, []


def put(src, rel):
    src = Path(src)
    if not src.is_file():
        print('MISSING (skipped):', src)
        return None
    dst = stage / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)
    staged[str(src.resolve())] = rel
    return dst


def omit(src):
    src = Path(src)
    omitted.append('%s  %d  %s' % (hashlib.sha256(src.read_bytes()).hexdigest(), src.stat().st_size, src.relative_to(root).as_posix()))


def put_tree(src, rel, keep=lambda p: True):
    src = Path(src)
    if not src.is_dir():
        print('MISSING folder (skipped):', src)
        return
    for p in sorted(src.rglob('*')):
        if p.is_file() and 'frames' not in p.parts and 'save' not in p.parts:
            if keep(p):
                put(p, rel + '/' + p.relative_to(src).as_posix())
            else:
                omit(p)


# ---- documents -------------------------------------------------------------------------------------------------------------------------
put(ev / 'README_FIRST_zh.md', 'README_FIRST_zh.md')
put(ev / 'final-report.md', 'final-report.md')
put(ev / 'recovery-table.md', 'recovery-table.md')
put(ev / 'finalization-audit.json', 'logs/finalization-audit.json')
put(ev / 'delivery-identifiers.json', 'delivery-identifiers.json')

# ---- motion (REAL_PLAYER_VIDEO) ----------------------------------------------------------------------------------------------------------
put(video / 'actual-player-b5-motion.mp4', 'motion/actual-player-b5-motion-AUTOMATED_INPUT.mp4')   # the input is a script: said in the name
put(video / 'contact-sheet.png', 'motion/contact-sheet.png')
put(ev / 'minimap-strip.png', 'motion/minimap-strip.png')
put_tree(video / 'selected-frames', 'motion/selected-frames')
for f in ('capture-provenance.json', 'tour-stats.json', 'tour.json', 'frame-manifest.json', 'process-result.json', 'resource-samples.json', 'ffmpeg.log'):
    put(video / f, 'motion/run-data/' + f)

# ---- real-player stills: key PNGs unmodified, the rest as sheets ---------------------------------------------------------------------------
KEY = ('after-b5-', 'before-b4-', 'stage-S3-set-by-script-spring', 'stage-S1-set-by-script-spring', 'stage-S2-set-by-script-garden', 'occlusion-behind-broadleaf',
       'ground-tiling-farthest-zoom', 'dialogue-mira', 'inventory-portrait', 'pause-menu', 'hero-walk-', 'hero-idle-S-closest-zoom', 'pitch-')
put_tree(t, 'real-player-screenshots/tour1080', keep=lambda p: (p.suffix == '.png' and p.name.startswith(KEY)) or p.name in ('tour-stats.json', 'process-result.json', 'resource-samples.json'))
put_tree(sheets, 'real-player-screenshots/sheets')
SMOKE_KEEP = ('01-', '02-', '03-', '05-', '06-', '07-', '08-', '09-', '11-', '13a', '14-', '15-', '16-', '17-', '18-', 'A1-', 'A2-', 'A3-')
put_tree(pl / 'smoke1080', 'real-player-screenshots/smoke1080', keep=lambda p: p.suffix == '.json' or (p.suffix == '.png' and p.name.startswith(SMOKE_KEEP)))
put_tree(pl / 'smoke720', 'real-player-screenshots/smoke720', keep=lambda p: p.suffix == '.json')
for tag in ('tour1080-unrecorded-2', 'tour1080-before-unrecorded', 'tour1080-before-unrecorded-2'):
    put_tree(pl / tag, 'performance/' + tag, keep=lambda p: p.suffix == '.json' and p.name != 'tour.json')
put(t / 'tour.json', 'performance/tour1080-unrecorded/tour.json')

# ---- art: source (unmodified owner pack), production art, audits ---------------------------------------------------------------------------
src = root / 'game/Assets/Echoes/Art/Phase52B5/Source'
art = root / 'game/Assets/Echoes/Art/Phase52B5'
for p in sorted(src.glob('*.png')):
    put(p, 'SOURCE_ART-owner-pack-not-game-footage/' + p.name)
put_tree(art / 'Props', 'PRODUCTION_ART/props', keep=lambda p: p.suffix in ('.png', '.json'))
put_tree(art / 'Terrain', 'PRODUCTION_ART/terrain', keep=lambda p: p.suffix in ('.png', '.json'))
put(art / 'UI/MinimapBase.png', 'PRODUCTION_ART/minimap/MinimapBase-B5.png')
put_tree(ev / 'asset-audit', 'asset-audit', keep=lambda p: p.suffix in ('.png', '.jpg', '.json'))
put_tree(ev / (sys.argv[7] if len(sys.argv) > 7 else 'survey-b5-final'), 'EDITOR_PREVIEW-not-player-evidence', keep=lambda p: p.suffix == '.txt' or p.suffix == '.png' and ('spring' in p.name or 'garden' in p.name or 'behind-tree' in p.name))

# ---- comparison page ---------------------------------------------------------------------------------------------------------------------
cmp = stage / 'comparison'
sections = []


def section(title, items, note=''):
    figs = []
    for label, category, path in items:
        if path is None or not Path(path).is_file():
            continue
        key = str(Path(path).resolve())
        if key not in staged:
            continue
        figs.append('<figure><img src="%s" loading="lazy"><figcaption><b>%s</b> &middot; %s</figcaption></figure>' % (html.escape('../' + staged[key]), html.escape(category), html.escape(label)))
    if figs:
        sections.append('<section><h2>%s</h2>%s<div class="row">%s</div></section>' % (html.escape(title), ('<p class="note">%s</p>' % html.escape(note)) if note else '', ''.join(figs)))


for place, label in (('plaza', 'Plaza'), ('workshop-front', 'Workshop, front'), ('workshop-side', 'Workshop, side'), ('willow', 'Willow'), ('spring', 'Spring (S0)'),
                     ('garden', 'Garden (S0)'), ('hen-yard', 'Hen yard'), ('stream', 'Stream bank (S0)'), ('windmill', 'Windmill approach (S0)')):
    section('%s: B4 (before mode) vs B5, same build, same place' % label,
            [('B4 presentation', 'REAL_PLAYER_SCREENSHOT', t / ('before-b4-%s.png' % place)), ('B5', 'REAL_PLAYER_SCREENSHOT', t / ('after-b5-%s.png' % place))])
section('The spring path at S1 (set by the script): B4 vs B5 (B4 grew a low-poly bloom tree here that hid the caretaker)', [('B4 vs B5, S1', 'REAL_PLAYER_SCREENSHOT', sheets / 'b4-vs-b5-S1-spring.jpg')])
for place in ('spring', 'garden', 'plaza', 'workshop-front', 'stream', 'windmill'):
    section('B5 %s through the restoration: S0 (played), S1-S3 SET BY THE EVIDENCE SCRIPT' % place, [('S0-S3 strip', 'REAL_PLAYER_SCREENSHOT', sheets / ('b5-stages-%s.jpg' % place))])
    section('%s at S3 (set by the script): B4 vs B5' % place, [('B4 vs B5, S3', 'REAL_PLAYER_SCREENSHOT', sheets / ('b4-vs-b5-S3-%s.jpg' % place))])
section('Occlusion (the visible share of the caretaker is measured, see the report)', [(p.stem, 'REAL_PLAYER_SCREENSHOT', p) for p in sorted(sheets.glob('occlusion-*.jpg'))])
section('WORST occlusion measured in B5', [('worst case', 'REAL_PLAYER_SCREENSHOT', sheets / 'worst-occlusion-b5.jpg')])
section('Ground textures repeated 4 x 4: the owner\'s originals (magenta lines at the tile edges) and the seam-repaired tiles',
        [('meadow original', 'SOURCE_ART', ev / 'asset-audit/ground/meadow_grass_albedo-4x4-original.jpg'), ('meadow seam-repaired', 'PRODUCTION_ART', ev / 'asset-audit/ground/meadow_grass_albedo-4x4-tileable.jpg'),
         ('soil original', 'SOURCE_ART', ev / 'asset-audit/ground/tilled_soil_albedo-4x4-original.jpg'), ('soil seam-repaired', 'PRODUCTION_ART', ev / 'asset-audit/ground/tilled_soil_albedo-4x4-tileable.jpg'),
         ('MeadowA final, no grid', 'PRODUCTION_ART', ev / 'asset-audit/ground/MeadowA-4x4-final-no-grid.jpg'), ('MeadowB final, no grid', 'PRODUCTION_ART', ev / 'asset-audit/ground/MeadowB-4x4-final-no-grid.jpg'),
         ('TilledSoil final, no grid', 'PRODUCTION_ART', ev / 'asset-audit/ground/TilledSoil-4x4-final-no-grid.jpg')])
section('Seam crossings at full resolution (where four tiles meet), before and after',
        [('meadow original', 'SOURCE_ART', ev / 'asset-audit/ground/meadow_grass_albedo-seam-crossing-original.png'), ('meadow repaired', 'PRODUCTION_ART', ev / 'asset-audit/ground/meadow_grass_albedo-seam-crossing-tileable.png'),
         ('soil original', 'SOURCE_ART', ev / 'asset-audit/ground/tilled_soil_albedo-seam-crossing-original.png'), ('soil repaired', 'PRODUCTION_ART', ev / 'asset-audit/ground/tilled_soil_albedo-seam-crossing-tileable.png')])
section('The ground in the game at the farthest zoom (17 m), B4 vs B5', [('B4 vs B5', 'REAL_PLAYER_SCREENSHOT', sheets / 'ground-tiling-farthest-zoom.jpg')])
section('The spring: the owner\'s two paintings and the four registered stages the game shows',
        [('spring_S0_dormant (owner, unmodified)', 'SOURCE_ART', src / 'spring_S0_dormant.png'), ('spring_S3_restored (owner, unmodified)', 'SOURCE_ART', src / 'spring_S3_restored.png'),
         ('stages 0-3 as used', 'PRODUCTION_ART', ev / 'asset-audit/spring/stages-0-1-2-3.png'), ('masks: water green, lanterns red, falls blue', 'PRODUCTION_ART', ev / 'asset-audit/spring/masks-water-green-lanterns-red-falls-blue.png'),
         ('S0 over S3 after registration, 50 %', 'PRODUCTION_ART', ev / 'asset-audit/spring-S0-over-S3-50pct.png')])
section('The farm painting and the plants cut from it (lush and withered)',
        [('farm_raised_beds (owner, unmodified)', 'SOURCE_ART', src / 'farm_raised_beds.png'), ('cut boxes', 'PRODUCTION_ART', ev / 'asset-audit/garden/plant-boxes-on-painting.png'),
         ('plants on soil', 'PRODUCTION_ART', ev / 'asset-audit/garden/GardenPlants-on-soil.png')])
section('The tree painting: owner\'s original and the cleaned card (no yellow-red glow)',
        [('broadleaf_tree (owner, unmodified)', 'SOURCE_ART', src / 'broadleaf_tree.png'), ('cleaned, on white', 'PRODUCTION_ART', ev / 'asset-audit/tree/BroadleafTree-on-white.png')])
for spot in ('plaza', 'workshop-front', 'garden'):
    section('Camera pitch candidates at the %s (default 48)' % spot, [('pitch %d' % p, 'REAL_PLAYER_SCREENSHOT', t / ('pitch-%d-%s.png' % (p, spot))) for p in (42, 48, 54)])
section('Caretaker walking at the closest zoom (S, W, E, N), unchanged B4 drawings', [('walk ' + d, 'REAL_PLAYER_SCREENSHOT', t / ('hero-walk-%s-closest-zoom.png' % d)) for d in 'SWEN'])
section('UI with the minimap: dialogue, satchel, pause (1080p)', [('dialogue', 'REAL_PLAYER_SCREENSHOT', t / 'dialogue-mira.png'), ('satchel', 'REAL_PLAYER_SCREENSHOT', t / 'inventory-portrait.png'),
                                                                   ('pause', 'REAL_PLAYER_SCREENSHOT', t / 'pause-menu.png')])
section('Minimap over the recorded route (crops of real frames)', [('minimap strip', 'REAL_PLAYER_SCREENSHOT', ev / 'minimap-strip.png')])
page = '''<!doctype html><html lang="en"><meta charset="utf-8"><title>Echoes B5 review</title>
<style>body{font:15px/1.5 system-ui,sans-serif;background:#18201e;color:#ece6d6;margin:0;padding:0 16px 40px}h1{font-size:22px}h2{font-size:16px;margin:26px 0 8px}
.row{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:12px}figure{margin:0}img{width:100%%;height:auto;display:block;border:1px solid #3c4b49}
figcaption{font-size:13px;color:#cfc8b2;padding:4px 0}.note{max-width:960px;color:#d8d0bb}b{color:#f2d48a}</style>
<h1>Echoes of the Valley &middot; Phase 5.2-B5 (painted environment pilot) &middot; comparisons</h1>
<p class="note">Labels: <b>SOURCE_ART</b> = the owner's six candidate paintings, unmodified, never game footage; <b>PRODUCTION_ART</b> = images the game draws, made from
them by script; <b>EDITOR_PREVIEW</b> = Unity Editor renders used for decisions, not player evidence; <b>REAL_PLAYER_SCREENSHOT</b> = framebuffer captures of the Windows
player (or labelled side-by-side sheets of them) driven by <b>automated</b> input (no human played). "B4" is the B4 presentation switched on in the same B5 build at the same
place. Captions are outside the pixels. This is the author's evidence, not an approval: <b>GATE_B5_OWNER_APPROVAL=PENDING</b>.</p>
%s</html>''' % '\n'.join(sections)
cmp.mkdir(parents=True, exist_ok=True)
(cmp / 'index.html').write_text(page, encoding='utf-8')

# ---- tests and logs ----------------------------------------------------------------------------------------------------------------------
put(tests / 'tests-EditMode.xml', 'tests/EditMode.xml')
put(tests / 'tests-PlayMode.xml', 'tests/PlayMode.xml')
put(tests / 'source-inputs-SHA256.json', 'tests/source-inputs-SHA256.json')
for mode in ('EditMode', 'PlayMode'):
    put(tests / ('tests-' + mode + '.log'), 'logs/tests-' + mode + '.log')
put(build_log, 'logs/build.log')
for tag in ('tour1080-video', 'tour1080-unrecorded', 'tour1080-unrecorded-2', 'tour1080-before-unrecorded', 'tour1080-before-unrecorded-2', 'smoke1080', 'smoke720'):
    put(pl / tag / 'player.log', 'logs/player-%s.log' % tag)
put(pl / 'evidence-run-summary.json', 'logs/evidence-run-summary.json')

# ---- source: changed files and git summary -----------------------------------------------------------------------------------------------
head = git('rev-parse', 'HEAD')
source_commit = json.loads((root / 'tools/art/phase52b5/judgments.json').read_text(encoding='utf-8')).get('sourceCommit', head)
changed = git('diff', '--name-status', base, 'HEAD')
(stage / 'source').mkdir(exist_ok=True)
(stage / 'source/changed-files.txt').write_text('base ' + base + '\nhead ' + head + '\n' + changed + '\n', encoding='utf-8')
(stage / 'source/build-source-commit.txt').write_text(source_commit + '\n', encoding='utf-8')
(stage / 'source/git-change-summary.txt').write_text(git('log', '--oneline', base + '..HEAD') + '\n\n' + git('diff', '--stat', base, 'HEAD') + '\n', encoding='utf-8')
for line in changed.splitlines():
    status, _, path = line.partition('\t')
    p = root / path
    if status.startswith('D') or not p.is_file():
        continue
    if p.suffix in ('.cs', '.py', '.ps1', '.shader', '.md', '.json') and p.stat().st_size < 400_000 and '/Scenes/' not in path and '/Source/' not in path:
        put(p, 'source/files/' + path)

# ---- provenance --------------------------------------------------------------------------------------------------------------------------
for f in ('asset-manifest.md', 'AI-disclosure.md', 'license-notes.md'):
    put(root / 'tools/art/phase52b5/provenance' / f, 'provenance/' + f)
for f in ('README_FIRST_zh.md', 'ART_QA_AND_LIMITATIONS.md', 'CLAUDE_CODE_B5_ART_INTEGRATION_PROMPT.md', 'SHA256SUMS.txt'):
    put(root / 'tools/art/phase52b5/source-docs' / f, 'provenance/owner-pack/' + f)
put(root / 'docs/11-PHASE52-B5.md', 'provenance/11-PHASE52-B5.md')
put(root / 'docs/03-RULES-CREDITS.md', 'provenance/03-RULES-CREDITS.md')

# ---- build manifest, labels, hashes, zip -------------------------------------------------------------------------------------------------
lines = ['SHA-256  size  file (build %s)' % build_dir.name]
for p in sorted(build_dir.rglob('*')):
    if p.is_file():
        lines.append('%s  %d  %s' % (hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_size, p.relative_to(build_dir).as_posix()))
(stage / 'logs').mkdir(exist_ok=True)
(stage / 'logs/build-manifest-SHA256.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
(stage / 'LABELS.md').write_text('''# Labels used in this package

| Label / folder | Meaning |
| --- | --- |
| `SOURCE_ART-owner-pack-not-game-footage/` | The owner's six B5 candidate paintings, byte-identical to the pack. Never game footage. |
| `PRODUCTION_ART/` | What the game draws: the spring stage strip, the tree card, the garden plant atlas, the ground textures, the B5 minimap base; made from the owner's paintings by the scripts in `source/files/tools/art/phase52b5`. |
| `asset-audit/` | Audit sheets: the paintings on white/black/grass/earth/checkerboard with their edge fringe, the spring registration, the ground textures repeated 4 x 4 before and after the seam repair. |
| `EDITOR_PREVIEW-not-player-evidence/` | Unity Editor renders used while building. Not Windows-player evidence. |
| `real-player-screenshots/` | REAL_PLAYER_SCREENSHOT: unmodified PNG captures of the Windows player, and `sheets/` = side-by-side JPEG sheets of such captures with labels outside the pictures. AUTOMATED input. |
| `motion/actual-player-b5-motion-AUTOMATED_INPUT.mp4` | REAL_PLAYER_VIDEO, AUTOMATED_INPUT: the Windows player's own framebuffer frames at their real intervals, AUTOMATED input, no audio. |
''', encoding='utf-8')
if omitted:
    header = 'SHA-256  size  path (left out to keep the package reviewable; the files stay in the local artifacts/ folder)'
    (stage / 'OMITTED-FILES-SHA256.txt').write_text(header + chr(10) + chr(10).join(omitted) + chr(10), encoding='utf-8')
manifest = []
for p in sorted(stage.rglob('*')):
    if p.is_file():
        manifest.append('%s  %d  %s' % (hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_size, p.relative_to(stage).as_posix()))
(stage / 'MANIFEST-SHA256.txt').write_text('SHA-256  size  member\n# package for commit %s\n' % head + '\n'.join(manifest) + '\n', encoding='utf-8')
out_dir.mkdir(parents=True, exist_ok=True)
zip_path = out_dir / (name + '.zip')
if zip_path.exists():
    zip_path = out_dir / (name + '-' + datetime.now().strftime('%Y%m%d-%H%M%S') + '.zip')
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file():
            z.write(p, p.relative_to(stage).as_posix())
digest = hashlib.sha256(zip_path.read_bytes()).hexdigest().upper()
(out_dir / (zip_path.name + '.sha256.txt')).write_text(digest + '  ' + zip_path.name + '\n', encoding='utf-8')
with zipfile.ZipFile(zip_path) as z:
    assert z.testzip() is None
    for entry in manifest:
        expected, size, member = entry.split('  ', 2)
        data = z.read(member)
        assert len(data) == int(size) and hashlib.sha256(data).hexdigest() == expected, member
    for link in re.findall(r'(?:src|href)="([^"]+)"', z.read('comparison/index.html').decode('utf-8')):
        if '://' not in link and not link.startswith('#'):
            member = posixpath.normpath('comparison/' + unquote(link.split('#')[0]))
            assert member in z.namelist(), 'Broken comparison link: ' + member
    print('ZIP', zip_path, 'members', len(z.namelist()), 'bytes', zip_path.stat().st_size)
print('REVIEW_ZIP_SHA256=' + digest)
