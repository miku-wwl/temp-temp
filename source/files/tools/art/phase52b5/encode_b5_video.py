"""Encodes the B5 tour's real player framebuffer frames at their captured intervals into actual-player-b5-motion.mp4.

python -I tools/art/phase52b5/encode_b5_video.py <tour-folder>
No synthetic imagery, interpolation, concept compositing or desktop capture. H.264 CRF 26 (1080p frames; the review package stays downloadable). Asserts 30-150 s and a full decode; writes frame-manifest.json
(hash of every frame) and capture-provenance.json.
"""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

FFMPEG = Path(r'C:\Users\weila\AppData\Local\Wand\app-12.65.1\resources\app.asar.unpacked\static\unpacked\capture\release\bin\64bit\ffmpeg.exe')
FFPROBE = FFMPEG.with_name('ffprobe.exe')

folder = Path(sys.argv[1]).resolve()
record = json.loads((folder / 'tour.json').read_text(encoding='utf-8-sig'))
frames = record['capturedFrames']
assert len(frames) >= 900, 'too few continuous framebuffer frames: %d' % len(frames)
assert not record['teleportsDuringRoute']
lines, manifest = ['ffconcat version 1.0'], []
for i, frame in enumerate(frames):
    path = folder / 'frames' / frame['file']
    assert path.is_file(), path
    interval = frames[i + 1]['t'] - frame['t'] if i + 1 < len(frames) else max(0.01, record['routeSeconds'] - frame['t'])
    assert interval > 0
    lines += ["file '%s'" % frame['file'], 'duration %.6f' % interval]
    manifest.append(dict(file=frame['file'], t=frame['t'], sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
lines.append("file '%s'" % frames[-1]['file'])
concat = folder / 'frames' / 'actual-timestamps.ffconcat'
concat.write_bytes(('\n'.join(lines) + '\n').encode('utf-8'))
movie = folder / 'actual-player-b5-motion.mp4'
assert not movie.exists()
with (folder / 'ffmpeg.log').open('w') as log:
    subprocess.run([str(FFMPEG), '-hide_banner', '-loglevel', 'warning', '-f', 'concat', '-safe', '0', '-i', str(concat), '-fps_mode', 'vfr', '-an', '-c:v', 'libx264',
                    '-preset', 'medium', '-crf', '26', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True, stdout=log, stderr=log)
probe = json.loads(subprocess.check_output([str(FFPROBE), '-v', 'error', '-show_entries', 'format=duration:stream=width,height,avg_frame_rate,nb_frames', '-of', 'json', str(movie)]))
duration = float(probe['format']['duration'])
assert 30 <= duration <= 150, 'video duration %.1f s outside 30-150 s' % duration
stream = probe['streams'][0]
assert stream['width'] == record['width'] and stream['height'] == record['height']
subprocess.run([str(FFMPEG), '-v', 'error', '-i', str(movie), '-f', 'null', '-'], check=True)
(folder / 'frame-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
info = dict(method=record['videoCapture'], automatedInput=True, conceptCompositing=False, desktopCaptured=False, audioRecorded=False, framesCaptured=len(frames),
            capturedSeconds=frames[-1]['t'] - frames[0]['t'], videoDuration=duration, probe=probe, fullDecode='PASS', movieSha256=hashlib.sha256(movie.read_bytes()).hexdigest(),
            presentation=record['presentation'], camera=record['camera'])
(folder / 'capture-provenance.json').write_text(json.dumps(info, indent=2), encoding='utf-8')
print(json.dumps(info))
