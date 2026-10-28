using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Echoes.EditorTools;
using Object = UnityEngine.Object;

namespace Echoes.Tests
{
    /// <summary>
    /// Phase 5.2-B4 high-angle 2.5D pilot: structure of the generated scene, the B4 caretaker drawings, the painted props, the minimap and
    /// the untouched earlier scenes. Engineering properties only, never an art verdict (the owner decides the visual gate).
    /// </summary>
    public class Phase52B4Tests
    {
        static SpriteSheet25D Hero => AssetDatabase.LoadAssetAtPath<SpriteSheet25D>(Phase52B4Builder.HeroSheetPath);

        static Phase52B4Pilot OpenB4()
        {
            EditorSceneManager.OpenScene(Phase52B4Builder.Valley);
            var b4 = Object.FindFirstObjectByType<Phase52B4Pilot>();
            Assert.IsNotNull(b4, "the B4 scene carries its presentation switch");
            return b4;
        }

        [Test] public void B4Scene_KeepsOnePlayerRoot_WithTheB4DrawingsOnTheUnchangedPresenter()
        {
            var b4 = OpenB4();
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, players.Length, "exactly one authoritative player");
            var presenter = players[0].GetComponentInChildren<CharacterVisual2D5D>(true);
            Assert.IsNotNull(presenter);
            Assert.AreSame(Hero, presenter.Sheet, "the presenter shows the B4 drawings");
            Assert.AreSame(presenter, b4.Hero);
            Assert.IsNotNull(b4.BeforeSheet, "the B3 drawings stay referenced for the before view");
            Assert.AreNotSame(b4.BeforeSheet, b4.B4Sheet);
            Assert.IsEmpty(presenter.GetComponentsInChildren<Collider>(true), "the sprite never takes part in physics");
            Assert.AreEqual(4.5f, players[0].MoveSpeed, 1e-4f, "the controller speed is unchanged");
        }

        [Test] public void HighFixedCamera_UsesTheDefaultPitchWithinTheTestedRange_AndOnlyAGentleEndingSway()
        {
            var b4 = OpenB4();
            var follow = Camera.main.GetComponent<FixedFollowCamera25D>();
            Assert.AreSame(follow, b4.FollowCamera);
            Assert.AreEqual(45f, follow.Yaw, 1e-3f, "the painted props were drawn for a 45 degree view of the workshop's facades");
            Assert.AreEqual(48f, follow.Pitch, 1e-3f, "default pitch 48");
            Assert.That(b4.B4View.pitch, Is.InRange(42f, 54f));
            Assert.That(follow.MinDistance, Is.InRange(9f, 12f));
            Assert.That(follow.MaxDistance, Is.InRange(follow.MinDistance, 18f));
            Assert.That(follow.Distance, Is.InRange(follow.MinDistance, follow.MaxDistance));
            Assert.LessOrEqual(follow.MaxOrbitDegrees, 15f, "the ending drifts by a gentle sway, never round the painted props' backs");
            Assert.IsTrue(new SerializedObject(follow).FindProperty("terrainOnlyCollision").boolValue, "a high camera passing over roofs does not jump in");
            Assert.AreEqual(30f, b4.BeforeView.pitch, 1e-3f, "the before view is the B3 camera");
            Assert.AreEqual(25f, b4.BeforeView.yaw, 1e-3f);
        }

        [Test] public void B4Sheet_HasFourDirections_TwelveIdleAndEightRunFrames_AtOneConsistentHeight()
        {
            var sheet = Hero;
            Assert.IsNotNull(sheet);
            Assert.IsNotNull(sheet.atlas);
            var heights = new System.Collections.Generic.List<float>();
            foreach (string key in new[] { "S", "N", "W", "E" })
            {
                var d = sheet.Get(key);
                Assert.IsNotNull(d, key);
                Assert.AreEqual(8, d.run.Length, key + " run frames");
                Assert.AreEqual(12, d.idle.Length, key + " idle frames");
                foreach (var f in d.run.Concat(d.idle))
                {
                    Assert.That(f.uv.x, Is.InRange(0f, 1f)); Assert.That(f.uv.y, Is.InRange(0f, 1f));
                    Assert.LessOrEqual(f.uv.x + f.uv.z, 1.0001f); Assert.LessOrEqual(f.uv.y + f.uv.w, 1.0001f);
                    Assert.That(d.pivotPx.x, Is.InRange(0f, f.sizePx.x), key + " pivot inside the frame");
                    Assert.That(d.pivotPx.y, Is.InRange(0f, f.sizePx.y * 0.1f), key + " ground pivot at the frame's bottom");
                    float metres = (f.sizePx.y - d.pivotPx.y) / sheet.pixelsPerMeter;
                    Assert.That(metres, Is.InRange(1.55f, 1.95f), key + " frame height above the ground point");
                }
                heights.Add((d.idle[0].sizePx.y - d.pivotPx.y) / sheet.pixelsPerMeter);
            }
            Assert.Less(heights.Max() - heights.Min(), 0.1f, "the four directions stand at the same height");
            var east = sheet.Resolve("E", out bool mirrored, out _);
            Assert.AreEqual("E", east.key);
            Assert.IsFalse(mirrored, "E has its own drawings; nothing flips at runtime");
        }

        [Test] public void B4Atlas_FramesDoNotOverlap_AndTransparentTexelsCarryBledColour()
        {
            var rects = Hero.directions.SelectMany(d => d.run.Concat(d.idle)).Select(f => new Rect(f.uv.x, f.uv.y, f.uv.z, f.uv.w)).ToList();
            Assert.AreEqual(4 * 20, rects.Count);
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    Rect a = rects[i], b = rects[j];
                    bool overlap = a.xMin < b.xMax - 1e-5f && b.xMin < a.xMax - 1e-5f && a.yMin < b.yMax - 1e-5f && b.yMin < a.yMax - 1e-5f;
                    Assert.IsFalse(overlap, "frames " + i + " and " + j + " overlap");
                }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(Phase52B4Builder.Folder + "/Sprites/CaretakerB4Atlas.png")));
            Color32[] p = texture.GetPixels32();
            int w = texture.width, h = texture.height, edge = 0, bad = 0;
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    Color32 c = p[y * w + x];
                    if (c.a > 8) continue;
                    if (p[y * w + x + 1].a < 200 && p[y * w + x - 1].a < 200 && p[(y + 1) * w + x].a < 200 && p[(y - 1) * w + x].a < 200) continue;
                    edge++;
                    if (c.r + c.g + c.b < 6 || (c.r > 250 && c.g > 250 && c.b > 250)) bad++;
                }
            Object.DestroyImmediate(texture);
            Assert.Greater(edge, 1000, "the atlas has sprite edges to check");
            Assert.Less(bad / (float)edge, 0.01f, "transparent texels next to the drawings carry their colour (no dark or white halo)");
        }

        [Test] public void B4RunCycle_StrideMatchesTheControllerSpeed_AtAReadableRate()
        {
            float stride = Hero.strideMeters;
            Assert.That(stride, Is.InRange(2.5f, 3.2f), "a jog stride for a 1.65 m character");
            float framesPerSecond = 4.5f / stride * 8f;
            Assert.That(framesPerSecond, Is.InRange(10f, 18f), "eight drawings per stride play at a readable rate at full speed");
            Assert.That(Hero.idleFps, Is.InRange(3f, 8f), "a slow, natural breathing loop");
        }

        [Test] public void PaintedProps_StandOnTheirLandmarks_AsVisualOnlyCards()
        {
            OpenB4();
            var props = Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(p => p.name);
            CollectionAssert.AreEquivalent(new[] { "Cottage", "Willow", "Spring", "SpringDormant" }, props.Keys);
            void Near(string name, Vector2 landmark, float metres)
            {
                StaticProp25D p = props[name];
                Vector3 c = name == "Willow" ? p.BaseFront : (p.BaseLeft + p.BaseRight) * 0.5f;
                Assert.Less(Vector2.Distance(new Vector2(c.x, c.z), landmark), metres, name + " stands on its landmark");
            }
            Near("Cottage", new Vector2(25f, 8f), 0.6f);
            Near("Willow", new Vector2(14.5f, 11f), 0.6f);
            Near("Spring", new Vector2(-21f, 16f), 0.6f);
            foreach (StaticProp25D p in props.Values)
            {
                Assert.IsEmpty(p.GetComponentsInChildren<Collider>(true), p.name + " is visual only");
                var renderer = p.GetComponent<MeshRenderer>();
                Assert.AreEqual("Echoes/StaticProp25D", renderer.sharedMaterial.shader.name);
                Assert.IsNotNull(renderer.sharedMaterial.mainTexture, p.name + " has its painting");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, "the real shadow comes from the hidden 3D body");
                Assert.Greater(p.Frame.z, 3f, p.name + " card width in metres");
            }
            Assert.IsTrue(props["Willow"].FadesAroundPlayer, "the willow's canopy opens around the player");
            Assert.IsFalse(props["Cottage"].FadesAroundPlayer);
            var variant = Object.FindFirstObjectByType<StageVariant25D>();
            Assert.IsNotNull(variant, "the spring shows a dormant and a restored painting by stage");
        }

        [Test] public void PaintedCardsAndFoliage_AreAlphaTestedWithDepth_SoTheyNeverDependOnSorting()
        {
            var b4 = OpenB4();
            var cards = Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(p => p.GetComponent<MeshRenderer>().sharedMaterial).ToList();
            var foliage = b4.B4Only.Select(g => g.GetComponent<MeshRenderer>()).Where(r => r != null && r.name == "B4_GroundFoliage").Select(r => r.sharedMaterial).ToList();
            Assert.AreEqual(1, foliage.Count, "one batched foliage mesh");
            foreach (Material m in cards.Concat(foliage))
            {
                Assert.That(m.renderQueue, Is.InRange(2450, 2999), m.name + " draws in the alpha-test queue (depth-written cut-outs, no blending)");
                Assert.AreEqual("TransparentCutout", m.GetTag("RenderType", false), m.name);
            }
            Material hero = b4.B4HeroMaterial;
            Assert.GreaterOrEqual(hero.renderQueue, 3000, "the caretaker draws after them and is depth-tested against their painted depth");
            Assert.IsTrue(b4.RestyleB4.All(m => m.renderQueue < 2450), "the plaza stone is opaque");
        }

        [Test] public void WorkshopCollision_FollowsThePaintedFootprint_AndEveryInteractionStaysReachable()
        {
            var b4 = OpenB4();
            Assert.IsNotEmpty(b4.DisabledInB4, "the old box house's colliders are switched off in B4");
            Assert.IsTrue(b4.DisabledInB4.All(c => !c.enabled));
            Assert.AreEqual(1, b4.B4Colliders.Length);
            Assert.IsTrue(b4.B4Colliders[0].enabled);
            var cottage = Object.FindObjectsByType<StaticProp25D>(FindObjectsSortMode.None).First(p => p.name == "Cottage");
            Physics.SyncTransforms();
            Vector3 inside = (cottage.BaseLeft + cottage.BaseRight) * 0.5f + Vector3.up;
            Assert.IsTrue(Physics.CheckSphere(inside, 0.3f, ~0, QueryTriggerInteraction.Ignore), "the painted workshop is solid");
            // every interaction point has free standing room for the player's capsule within its range
            foreach (Interactable target in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                bool free = false;
                for (int ring = 1; ring <= 4 && !free; ring++)
                    for (int a = 0; a < 16 && !free; a++)
                    {
                        float r = target.Range * ring / 5f, angle = a * Mathf.PI / 8f;
                        Vector3 spot = target.Position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                        Terrain t = Terrain.activeTerrain;
                        float terrainY = t.SampleHeight(spot) + t.transform.position.y;
                        // stand on whatever is there (terrain, the plaza's dais, a step) if a character can step onto it
                        if (!Physics.Raycast(new Vector3(spot.x, terrainY + 6f, spot.z), Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (ground.collider is CharacterController || ground.point.y - terrainY > 0.45f) continue;
                        spot.y = ground.point.y;
                        Vector3 bottom = spot + Vector3.up * 0.45f, top = spot + Vector3.up * 1.35f;
                        bool blocked = Physics.OverlapCapsule(bottom, top, 0.35f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider) && !(c is CharacterController));
                        free = !blocked;
                    }
                Assert.IsTrue(free, target.name + " can still be reached in B4");
            }
            // before mode restores the old collision in the same scene
            b4.Apply(true);
            Assert.IsTrue(b4.DisabledInB4.All(c => c.enabled));
            Assert.IsFalse(b4.B4Colliders[0].enabled);
            b4.Apply(false);
            Assert.IsTrue(b4.DisabledInB4.All(c => !c.enabled));
        }

        [Test] public void ReplacedLandmarks_KeepTheirGameplayColliders()
        {
            OpenB4();
            var willow = Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("Willow_Pilot"))
                .OrderBy(c => Vector2.Distance(new Vector2(c.transform.position.x, c.transform.position.z), new Vector2(14.5f, 11f))).First();
            Assert.IsTrue(willow.enabled, "the willow trunk still blocks");
            Assert.IsFalse(willow.GetComponent<Renderer>().enabled, "the 3D willow is hidden behind its painting");
            Transform spring = GameObject.Find("RuinedSpring").transform;
            Assert.Greater(spring.GetComponentsInChildren<Collider>().Count(c => c.enabled), 5, "the spring's stones still block");
            Assert.IsTrue(spring.GetComponentsInChildren<MeshRenderer>(true).All(r => !r.enabled), "the 3D spring is hidden behind its painting");
            Assert.IsNotNull(Object.FindFirstObjectByType<SpringInteractable>());
            Assert.IsNotNull(Object.FindFirstObjectByType<WorkshopInteractable>());
        }

        [Test] public void Minimap_IsARealBakedMapOfTheValley_TurnedWithTheCamera()
        {
            OpenB4();
            var minimap = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(minimap);
            Assert.AreEqual(45f, minimap.MapYaw, 1e-3f, "up on the map is up on the screen");
            Vector4 b = minimap.WorldBounds;
            Assert.AreEqual(Phase52B4Builder.MapBounds, b);
            Assert.LessOrEqual(b.x, -50f); Assert.LessOrEqual(b.y, -50f);
            Assert.GreaterOrEqual(b.x + b.z, 50f); Assert.GreaterOrEqual(b.y + b.w, 50f);
            var texture = minimap.Map.texture as Texture2D;
            Assert.IsNotNull(texture, "the map shows the baked image");
            Assert.AreEqual(Phase52B4Builder.MinimapBasePath, AssetDatabase.GetAssetPath(texture));
            Assert.AreEqual(texture.width, texture.height);
            // the baked image is a picture of the valley, not a flat colour
            var raw = new Texture2D(2, 2);
            Assert.IsTrue(raw.LoadImage(File.ReadAllBytes(Phase52B4Builder.MinimapBasePath)));
            Color[] px = raw.GetPixels();
            float mean = px.Average(c => c.grayscale), variance = px.Average(c => (c.grayscale - mean) * (c.grayscale - mean));
            Object.DestroyImmediate(raw);
            Assert.Greater(Mathf.Sqrt(variance), 0.04f, "the baked map has terrain, paths and buildings");
            Assert.IsTrue(minimap.GetComponentInParent<Canvas>(true) != null, "the minimap lives on the HUD canvas");
            var so = new SerializedObject(minimap);
            foreach (string field in new[] { "player", "mira", "spring", "heart", "workshop", "wren", "hen", "alder", "playerArrow", "objectiveMarker" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, field + " is wired");
        }

        [Test] public void MinimapTargets_FollowTheObjectivePanel()
        {
            var session = new GameSession();
            Assert.AreEqual(MinimapTargets.Kind.Mira, MinimapTargets.Current(session), "a new game points at Mira");
            Assert.AreEqual(MinimapTargets.Kind.None, MinimapTargets.Current(null));
            Assert.AreEqual(ItemIds.Herb, MinimapTargets.ItemFor(MinimapTargets.Kind.Herb));
            Assert.IsNull(MinimapTargets.ItemFor(MinimapTargets.Kind.Workshop));
        }

        [Test] public void B4Terrain_IsACopyWithACobbledPlaza_AndTheB2TerrainIsUnchanged()
        {
            var b4 = OpenB4();
            Assert.AreNotSame(b4.BeforeTerrain, b4.B4Terrain);
            Assert.AreSame(b4.B4Terrain, b4.Terrain.terrainData);
            Assert.AreEqual(b4.BeforeTerrain.terrainLayers.Length + 1, b4.B4Terrain.terrainLayers.Length, "one added layer: the plaza cobble");
            Assert.AreEqual("PlazaCobbleB4", b4.B4Terrain.terrainLayers.Last().name);
            for (int i = 0; i < 40; i++)
            {
                float x = (i * 0.137f) % 1f, z = (i * 0.291f) % 1f;
                Assert.AreEqual(b4.BeforeTerrain.GetInterpolatedHeight(x, z), b4.B4Terrain.GetInterpolatedHeight(x, z), 1e-4f, "heights untouched");
            }
            Assert.IsFalse(b4.BeforeTerrain.terrainLayers.Any(l => l != null && l.name == "PlazaCobbleB4"), "the B2 pilot terrain never gets the B4 layer");
        }

        [Test] public void PlazaStones_TakeThePaintedFieldstone_AndBeforeModeRestoresTheirMaterials()
        {
            var b4 = OpenB4();
            Assert.Greater(b4.RestyledInB4.Length, 10);
            Assert.AreEqual(b4.RestyledInB4.Length, b4.RestyleBefore.Length);
            Assert.AreEqual(b4.RestyledInB4.Length, b4.RestyleB4.Length);
            Assert.IsTrue(b4.RestyledInB4.All(r => r.sharedMaterial.shader.name == "Echoes/TriplanarStone"));
            b4.Apply(true);
            for (int i = 0; i < b4.RestyledInB4.Length; i++) Assert.AreSame(b4.RestyleBefore[i], b4.RestyledInB4[i].sharedMaterial);
            b4.Apply(false);
            Assert.IsTrue(b4.RestyledInB4.All(r => r.sharedMaterial.shader.name == "Echoes/TriplanarStone"));
        }

        [Test] public void B4Sources_AreTheUnmodifiedOwnerSuppliedFiles()
        {
            var expected = new (string file, string sha)[]
            {
                ("art-direction/b4_isometric_style_target.png", "91b70961f36e04b9bb3188c91d146d39165ed7b5f47e2614935ef29c750888b9"),
                ("characters/idle/hero_idle_E.png", "5a35257d0c370c27eed4677957ebf2da65272b9a8c2836a7d40c4ec21ad58b8e"),
                ("characters/idle/hero_idle_N.png", "15f36f363b174e9915eed9010237b0c489fb24b8c40bc263f5bba947d3950658"),
                ("characters/idle/hero_idle_S.png", "f6f93c3ce5d8ba0b06ec04625d55f4777673404ed16829259a7c77f4e3cf65cc"),
                ("characters/idle/hero_idle_W.png", "08c38fa0136ab1b8720a0adc18d5528e7852671ff46ae0db3894a6d1b3f4647c"),
                ("characters/run-keyposes/hero_run_E.png", "3142fa75b74b7e482221fe9177f2b990719fa3e61474596b936932885f4b8954"),
                ("characters/run-keyposes/hero_run_N.png", "fbdeed57a9f3a5306470af1cd9298e149934a05c3d7833f7f8af9f58160b3578"),
                ("characters/run-keyposes/hero_run_S.png", "9cea55830de32123d692769ae90423751b631030a359f362ed8c9f56869c7aa5"),
                ("characters/run-keyposes/hero_run_W.png", "ea38a30633a9c6fd585c21fba76945a9c3ee85574fd2c4177083507b06ed6ea1"),
                ("environment/foliage/broadleaf_branch.png", "274c5f6eaa9a92a444546798f98dcb068213c5459044d7c1d326b6afd72cd4f9"),
                ("environment/foliage/forest_fern.png", "c6bd51438486a22c557e05ed232004ef3b0153d3152f33a95f4017e470cd437c"),
                ("environment/foliage/meadow_grass_clump.png", "b4270b5d5bd325ad589f5d6647cd8bd21024488d1e9110826134b52d2fc536dd"),
                ("environment/foliage/valley_wildflowers.png", "227031b58bcaaf42acf991716e8b94dd62c0539c546a8534cb77fc70df88e71d"),
                ("environment/foliage/willow_leaf_fronds.png", "5f479aec36ae0e1ea08a5b7b9499886c1792fa98addb47fec44865f7fd43aa97"),
                ("environment/isometric-static-props/spring_ruins_iso.png", "29dcf0ad5d9fe7f28316da33df4655ee99bcbcc060df18394c42b21a05985680"),
                ("environment/isometric-static-props/valley_apothecary_cottage_iso.png", "d4444737340f85aa951b6aa202d8cf5cd1c4e4710ed4313c786ce82650ba012b"),
                ("environment/isometric-static-props/willow_tree_iso.png", "dd97a40c10ac3e8de6c0c48b4fbf1443314e1d56b799a77aae7d373416a12a11"),
                ("environment/materials/mossy_stone_tile.png", "0ac381f44f3d6816ec2cc5d5ce14bdfb899dc85b2c29d046487f750607ac7ef1"),
                ("environment/materials/plaster_wall_tile.png", "ab5c740f3ab9bb5ed2b2868ae7fb0d42038b067e199105dc823264d95d502a3e"),
                ("environment/materials/roof_tiles_tile.png", "f6529a9134e02396e84b91a2b92afab627e3f40dc98d3850c370a200fcf3731a"),
                ("environment/materials/weathered_timber_tile.png", "50e2e8d0554b7ecf2ac75bdee8bee84e789b3d35c4313da4d69574d1e906b53e"),
                ("ui/minimap_frame.png", "63859deb9b9ba61dffd6a0cb28015286cf24301608af3f958eefe41e34fc541c"),
            };
            using (var sha = SHA256.Create())
                foreach (var e in expected)
                {
                    string path = Phase52B4Builder.Folder + "/Source/" + e.file;
                    Assert.AreEqual(e.sha, string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(b => b.ToString("x2"))), e.file);
                }
            string atlas = File.ReadAllText(Phase52B4Builder.Folder + "/Sprites/CaretakerB4Atlas.json");
            foreach (var e in expected.Where(x => x.file.StartsWith("characters/")))
                StringAssert.Contains("\"" + e.file + "\": \"" + e.sha + "\"", atlas, e.file + " is recorded as a sprite source");
        }

        [Test] public void OriginalB2AndB3Scenes_ContainNoB4Presentation()
        {
            foreach (string scene in new[] { AssetPaths.ValleyScene, Phase52PilotBuilder.Valley, Phase52B3Builder.Valley })
            {
                EditorSceneManager.OpenScene(scene);
                Assert.IsNull(Object.FindFirstObjectByType<Phase52B4Pilot>(FindObjectsInactive.Include), scene);
                Assert.IsNull(Object.FindFirstObjectByType<StaticProp25D>(FindObjectsInactive.Include), scene);
                Assert.IsNull(Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include), scene);
            }
            EditorSceneManager.OpenScene(Phase52B3Builder.Valley);
            var follow = Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Assert.AreEqual(30f, follow.Pitch, 1e-3f, "the B3 scene keeps its own camera");
            Assert.GreaterOrEqual(follow.MaxOrbitDegrees, 180f, "the B3 scene keeps its full ending orbit");
        }
    }
}
