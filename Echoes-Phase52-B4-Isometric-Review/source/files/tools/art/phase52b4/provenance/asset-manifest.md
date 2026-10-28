# Phase 5.2-B4 asset manifest and audit (A / B / C)

Source pack: `D:\Download\Echoes-B4-Original-Art-Assets-20261028.zip`, SHA-256 `738B55FDB63BB4EF328A3FAA35F6D4802E0428B9C7577458D1B9E5A14BA8F30B`.
Its `SHA256SUMS.txt` verified 28/28 (22 new PNGs, 3 earlier owner references, README, QA list, prompt) before any use. The 22 new PNGs are
copied **unmodified** to `game/Assets/Echoes/Art/Phase52B4/Source/` (the EditMode test `B4Sources_AreTheUnmodifiedOwnerSuppliedFiles`
re-hashes every one); the pack's documents are in `tools/art/phase52b4/source-docs/`. Nothing in the pack is a 3D model, rig or animation
file; the pack's README describes every image as an original AI-generated illustration (the generator is not named in the pack).

Classification: **A** = used in the B4 game scene after processing; **B** = kept as reference, not used in the game; **C** = rejected.
The automated audit (`tools/art/phase52b4/audit_assets.py`: every image on white, black, grass and earth; materials tiled 2x2) is in
`artifacts/art-work/phase52b4/audit/`.

| File (pack path) | SHA-256 (first 16) | Audit finding | Class | Actual use and every modification |
| --- | --- | --- | --- | --- |
| `characters/idle/hero_idle_S.png` | `f6f93c3ce5d8ba0b` | RGBA; red-brown haze ring (3,664 red-fringe px, haze up to 29 px from the figure) | A | B4 caretaker S idle: haze and stray islands removed, edge colour taken from the nearest solid pixel, scaled to 300 px/m (1.65 m), 12-frame breathing loop by 2D linear-blend skinning (`make_b4_hero.py`) |
| `characters/idle/hero_idle_N.png` | `15f36f363b174e99` | same kind (2,808 px) | A | N idle, same processing |
| `characters/idle/hero_idle_W.png` | `08c38fa0136ab1b8` | same kind (5,267 px) | A | W idle, same processing |
| `characters/idle/hero_idle_E.png` | `5a35257d0c370c27` | same kind (3,552 px) | A | E idle, same processing (E has its own drawing; nothing is mirrored at runtime) |
| `characters/run-keyposes/hero_run_S.png` | `9cea55830de32123` | four poses on one sheet, unequal widths, haze up to 37 px | A | Poses split by closed-mask components, cleaned, per-direction scale corrected after a head-size check; run cycle = drawn poses 1-2-3-2 with 1.5 % squash in-betweens (8 frames, 2.8 m stride) |
| `characters/run-keyposes/hero_run_N.png` | `fbdeed57a9f3a530` | as above | A | N run: poses 1-1m-3-1m, where 1m is pose 1 with the lower legs mirrored (the drawn back view leads with one leg only) |
| `characters/run-keyposes/hero_run_W.png` | `ea38a30633a9c6fd` | as above | A | W run: poses 0-1-2-3 |
| `characters/run-keyposes/hero_run_E.png` | `3142fa75b74b7e48` | as above | A | E run: poses 0-1-2-3 |
| `environment/isometric-static-props/valley_apothecary_cottage_iso.png` | `d4444737340f85aa` | clean painting; 2,133 red-fringe px at the hanging sign | A | Painted workshop: alpha cleanup and crop; a camera-facing card whose every pixel takes the depth of the painting's ground line (`Echoes/StaticProp25D`); the old 3D house is hidden and only casts the shadow; its collision is a prism on the painted footprint |
| `environment/isometric-static-props/willow_tree_iso.png` | `dd97a40c10ac3e8d` | 11 % far haze (up to 75 px) around the fronds | A | Painted willow: haze removed, crop; dithered canopy opening around the player (occlusion); the old trunk collider remains |
| `environment/isometric-static-props/spring_ruins_iso.png` | `29dcf0ad5d9fe7f2` | clean | A | Painted spring for stage S1 and later, and a dormant S0 variant made by recolouring only the water (script); the old spring colliders remain |
| `environment/foliage/meadow_grass_clump.png` | `b4270b5d5bd325ad` | 15 % far haze up to 169 px | A | Ground foliage atlas (haze removed, crop, downscale); 900 painted clumps around the four areas (`Echoes/Foliage25D`) |
| `environment/foliage/valley_wildflowers.png` | `227031b58bcaaf42` | 9.6 % haze, 1,000 red-fringe px | A | Same atlas |
| `environment/foliage/forest_fern.png` | `c6bd51438486a22c` | 5 % haze | A | Same atlas |
| `environment/materials/mossy_stone_tile.png` | `0ac381f44f3d6816` | seams visible when tiled (wrap ratio 1.42 / 1.47) | A | Made seamless by a 40 px wrap blend (`make_b4_props.py`), then used as the plaza cobble terrain layer (RGB only) and as triplanar fieldstone on the plaza's pillars, dais and blocks (`Echoes/TriplanarStone`) |
| `ui/minimap_frame.png` | `63859deb9b9ba61d` | centre transparent; 313 red-fringe px on the rim | A | Minimap frame after fringe cleanup; the map inside it is the real baked valley |
| `art-direction/b4_isometric_style_target.png` | `91b70961f36e04b9` | opaque concept painting | B | Style reference only. **Not game footage**: never shown as a screenshot, not in the build |
| `environment/foliage/broadleaf_branch.png` | `274c5f6eaa9a92a4` | branch cluster, 1.8 % haze | B | Not used: one branch does not make a painted broadleaf tree; the B2 broadleaf trees around the four areas stay (known limitation) |
| `environment/foliage/willow_leaf_fronds.png` | `5f479aec36ae0e1e` | fronds, 3.9 % haze | B | Not used: the painted willow already carries its fronds |
| `environment/materials/plaster_wall_tile.png` | `ab5c740f3ab9bb5e` | wrap ratio 1.44 / 1.18 | B | Not used: the workshop is the painted prop (its 3D walls are hidden) |
| `environment/materials/roof_tiles_tile.png` | `f6529a9134e02396` | visible seams (1.77 / 1.46) | B | Not used, same reason; would need seam repair before Repeat |
| `environment/materials/weathered_timber_tile.png` | `50e2e8d0554b7ecf` | strong X seam (2.25) | B | Not used, same reason; would need seam repair before Repeat |

**C (rejected): none.** No file was rejected for quality; the B files are unused because the painted props already cover what they would
have textured (and the three wall/roof/timber tiles would need seam repair first). The earlier owner references (`01`, `02`, `05`) are B3
sources and unchanged.

Derived files (all reproducible from the sources with the scripts named): `Art/Phase52B4/Sprites/CaretakerB4Atlas.png/.json` (80 frames,
4096 x 4096, source hashes recorded inside), `Art/Phase52B4/Props/{Cottage,Willow,Spring,SpringDormant,Foliage}.png` with `Props.json` and
`Foliage.json`, `Art/Phase52B4/UI/{MinimapFrame,Icon_arrow,Icon_ring,Icon_edge,Icon_dot}.png` (the four icons are simple shapes drawn by
script), `Art/Phase52B4/Terrain/PlazaCobble.png`, and `Art/Phase52B4/UI/MinimapBase.png` (rendered in the Editor from the valley's own
terrain, water, paths and buildings).
