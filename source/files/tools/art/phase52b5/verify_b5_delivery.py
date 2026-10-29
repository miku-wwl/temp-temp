"""Audit preserved B5 evidence against the final inputs; does not run or replace Unity tests.

python -I tools/art/phase52b5/verify_b5_delivery.py tools/art/phase52b5/judgments.json
Writes finalization-audit.json. Existing logs, captures and builds are read only.
"""
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
J = json.loads((ROOT / sys.argv[1]).read_text(encoding='utf-8'))
EV = ROOT / J['evidenceRoot']
PLAYER = EV / J['playerFolder']
TESTS = ROOT / J['testFolder']


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8').strip()


audit = dict(checkedAtUtc=datetime.now(timezone.utc).isoformat(), sourceCommit=J['sourceCommit'])
assert not git('diff', J['sourceCommit'], '--', 'game'), 'Unity inputs differ from the built source commit'
inputs = read(TESTS / 'source-inputs-SHA256.json')
assert inputs['sourceCommit'] == J['sourceCommit']
for item in inputs['files']:
    path = ROOT / 'game' / item['path']
    assert path.stat().st_size == item['bytes'] and sha(path).upper() == item['sha256'], item['path']
audit['testInputCorrespondence'] = dict(result='PASS', files=len(inputs['files']), manifest=str((TESTS / 'source-inputs-SHA256.json').relative_to(ROOT)))

baseline_tests = git('ls-tree', '-r', '--name-only', J['baselineCommit'], 'game/Assets/Echoes/Tests').splitlines()
for path in baseline_tests:
    previous = subprocess.check_output(['git', 'show', J['baselineCommit'] + ':' + path], cwd=ROOT)
    assert (ROOT / path).read_bytes().replace(b'\r\n', b'\n') == previous.replace(b'\r\n', b'\n'), path
audit['unchangedBaselineTestFiles'] = len(baseline_tests)
audit['tests'] = {}
for mode, expected in (('EditMode', 262), ('PlayMode', 106)):
    path = TESTS / ('tests-' + mode + '.xml')
    xml = ET.parse(path).getroot()
    result = {k: xml.get(k) for k in ('result', 'total', 'passed', 'failed', 'skipped', 'inconclusive', 'start-time', 'end-time')}
    assert result['result'] == 'Passed' and int(result['passed']) == expected
    assert int(result['failed']) == int(result['skipped']) == int(result['inconclusive']) == 0
    assert int(result['total']) == expected
    result['sha256'] = sha(path)
    result['b5Cases'] = [c.get('fullname') for c in xml.iter('test-case') if 'Phase52B5' in (c.get('classname') or '')]
    audit['tests'][mode] = result

log = (ROOT / J['buildLog']).read_text(encoding='utf-8', errors='replace')
build = re.search(r'PILOT_BUILD (\w+) id=(\w+) errors=(\d+) warnings=(\d+) bytes=(\d+)', log)
assert build and build.group(1) == 'Succeeded' and build.group(3) == '0'
build_dir = ROOT / J['buildDir']
assert str(build_dir) in log and str(ROOT / 'game') in log
build_files = []
for path in sorted(build_dir.rglob('*')):
    if path.is_file():
        build_files.append(dict(path=path.relative_to(build_dir).as_posix(), sha256=sha(path), bytes=path.stat().st_size))
audit['build'] = dict(result=build.group(1), id=build.group(2), errors=int(build.group(3)), warnings=int(build.group(4)), sourceCommit=J['sourceCommit'], files=build_files,
                      correspondence='Original-project build log after source commit; Unity inputs still match that commit. Build and player paths match. No pre-build input hash was recorded; the new test input manifest now preserves the same source.')

# Resolve serialized B5 references through project and installed package metadata.
known = set()
for directory in (ROOT / 'game/Assets', ROOT / 'game/Library/PackageCache'):
    for path in directory.rglob('*.meta'):
        match = re.search(rb'^guid: ([0-9a-f]{32})', path.read_bytes(), re.M)
        if match:
            known.add(match.group(1).decode())
unresolved = {}
for path in (ROOT / 'game/Assets/Echoes/Art/Phase52B5').rglob('*'):
    if path.suffix not in ('.unity', '.prefab', '.mat', '.asset'):
        continue
    data = path.read_bytes()
    if b'\0' in data[:256]:
        continue
    for guid in re.findall(rb'guid: ([0-9a-f]{32})', data):
        guid = guid.decode()
        if guid not in known and not guid.startswith('0000000000000000'):
            unresolved.setdefault(guid, []).append(path.relative_to(ROOT).as_posix())
assert not unresolved, unresolved
audit['b5GuidReferences'] = dict(result='PASS', unresolved=unresolved)

smoke = read(PLAYER / 'smoke1080/process-result.json')
assert smoke['naturalExit'] and smoke['exitCode'] == 0 and smoke['exceptionLines'] == 0 and smoke['passLines']
audit['smoke1080'] = smoke
audit['smoke720'] = 'NOT RUN as a separate whole-game run; the 1080p smoke includes a brief 720p performance sample only'
audit['routes'] = {}
for name in ('tour1080-video', 'tour1080-unrecorded', 'tour1080-before-unrecorded'):
    folder = PLAYER / name
    result, stats, tour = read(folder / 'process-result.json'), read(folder / 'tour-stats.json'), read(folder / 'tour.json')
    assert str(build_dir).replace('\\', '/') in (folder / 'player.log').read_text(encoding='utf-8').replace('\\', '/')
    if 'before' not in name:
        assert result['naturalExit'] and result['exitCode'] == 0 and stats['routePass']
    audit['routes'][name] = dict(process=result, statistics=stats)
    if name == 'tour1080-unrecorded':
        assert len(tour['stills']) == 85
        for still in tour['stills']:
            with Image.open(folder / (still + '.png')) as image:
                assert image.size == (1920, 1080)
        audit['screenshots'] = dict(result='PASS', count=85, resolution='1920x1080', automatedInput=True, stageStills='S1-S3 set by evidence script, explicitly labelled')
        audit['occlusion'] = tour['occlusion']
audit['performanceComparison'] = 'PARTIAL: same build, hardware, cap and attempted route, but B4 stream segment collided and its route exited 1. Raw B4/B5 figures retained; no full-route parity claimed.'

folder = PLAYER / 'tour1080-video'
provenance = read(folder / 'capture-provenance.json')
movie = folder / 'actual-player-b5-motion.mp4'
assert sha(movie) == provenance['movieSha256']
frames = read(folder / 'frame-manifest.json')
for frame in frames:
    assert sha(folder / 'frames' / frame['file']) == frame['sha256'], frame['file']
assert len(frames) == provenance['framesCaptured'] and provenance['automatedInput'] and not provenance['audioRecorded']
ffmpeg = Path(r'C:\Users\weila\AppData\Local\Wand\app-12.65.1\resources\app.asar.unpacked\static\unpacked\capture\release\bin\64bit\ffmpeg.exe')
probe = json.loads(subprocess.check_output([str(ffmpeg.with_name('ffprobe.exe')), '-v', 'error', '-show_entries', 'format=duration:stream=codec_type,width,height,avg_frame_rate,r_frame_rate,nb_frames', '-of', 'json', str(movie)]))
subprocess.run([str(ffmpeg), '-v', 'error', '-i', str(movie), '-f', 'null', '-'], check=True)
audit['video'] = dict(result='PASS_DECODE', sha256=sha(movie), capturedFrames=len(frames), probe=probe, actualPlayback='NOT VERIFIED: user stopped Computer Use with physical Escape before the B5 file was opened', audio='NO AUDIO STREAM', humanPlaytest=False)
audit['engineering'] = 'PASS (automated Unity suites, build, 1080p full-game smoke and B5 movement route)'
audit['visualGate'] = 'FAIL stop rule 3: caretaker body 0% visible behind workshop, silhouette only; inherited from B4. Owner review PENDING.'
audit['ownerPlaytest'] = 'PENDING'
audit['overall'] = 'PARTIAL'
(EV / 'finalization-audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
print(json.dumps({k: audit[k] for k in ('testInputCorrespondence', 'unchangedBaselineTestFiles', 'b5GuidReferences', 'engineering', 'visualGate', 'overall')}, indent=2))
