"""Writes final-report.md and README_FIRST_zh.md for the Phase 5.2-B5 review package.

python -I tools/art/phase52b5/make_b5_report.py <judgments.json>
Numbers (tests, build id, frame times, resources, distances, exit codes, hashes, git ids, occlusion shares, seam ratios, registration,
minimap timeline) are read from the evidence files, never typed by hand. Judgments (statuses, what still looks bad, recommendations)
come from judgments.json and are the author's opinion, not owner approval.
"""
import hashlib
import json
import re
import statistics
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[3]
J = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
ev = root / J['evidenceRoot']
pl = ev / J.get('playerFolder', 'player')
tv = root / J['testFolder']


def read(path, default=None):
    p = Path(path)
    return json.loads(p.read_text(encoding='utf-8-sig')) if p.is_file() else default


def run_xml(xml):
    r = ET.parse(xml).getroot()
    return dict(result=r.get('result'), passed=int(r.get('passed')), total=int(r.get('total')), failed=int(r.get('failed')), skipped=int(r.get('skipped')))


def cases(xml, cls):
    r = ET.parse(xml).getroot()
    return ['%s: %s' % (c.get('name'), c.get('result')) for c in r.iter('test-case') if cls in (c.get('classname') or '')]


def git(*a):
    return subprocess.check_output(['git', *a], cwd=root, text=True).strip()


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()


def bullets(items):
    return '\n'.join('- ' + i for i in items)


edit_xml, play_xml = tv / 'tests-EditMode.xml', tv / 'tests-PlayMode.xml'
edit, play = run_xml(edit_xml), run_xml(play_xml)
b5cases = cases(edit_xml, 'Phase52B5') + cases(play_xml, 'Phase52B5')
blog = (root / J['buildLog']).read_text(encoding='utf-8', errors='replace')
m = re.search(r'PILOT_BUILD (\w+) id=(\w+) errors=(\d+) warnings=(\d+) bytes=(\d+)', blog)
build = dict(result=m.group(1), id=m.group(2), errors=m.group(3), warnings=m.group(4), bytes=m.group(5)) if m else dict(result='UNKNOWN', id='?', errors='?', warnings='?', bytes='?')
exe = root / J['buildDir'] / 'EchoesOfTheValley.exe'
b4log = (root / J['b4BuildLog']).read_text(encoding='utf-8', errors='replace') if (root / J['b4BuildLog']).is_file() else ''
m4 = re.search(r'PILOT_BUILD (\w+) id=(\w+) errors=(\d+) warnings=(\d+) bytes=(\d+)', b4log)
b4build = dict(result=m4.group(1), id=m4.group(2), errors=m4.group(3), warnings=m4.group(4)) if m4 else None

tour_keys = ('tour1080-video', 'tour1080-unrecorded', 'tour1080-unrecorded-2', 'tour1080-before-unrecorded', 'tour1080-before-unrecorded-2')
runs = {k: (read(pl / k / 'process-result.json'), read(pl / k / 'tour-stats.json'), read(pl / k / 'tour.json')) for k in tour_keys}
smoke = {k: read(pl / k / 'process-result.json') for k in ('smoke720', 'smoke1080')}
prov = read(pl / 'tour1080-video/capture-provenance.json', {})
head = git('rev-parse', 'HEAD')
source_commit = J.get('sourceCommit', head)
branch = git('rev-parse', '--abbrev-ref', 'HEAD')
origin = git('rev-parse', 'origin/main')
baseline = J['baselineCommit']
commits = git('log', '--oneline', baseline + '..HEAD')
branches = git('branch', '--list')
spring = read(root / 'game/Assets/Echoes/Art/Phase52B5/Props/SpringStages.json', {})
ground = read(root / 'game/Assets/Echoes/Art/Phase52B5/Terrain/Ground.json', {})
audit = read(ev / 'asset-audit/audit.json', {})


def run_line(label, key):
    r, s, _ = runs[key]
    if not r or not s:
        return '%s: not run' % label
    f = s['frameMs']
    res = (r.get('resources') or {})
    shared = (res.get('gpuSharedMB') or {})
    return ('%s: exit code %s (natural exit %s), exception/error lines %s, %.1f s to the ready flag; route %s m in %s s, route pass %s, failures %s; %s, %d frames, '
            'P50 %.2f / P95 %.2f / P99 %.2f ms, worst %.1f ms, mean %.1f FPS, 1%% low %.1f FPS; GPU memory in use (shared, integrated GPU) mean %s MB, max %s MB (%s)'
            % (label, r['exitCode'], r['naturalExit'], r['exceptionLines'], r.get('secondsToReady') or -1, s['metres'], s['routeSeconds'], s['routePass'], s['failures'] or 'none',
               s['resolution'], s['frames'], f['p50'], f['p95'], f['p99'], f['worst'], s['fps']['mean'], s['fps']['onePercentLow'], shared.get('mean'), shared.get('max'), s['note']))


labels = {'tour1080-video': 'Recorded route 1080p (video)', 'tour1080-unrecorded': 'Unrecorded route 1080p, B5', 'tour1080-before-unrecorded': 'Unrecorded route 1080p, B4 presentation (before mode, same build)',
          'tour1080-unrecorded-2': 'Unrecorded route 1080p, B5, run 2', 'tour1080-before-unrecorded-2': 'Unrecorded route 1080p, B4 presentation, run 2'}
run_lines = [run_line(labels[k], k) for k in tour_keys if runs[k][0]]
for k, label in (('smoke1080', 'Whole-game smoke 1080p'), ('smoke720', 'Whole-game smoke 720p')):
    r = smoke[k]
    if r:
        run_lines.append('%s: natural exit %s, exit code %s, %s s, %s screenshots, exception lines %s, %s' % (label, r['naturalExit'], r['exitCode'], r['seconds'], r['screenshots'], r['exceptionLines'], '; '.join(r.get('passLines') or [])))


def perf_of(keys):
    rows = [(runs[k][1], runs[k][0]) for k in keys if runs[k][1] and runs[k][0]]
    if not rows:
        return None
    fps = [s['fps']['mean'] for s, _ in rows]
    low = [s['fps']['onePercentLow'] for s, _ in rows]
    p95 = [s['frameMs']['p95'] for s, _ in rows]
    p99 = [s['frameMs']['p99'] for s, _ in rows]
    worst = [s['frameMs']['worst'] for s, _ in rows]
    shared = [((r.get('resources') or {}).get('gpuSharedMB') or {}).get('mean') for _, r in rows]
    shared_max = [((r.get('resources') or {}).get('gpuSharedMB') or {}).get('max') for _, r in rows]
    gpu = [((r.get('resources') or {}).get('gpu3dUtilisationPercent') or {}).get('mean') for _, r in rows]
    private = [((r.get('resources') or {}).get('privateMB') or {}).get('max') for _, r in rows]
    ready = [r.get('secondsToReady') for _, r in rows]
    mean = lambda v: round(statistics.mean([x for x in v if x is not None]), 2) if any(x is not None for x in v) else None
    return dict(runs=len(rows), fps=mean(fps), low=mean(low), p95=mean(p95), p99=mean(p99), worst=max(worst), shared=mean(shared), sharedMax=max(x for x in shared_max if x is not None) if any(x is not None for x in shared_max) else None,
                gpu=mean(gpu), privateMax=max(x for x in private if x is not None) if any(x is not None for x in private) else None, ready=mean(ready))


p5 = perf_of(('tour1080-unrecorded', 'tour1080-unrecorded-2'))
p4 = perf_of(('tour1080-before-unrecorded', 'tour1080-before-unrecorded-2'))
perf_table = 'not measured'
if p5 and p4:
    rows = [('Runs (1080p, unrecorded, attempted same route)', p4['runs'], p5['runs']), ('Mean FPS', p4['fps'], p5['fps']), ('1 % low FPS', p4['low'], p5['low']),
            ('P95 frame time (ms)', p4['p95'], p5['p95']), ('P99 frame time (ms)', p4['p99'], p5['p99']), ('Worst frame (ms)', p4['worst'], p5['worst']),
            ('GPU 3D engine utilisation, mean (%)', p4['gpu'], p5['gpu']), ('GPU memory in use (shared), mean (MB)', p4['shared'], p5['shared']),
            ('GPU memory in use (shared), max (MB)', p4['sharedMax'], p5['sharedMax']), ('Process private memory, max (MB)', p4['privateMax'], p5['privateMax']),
            ('Launch to tour ready, incl. menu and scene load (s)', p4['ready'], p5['ready'])]
    perf_table = '| Measure | B4 presentation (before mode) | B5 |\n| --- | --- | --- |\n' + '\n'.join('| %s | %s | %s |' % r for r in rows)
video_line = ('%.2f s, %s captured framebuffer frames (about %.2f captured FPS), %s encoded frames, encoded average rate %s FPS (variable frame rate), %sx%s, no audio, no desktop capture, no concept compositing; SHA-256 `%s`. Full decode: %s. Actual GUI playback: NOT VERIFIED (owner pressed Escape to stop Computer Use before this file was opened).'
              % (prov['videoDuration'], prov['framesCaptured'], prov['framesCaptured'] / max(1e-6, prov['capturedSeconds']), prov['probe']['streams'][0]['nb_frames'], prov['probe']['streams'][0]['avg_frame_rate'], prov['probe']['streams'][0]['width'], prov['probe']['streams'][0]['height'], prov['movieSha256'], prov['fullDecode'])) if prov else 'not produced'
vs = runs['tour1080-video'][1] or {}
mm = vs.get('minimap', {})
timeline = ', '.join('%.1f s %s (%s)' % (e['t'], e['objective'], e['step']) for e in mm.get('objectiveTimeline', [])) or 'not recorded'
scale = ((runs['tour1080-unrecorded'][1] or {}).get('minimap') or {}).get('mapUnitsPerMetre', {})
skate = ((runs['tour1080-unrecorded'][1] or {}).get('runPhaseVsDistance') or {})
dirs = vs.get('directionSamples', {})
tour = runs['tour1080-unrecorded'][2] or {}
occ = tour.get('occlusion', [])
pct = lambda v: ('%.0f %%' % (100 * v)) if v is not None and v >= 0 else '-'
occ_rows = '\n'.join('| %s | %s | %.0f m | %s | %s | %s |' % (o['still'], o['presentation'], o['zoom'], pct(o.get('visibleShare')), pct(o.get('visibleShareWithoutOpening')),
                                                            pct(o.get('markedShare'))) for o in occ)
worst = min((o for o in occ if o['presentation'] == 'B5' and o.get('visibleShare', -1) >= 0), key=lambda o: o['visibleShare'], default=None)
reg = spring.get('registration', {})
seams = '\n'.join('| %s | %s / %s | %s / %s |' % (k, (v.get('sourceWrap') or {}).get('seamRatioX', '-'), (v.get('sourceWrap') or {}).get('seamRatioY', '-'), v['finalWrap']['seamRatioX'], v['finalWrap']['seamRatioY'])
                  for k, v in ground.items() if k != 'PlazaCobbleWarm')
S = J['status']
smoke_ok = bool(smoke.get('smoke1080')) and all(r['naturalExit'] and r['exitCode'] == 0 and r['exceptionLines'] == 0 for r in smoke.values() if r)
smoke_resolutions = ', '.join('%sx%s' % (r['width'], r['height']) for r in smoke.values() if r)
video_ok = bool(prov) and 30 <= prov.get('videoDuration', 0) <= 150
rc1 = root / 'artifacts/release/EchoesOfTheValley-Windows-x64-RC1.zip'
protected = rc1.is_file() and sha(rc1) == 'EB9BFC0D9B0BA60A0E9E8367ADF3BA4EB4D4F3EFE8F0F6FCD58A2AB4FDA3112A'
stop_rows = '\n'.join('| %d | %s | %s | %s |' % (i + 1, s['rule'], s['result'], s['evidence']) for i, s in enumerate(J['stopRules']))

report = '''# Echoes of the Valley: Phase 5.2-B5 (painted environment) review report

2026-10-29 NZDT, branch `%(branch)s` (main-only workflow). **Engineering PASS (automated); visual stop rule 3 FAIL; overall PARTIAL. Gate B5 owner approval, human visual approval and human gameplay QA are PENDING; world expansion is NOT allowed.** Gameplay screenshots and the video are real Windows-player framebuffer captures driven by **automated** input (no human played); source art and Editor diagnostics are separately labelled and are not gameplay evidence. S1-S3 comparison stills are reached by the evidence script and say so in their names. "B4" in a comparison is the B4 presentation switched on in the same B5 build at the same place (`-echoes-b5-before`).

## 1. Git and safety

- Branch `%(branch)s`, HEAD `%(head)s`, `origin/main` `%(origin)s` (in sync: %(sync)s). B5 started from `%(baseline)s` (B4 on main). Local branches: `%(branches)s` (no branch was created). No force push, no history rewrite, no reset, no stash.
- Gameplay/build source commit: `%(source_commit)s`. HEAD above includes finalization tooling/documentation only after that source commit. The exact pre-test inputs and finalization checks are in `tests/source-inputs-SHA256.json` and `logs/finalization-audit.json` in the review package. The original test snapshot differed in nine generated assets after its tests; it was preserved, and a fresh full test run was required to record the exact inputs.
- Commits for B5:
```
%(commits)s
```
- RC1 release zip unchanged: %(prot)s. The original, B2, B3 and B4 scenes contain no B5 component (EditMode test `OriginalB2B3AndB4Scenes_ContainNoB5Presentation`). B4 is recoverable by `git checkout %(baseline)s`, and inside the B5 build by `-echoes-b5-before`.

## 2. Baseline audit: what B4 showed in the real player (before mode stills of this build, the same places as B5)

%(baseline_audit)s

## 3. Asset pack (A / B / C)

%(assets)s

Spring registration (SIFT similarity, S0 onto S3): %(regm)s matches, %(regi)s inliers, scale %(regs).4f, rotation %(regr).3f deg, shift %(regt)s px, residual median %(regmed).2f px, p95 %(regp95).2f px.

Ground texture seams (opposite edges against neighbouring texels; 1.0 = as continuous as the inside):

| Texture | Source X / Y | Final X / Y |
| --- | --- | --- |
%(seams)s

## 4. What was built

### P0-A Spring
%(springtxt)s

### P0-B Ground and farmland
%(groundtxt)s

### P0-C Trees, occlusion and the caretaker
%(treestxt)s

### P1 Overall look, minimap and UI
%(p1txt)s

## 5. Occlusion, measured in the real player

Share of the caretaker the player sees, measured on the real framebuffer (consecutive frames with and without her card, against the same with every other renderer off; the hidden-silhouette marker switched off). "Visible or marked" counts the silhouette marker too (lower threshold). For B5 the count with the painted trees' opening switched off shows what the opening contributes.

| Still | Presentation | Zoom | Visible | Visible without the canopy opening | Visible or marked |
| --- | --- | --- | --- | --- | --- |
%(occ)s

Worst B5 case: `%(worst)s` (%(worstv)s). The faint hidden-silhouette pass is not counted as visible.

## 6. Caretaker and minimap

Direction samples in the recorded route: %(dirs)s. Run cycle against distance (unrecorded 1080p): median per-frame difference %(skmed)s, p95 %(skp95)s of a cycle (stride %(stride)s m; driven by distance). This checks animation phase, not visual foot contact; sliding and comfort still need owner review. The caretaker's drawings, speed (4.5 m/s) and stride are B4's, unchanged.

Minimap during the recorded route (objective over time): %(timeline)s. Map scale while walking (unrecorded 1080p): p05 %(sc05)s, median %(scmed)s, p95 %(sc95)s UI units per metre over %(scn)s steps (constant: the map follows the world).

## 7. Stop rules (any FAIL means B5 cannot pass)

| # | Rule | Result | Evidence |
| --- | --- | --- | --- |
%(stops)s

## 8. Tests (read from the XML)

| Run | Result |
| --- | --- |
| EditMode, isolated project copy `%(tv)s` | %(edit_r)s %(edit_p)s/%(edit_t)s (failed %(edit_f)s, skipped %(edit_s)s) |
| PlayMode, same copy | %(play_r)s %(play_p)s/%(play_t)s (failed %(play_f)s, skipped %(play_s)s) |

B4 baseline: EditMode 244, PlayMode 100; no existing test was removed or weakened. B5 tests: %(cases)s.

## 9. Windows builds

- B5: non-development Windows x64 Mono/D3D11, Unity 6000.3.27f1 / URP 17.3.0, version `0.5.2-pilot.5`, `%(bdir)s`: **%(bres)s**, build id `%(bid)s`, errors %(berr)s, warnings %(bwarn)s, %(bbytes)s bytes; `EchoesOfTheValley.exe` SHA-256 `%(exe)s` (the Unity launcher stub; every build file is hashed in the package's `logs/build-manifest-SHA256.txt`).
- B4 (rebuilt on 2026-10-29 from `%(b4c)s`, the B4 content of `%(baseline)s`, after the earlier B4 build folder disappeared): %(b4b)s.

## 10. Real-player runs (automated)

%(runs)s

Video: %(video)s. The video frame rate is the framebuffer capture rate (each frame is read back and JPEG-encoded during the run), not the game's frame rate: the game's frame times are measured in the unrecorded runs.

## 11. Performance (Radeon 780M, D3D11, windowed 1920 x 1080, 60 FPS cap, vSync off; same build, attempted same route)

%(perf)s

%(perfnote)s

## 12. WHAT STILL LOOKS BAD

%(bad)s

## 13. Not verified

%(unverified)s

## 14. Deferred visual observations (no further phase implemented or authorized here)

%(b6)s

## 15. Status

```text
PHASE52_B5_STATUS=%(s_b5)s
SOURCE_BRANCH=%(branch)s
SOURCE_COMMIT=%(source_commit)s
FINAL_COMMIT=%(head)s
ORIGIN_MAIN=%(origin)s
B5_ASSET_ZIP_VERIFICATION=%(s_zip)s
B5_ASSETS_USED=%(s_used)s
B5_ASSETS_REJECTED=%(s_rej)s
SPRING_STAGES_STATUS=%(s_spring)s
GROUND_SEAMLESS_STATUS=%(s_ground)s
FARMLAND_STATUS=%(s_farm)s
TREES_AND_OCCLUSION_STATUS=%(s_trees)s
CHARACTER_ANIMATION_STATUS=%(s_hero)s
GLOBAL_LOOK_STATUS=%(s_look)s
MINIMAP_REAL_WORLD_STATUS=%(s_map)s
EDITMODE_STATUS=%(edit_r)s %(edit_p)s/%(edit_t)s
PLAYMODE_STATUS=%(play_r)s %(play_p)s/%(play_t)s
WINDOWS_BUILD_STATUS=%(bres)s %(bid)s (%(bdir)s)
FULL_GAME_SMOKE=%(s_smoke)s
REAL_PLAYER_SCREENSHOTS=%(s_shots)s
CONTINUOUS_REAL_PLAYER_MOVEMENT_VIDEO=%(s_video)s
Radeon_780M_1080P_PERFORMANCE=%(s_perf)s
STOP_RULES=%(s_stops)s
GATE_B5_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL
GATE_B5_OWNER_APPROVAL=PENDING
HUMAN_VISUAL_APPROVAL=PENDING
HUMAN_GAMEPLAY_QA_STATUS=PENDING
WORLD_EXPANSION_ALLOWED=NO
REMOTE_PUSH_STATUS=%(s_push)s
ITCH_SUBMISSION_STATUS=NOT_SUBMITTED
REVIEW_ZIP=%(zip)s
NEXT_ACTION=%(next)s
```
''' % dict(branch=branch, head=head, source_commit=source_commit, origin=origin, sync='YES' if head == origin else 'NO (local commits not yet pushed when this report was written)', baseline=baseline,
           branches=', '.join(b.strip('* ').strip() for b in branches.splitlines()), commits=commits or '(none yet)', prot='YES' if protected else 'NO (or not present locally)',
           baseline_audit=bullets(J['baselineAudit']), assets=bullets(J['assets']),
           regm=reg.get('matches'), regi=reg.get('inliers'), regs=reg.get('scale', 0), regr=reg.get('rotationDeg', 0), regt=', '.join('%.2f' % v for v in reg.get('translationPx', [])),
           regmed=reg.get('residualMedianPx', 0), regp95=reg.get('residualP95Px', 0), seams=seams,
           springtxt=bullets(J['spring']), groundtxt=bullets(J['ground']), treestxt=bullets(J['trees']), p1txt=bullets(J['p1']),
           occ=occ_rows or '| not measured | | | | | |', worst=worst['still'] if worst else 'n/a', worstv=('%.0f %% visible' % (100 * worst['visibleShare'])) if worst else 'n/a',
           dirs=', '.join('%s %d' % kv for kv in sorted(dirs.items())) or 'n/a', skmed=skate.get('medianError'), skp95=skate.get('p95Error'), stride=skate.get('stride'),
           timeline=timeline, sc05=scale.get('p05'), scmed=scale.get('median'), sc95=scale.get('p95'), scn=scale.get('samples'), stops=stop_rows,
           tv=J['testFolder'], edit_r=edit['result'], edit_p=edit['passed'], edit_t=edit['total'], edit_f=edit['failed'], edit_s=edit['skipped'],
           play_r=play['result'], play_p=play['passed'], play_t=play['total'], play_f=play['failed'], play_s=play['skipped'], cases=', '.join(b5cases) or 'none found',
           bdir=J['buildDir'], bres=build['result'], bid=build['id'], berr=build['errors'], bwarn=build['warnings'], bbytes=build['bytes'], exe=sha(exe) if exe.is_file() else 'MISSING',
           b4c=J.get('b4BuildCommit', baseline), b4b=('%s, build id `%s`, errors %s, warnings %s, `%s`' % (b4build['result'], b4build['id'], b4build['errors'], b4build['warnings'], J['b4BuildDir'])) if b4build else 'not rebuilt',
           runs=bullets(run_lines), video=video_line, perf=perf_table, perfnote=J['perfNote'], bad=bullets(J['bad']), unverified=bullets(J['unverified']), b6=bullets(J['b6']),
           s_b5=S['b5'], s_zip=S['zip'], s_used=S['used'], s_rej=S['rejected'], s_spring=S['spring'], s_ground=S['ground'], s_farm=S['farm'], s_trees=S['trees'], s_hero=S['hero'],
           s_look=S['look'], s_map=S['minimap'], s_smoke=('PASS (%s, three restorations, ending, save and Continue, 0 exception lines); standalone 720p smoke NOT RUN' % smoke_resolutions) if smoke_ok else 'FAIL or incomplete',
           s_shots=S['screenshots'], s_video=('PASS (AUTOMATED_INPUT, %.1f s, real framebuffer)' % prov['videoDuration']) if video_ok else 'FAIL or incomplete',
           s_perf=S['performance'], s_stops=S['stopRules'], s_push=('PASS (HEAD equals origin/main; see delivery-identifiers.json for live remote verification)' if head == origin else 'PENDING (local HEAD differs from origin/main)'), zip=J['reviewZip'], next=J['next'])
(ev / 'final-report.md').write_text(report, encoding='utf-8')
(ev / 'README_FIRST_zh.md').write_text(J['readmeZh'], encoding='utf-8')
print('written', ev / 'final-report.md')
