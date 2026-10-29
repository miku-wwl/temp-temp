"""Splits the B5 review package into three ordinary zips, each under a download limit (Google Drive's 256 MiB per file).

python -I tools/art/phase52b5/split_review_zip.py <review.zip> <output-folder> [limit-MiB]
The B4 splitter (tools/art/phase52b4/split_review_zip.py) with the B5 package's folders. Members are copied byte for byte from the
original zip and grouped by folder, so each part opens on its own and the three parts extracted into one folder give back exactly the
original files (MANIFEST-SHA256.txt still applies; comparison/index.html links across parts). Every part carries PART-<n>-OF-3.txt
listing its members with their SHA-256. Never overwrites existing parts.
"""
import hashlib
import sys
import zipfile
from pathlib import Path

src = Path(sys.argv[1]).resolve()
out = Path(sys.argv[2]).resolve()
limit = int(float(sys.argv[3]) * 1024 * 1024) if len(sys.argv) > 3 else 256 * 1024 * 1024
stem = src.stem
ART = ('real-player-screenshots/', 'PRODUCTION_ART/', 'asset-audit/', 'EDITOR_PREVIEW-not-player-evidence/', 'SOURCE_ART-owner-pack-not-game-footage/')
GROUPS = [
    ('documents, report, comparison page, tests, logs, provenance, source, performance data and the recorded video', lambda n: not n.startswith(ART)),
    ('real-player stills of the evidence route (unmodified PNG, 1080p)', lambda n: n.startswith('real-player-screenshots/tour1080')),
    ('labelled side-by-side sheets, whole-game smoke screenshots, the owner\'s source paintings, production art, asset audit and Editor previews',
     lambda n: n.startswith(ART) and not n.startswith('real-player-screenshots/tour1080')),
]
out.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(src) as z:
    infos = z.infolist()
    assigned = {}
    for i in infos:
        hits = [k for k, (_, rule) in enumerate(GROUPS) if rule(i.filename)]
        assert len(hits) == 1, (i.filename, hits)
        assigned.setdefault(hits[0], []).append(i)
    assert sum(len(v) for v in assigned.values()) == len(infos)
    lines = ['%s  original package, %d members' % (hashlib.sha256(src.read_bytes()).hexdigest().upper(), len(infos))]
    for k, (what, _) in enumerate(GROUPS):
        name = '%s-part%dof3.zip' % (stem, k + 1)
        path = out / name
        assert not path.exists(), 'keep earlier parts: ' + str(path)
        members = assigned.get(k, [])
        index = ['%s part %d of 3: %s.' % (stem, k + 1, what),
                 'Extract all three parts into ONE folder: together they are the original package (MANIFEST-SHA256.txt in part 1 lists every',
                 'file; comparison/index.html in part 1 links images in parts 2 and 3).', '', 'SHA-256  size  member']
        with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as w:
            for i in members:
                data = z.read(i.filename)
                copy = zipfile.ZipInfo(i.filename, date_time=i.date_time)   # a fresh entry: the writer must not touch the source's records
                copy.external_attr = i.external_attr
                copy.compress_type = zipfile.ZIP_DEFLATED
                w.writestr(copy, data)
                index.append('%s  %d  %s' % (hashlib.sha256(data).hexdigest(), len(data), i.filename))
            w.writestr('PART-%d-OF-3.txt' % (k + 1), '\n'.join(index) + '\n')
        size = path.stat().st_size
        assert size < limit * 0.97, '%s is %d bytes, over the limit' % (name, size)
        with zipfile.ZipFile(path) as check:
            assert check.testzip() is None
            for i in members:
                assert hashlib.sha256(check.read(i.filename)).digest() == hashlib.sha256(z.read(i.filename)).digest()
        digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        lines.append('%s  %s  (%d bytes, %.1f MiB, %d members + PART-%d-OF-3.txt)' % (digest, name, size, size / 1048576, len(members), k + 1))
        print(name, size, '%.1f MiB' % (size / 1048576), len(members), 'members', digest)
(out / (stem + '-parts.sha256.txt')).write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
