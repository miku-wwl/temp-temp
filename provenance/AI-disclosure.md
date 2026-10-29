# AI disclosure for Phase 5.2-B5 (thatgamejam #02 requires disclosure of AI-generated material)

**AI-generated art in the B5 pilot.** The spring (two paintings), the broadleaf tree, the raised-bed garden painting and the meadow
grass and tilled soil textures are **AI-assisted candidate illustrations supplied by the game's owner** in
`Echoes-B5-Environment-Art-Pack.zip` (the pack's README: generated with an AI image tool from the owner's descriptions; the generator is
not named, so the owner should state it in the release disclosure). Scripts cleaned their alpha, registered the two spring paintings,
derived the drained stage-0 spring and the stage-1/2 mixes from them, cut plants and a stone out of the garden painting and recoloured a
withered set, repaired the ground textures' seams and graded them; the only strokes drawn by code are the stage-0 spring's thin murky trickle and small puddle; no human painted over them and no new image was generated in this
session. Everything from B4 (the caretaker drawings, the workshop, the willow, the ground plants, the minimap frame) and B3 (villagers,
dialogue busts) is unchanged.

**AI-assisted code and documentation.** Anthropic Claude Code (this session reported the model Claude Opus 5.5) wrote, under the
developer's direction: `tools/art/phase52b5/*` (audit, ground, spring, tree and garden pipelines, evidence scripts and sheets), the
`Echoes/StageProp25D`, `Echoes/PaintedCard25D` and `Echoes/PaintedWater` shaders, `StagePainting25D`, `Phase52B5Pilot`, the
`ValleyLighting.SetMutedBase` hook, the B5 builder, survey and tour, the B5 tests, `docs/11` and the review package.

**Not AI-generated / unchanged.** Gameplay systems, the save format, quests, the 36 synthesised WAV files and the Unity built-in font are
unchanged by B5. No third-party art, franchise material (including Atelier Ryza), marketplace pack, stock image or font was used.

**In-game credits.** The B5 pilot menu (version `0.5.2-pilot.5`) states that these illustrations are original AI-generated art supplied
by the owner and that the pipelines, shaders, camera, minimap and scene builder were made with Anthropic Claude Code.

**Owner action before any release:** name the image generator, confirm this wording and approve the illustrations as release art.

**Finalization handoff (2026-10-29).** OpenAI Codex audited the preserved source and evidence, added exact pre-test input hashing,
corrected unsupported report claims, completed the review packaging and verified the Git delivery. It generated no new artwork and
changed no Unity game input during finalization. All controls and captures remain automated; owner visual/gameplay approval is pending.
