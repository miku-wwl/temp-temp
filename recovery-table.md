# B5 finalization recovery (2026-10-29 NZDT)

| Component | Current status | Last verified evidence | Remaining work at takeover |
| --- | --- | --- | --- |
| Existing changes | Preserved: 12 files, external backup with SHA-256 | D:/workshop/oct/echoes-of-the-valley-finalization-backup-20261029-2154/backup-manifest.json | Review and commit scoped changes |
| Unity game source | e4e4e0fa2e6dd9cece55af9f3cf2d1206cad8e75, main | Clean Unity inputs at takeover; B5 core 94d8c1a plus canopy/plaza fix e4e4e0f | No redesign or asset recreation |
| Original tests | 64 baseline files unchanged, 18 new EditMode + 6 new PlayMode cases | Baseline f712fd7 compared with current source | Fresh exact-input run because old disposable copy had 9 regenerated differences |
| Windows Player | Final existing build 8dcf20bf30374cc1b676ad6c2df2fd78, 0 errors / 0 build warnings | logs/build-final-e4e4e0f/Build.log, artifacts/build/phase52-b5-final | Reuse with source/path verification, preserve previous builds |
| Gameplay smoke | 1080p PASS, 40 screenshots, exit 0, no error/exception lines | player-final/smoke1080 | Correct old report: separate 720p full run NOT RUN |
| Movement/screenshots | Existing 83.32 s automated-input video and 85 route stills | player-final/tour1080-video and tour1080-unrecorded | Verify hashes, decode and package; no recapture needed |
| B4 comparison | Same build before mode, route exit 1 at inherited spring-bank collider | player-final/tour1080-before-unrecorded | Report partial comparison; no parity claim |
| Visual gate | FAIL stop rule 3: workshop body visibility 0%, silhouette only; owner approval PENDING | sheets/worst-occlusion-b5.jpg and tour occlusion data | Deliver known limitation, no new visual development |
| Packaging | Unfinished scripts and report draft preserved | tools/art/phase52b5, existing b5 report drafts backed up | Fix factual claims, complete ZIP/hash/link verification and independently extractable parts |

Final observed test results and source/build identities are in finalization-audit.json and delivery-identifiers.json. All historical evidence stays on disk. No branch, history rewrite, force push, new artwork, B6 work or itch.io submission is part of this finalization.
