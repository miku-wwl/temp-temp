using System.Collections.Generic;
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
    /// Phase 5.2-B5 painted environment pilot: the owner's six paintings in the B4 scene. Engineering properties only (sources, seams,
    /// import settings, stage paintings, collision, reachability, the before switch, the untouched earlier scenes), never an art verdict.
    /// </summary>
    public class Phase52B5Tests
    {
        static Phase52B5Pilot OpenB5()
        {
            EditorSceneManager.OpenScene(Phase52B5Builder.Valley);
            var b5 = Object.FindFirstObjectByType<Phase52B5Pilot>();
            Assert.IsNotNull(b5, "the B5 scene carries its presentation switch");
            return b5;
        }

        static Texture2D Png(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
            return texture;
        }

        static SerializedObject Stages() => new SerializedObject(Object.FindFirstObjectByType<ResonanceView>());
        static GameObject StageRoot(string field) => (GameObject)Stages().FindProperty(field).objectReferenceValue;

        [Test] public void B5Sources_AreTheUnmodifiedOwnerSuppliedFiles()
        {
            var expected = new (string file, string sha)[]
            {
                ("broadleaf_tree.png", "3ff9ee2d8296fbc3a4b1e9738d8ba036e73e082e4d8604fefbb221e2548aec1b"),
                ("farm_raised_beds.png", "39244c66b88f572645a9de0958026f255512125afbeea66a478d974e9d3df42c"),
                ("meadow_grass_albedo.png", "77f21e28d37b41e4d9afaf8c31f65108a8ad608713c2fd3a144d9160c767aac0"),
                ("spring_S0_dormant.png", "c9abee13c1713b024934ba8bb6896c00c508592aef649c516487d1b8f3389b76"),
                ("spring_S3_restored.png", "c2bdbe572487e831d75d85303bccc1ad6784a744256aa9de9d7922c54180e2ed"),
                ("tilled_soil_albedo.png", "1a8e9d4d44ef6dd1bcd1aad983f3725921e00015046bde7e49b903fc6e06f7e7"),
            };
            using (var sha = SHA256.Create())
                foreach (var e in expected)
                    Assert.AreEqual(e.sha, string.Concat(sha.ComputeHash(File.ReadAllBytes(Phase52B5Builder.Folder + "/Source/" + e.file)).Select(b => b.ToString("x2"))), e.file);
        }

        /// <summary>Opposite edges of a tile differ no more than neighbouring texels inside it do (1.0 = as continuous as the inside).</summary>
        [Test] public void GroundTextures_WrapWithoutSeams_InBothDirections()
        {
            foreach (string name in new[] { "MeadowA", "MeadowB", "TilledSoil", "PlazaCobbleWarm" })
            {
                Texture2D t = Png(Phase52B5Builder.Folder + "/Terrain/" + name + ".png");
                Color32[] p = t.GetPixels32();
                int w = t.width, h = t.height;
                double inX = 0, inY = 0, edgeX = 0, edgeY = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = p[y * w + x];
                        if (x + 1 < w) inX += Diff(c, p[y * w + x + 1]);
                        if (y + 1 < h) inY += Diff(c, p[(y + 1) * w + x]);
                    }
                for (int y = 0; y < h; y++) edgeX += Diff(p[y * w], p[y * w + w - 1]);
                for (int x = 0; x < w; x++) edgeY += Diff(p[x], p[(h - 1) * w + x]);
                double ratioX = edgeX / h / (inX / ((w - 1) * h)), ratioY = edgeY / w / (inY / ((h - 1) * w));
                Object.DestroyImmediate(t);
                Assert.Less(ratioX, 1.15, name + " left/right wrap");
                Assert.Less(ratioY, 1.15, name + " top/bottom wrap");
            }
        }

        static double Diff(Color32 a, Color32 b) => (System.Math.Abs(a.r - b.r) + System.Math.Abs(a.g - b.g) + System.Math.Abs(a.b - b.b)) / 3.0;

        [Test] public void GroundTextures_ImportAsOpaqueRepeatingMipmappedDxt1_WithAnisotropy()
        {
            foreach (string name in new[] { "MeadowA", "MeadowB", "TilledSoil", "PlazaCobbleWarm" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Phase52B5Builder.Folder + "/Terrain/" + name + ".png");
                Assert.IsNotNull(importer, name);
                Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource, name + ": no alpha (URP reads a diffuse alpha as smoothness)");
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode, name);
                Assert.IsTrue(importer.mipmapEnabled, name);
                Assert.AreEqual(FilterMode.Trilinear, importer.filterMode, name);
                Assert.GreaterOrEqual(importer.anisoLevel, 4, name + ": no shimmer at the high camera's grazing angle");
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.IsTrue(standalone.overridden);
                Assert.AreEqual(TextureImporterFormat.DXT1, standalone.format, name);
                Assert.IsTrue(importer.sRGBTexture, name);
            }
        }

        [Test] public void B5Terrain_KeepsTheB4Heights_AndUsesThePaintedLayers()
        {
            var b5 = OpenB5();
            var b4 = Object.FindFirstObjectByType<Phase52B4Pilot>();
            Assert.AreSame(b4.B4Terrain, b5.BeforeTerrain);
            Assert.AreNotSame(b5.BeforeTerrain, b5.B5Terrain);
            Assert.AreSame(b5.B5Terrain, b5.Terrain.terrainData);
            Assert.AreSame(b5.B5Terrain, b5.Terrain.GetComponent<TerrainCollider>().terrainData, "collision uses the same (copied) heights");
            CollectionAssert.AreEqual(new[] { "B5MeadowA", "B5MeadowB", "RouteDirt", "Stone", "B5PlazaCobble", "B5TilledSoil" }, b5.B5Terrain.terrainLayers.Select(l => l.name).ToArray());
            for (int i = 0; i < 60; i++)
            {
                float x = (i * 0.137f) % 1f, z = (i * 0.291f) % 1f;
                Assert.AreEqual(b5.BeforeTerrain.GetInterpolatedHeight(x, z), b5.B5Terrain.GetInterpolatedHeight(x, z), 1e-4f, "heights untouched");
            }
            TerrainData d = b5.B5Terrain;
            float[,,] maps = d.GetAlphamaps(0, 0, d.alphamapWidth, d.alphamapHeight);
            for (int z = 0; z < d.alphamapHeight; z += 7)
                for (int x = 0; x < d.alphamapWidth; x += 7)
                {
                    float sum = 0f;
                    for (int l = 0; l < d.terrainLayers.Length; l++) sum += maps[z, x, l];
                    Assert.AreEqual(1f, sum, 0.02f, "every texel is fully covered");
                }
            Assert.IsFalse(b4.B4Terrain.terrainLayers.Any(l => l.name.StartsWith("B5")), "the B4 terrain never gets a B5 layer");
        }

        [Test] public void SpringStages_AreFourRegisteredPaintingsOnOneFootprint()
        {
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Phase52B5Builder.Folder + "/Props/SpringStages.png");
            Assert.IsNotNull(array, "the stage strip imports as a Texture2DArray");
            Assert.AreEqual(4, array.depth);
            Texture2D strip = Png(Phase52B5Builder.Folder + "/Props/SpringStages.png");
            int w = strip.width, h = strip.height / 4;
            Color32[] p = strip.GetPixels32();
            Object.DestroyImmediate(strip);
            // slice s is the s-th from the top of the PNG; texture rows run upwards
            bool Solid(int s, int x, int y) => p[((3 - s) * h + y) * w + x].a > 128;
            int Water(int s)
            {
                int n = 0;
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x += 2)
                    {
                        Color32 c = p[((3 - s) * h + y) * w + x];
                        if (c.a < 200) continue;
                        Color.RGBToHSV(c, out float hue, out float sat, out float val);
                        if (hue * 360f > 160f && hue * 360f < 215f && sat > 0.18f && val > 0.18f) n++;
                    }
                return n;
            }
            for (int s = 0; s < 3; s++)
            {
                int both = 0, either = 0;
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x += 2)
                    {
                        bool a = Solid(s, x, y), b = Solid(3, x, y);
                        if (a && b) both++;
                        if (a || b) either++;
                    }
                Assert.Greater(both / (float)either, 0.95f, "stage " + s + " stands on stage 3's footprint (registered paintings: no jump between stages)");
            }
            int w0 = Water(0), w1 = Water(1), w2 = Water(2), w3 = Water(3);
            Assert.Greater(w3, 5000, "the restored spring has clear water");
            Assert.Less(w0, w3 * 0.15f, "the silent spring is drained (the supplied S0 painting still had its falls and a full pool)");
            Assert.Greater(w1, w3 * 0.7f, "the stream restored (stage 1) brings the water back");
            Assert.Greater(w2, w3 * 0.7f);
        }

        [Test] public void SpringCard_ShowsTheShownStage_OnTheSpringsLandmark()
        {
            OpenB5();
            var painting = Object.FindFirstObjectByType<StagePainting25D>(FindObjectsInactive.Include);
            Assert.IsNotNull(painting);
            var renderer = painting.GetComponent<MeshRenderer>();
            Assert.AreEqual("Echoes/StageProp25D", renderer.sharedMaterial.shader.name);
            Assert.AreSame(Object.FindFirstObjectByType<ResonanceView>(), new SerializedObject(painting).FindProperty("view").objectReferenceValue, "the painting reads the valley's own stage");
            var card = painting.GetComponent<StaticProp25D>();
            Vector3 c = (card.BaseLeft + card.BaseRight) * 0.5f;
            Assert.Less(Vector2.Distance(new Vector2(c.x, c.z), Phase52B5Builder.SpringCentre), 0.6f, "the painted footprint is centred on the spring");
            Assert.IsEmpty(painting.GetComponentsInChildren<Collider>(true), "the card is visual only");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode);
            var spring = Object.FindFirstObjectByType<SpringInteractable>();
            Assert.Less(Vector2.Distance(new Vector2(spring.Position.x, spring.Position.z), Phase52B5Builder.SpringCentre), 0.1f, "the interaction stays where it was");
            foreach (var old in Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.name == "Spring" || p.name == "SpringDormant"))
                Assert.IsFalse(old.GetComponent<MeshRenderer>().enabled, "the B4 spring paintings are hidden in B5");
        }

        [Test] public void PaintedCards_AreAlphaTestedWithDepth_AndTheirTexturesKeepCoverageInTheMipmaps()
        {
            OpenB5();
            var materials = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(r => r.sharedMaterial).Where(m => m != null && (m.shader.name == "Echoes/PaintedCard25D" || m.shader.name == "Echoes/StageProp25D")).Distinct().ToList();
            Assert.GreaterOrEqual(materials.Count, 4, "trees, garden plants, ground foliage and the spring");
            foreach (Material m in materials)
            {
                Assert.That(m.renderQueue, Is.InRange(2450, 2999), m.name + " draws in the alpha-test queue (depth-written cut-outs, no sorting)");
                Assert.AreEqual("TransparentCutout", m.GetTag("RenderType", false), m.name);
            }
            foreach (string path in new[] { "/Props/BroadleafTree.png", "/Props/GardenPlants.png", "/Props/SpringStages.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Phase52B5Builder.Folder + path);
                Assert.IsTrue(importer.mipmapEnabled, path);
                Assert.IsTrue(importer.mipMapsPreserveCoverage, path + ": leaves do not thin out at a distance");
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, path);
                Assert.AreEqual(TextureImporterCompression.CompressedHQ, importer.textureCompression, path);
            }
            Assert.AreEqual(TextureImporterShape.Texture2DArray, ((TextureImporter)AssetImporter.GetAtPath(Phase52B5Builder.Folder + "/Props/SpringStages.png")).textureShape);
        }

        [Test] public void PaintedTextures_TransparentTexelsCarryBledColour_NoDarkOrWhiteHalo()
        {
            foreach (string path in new[] { "/Props/BroadleafTree.png", "/Props/GardenPlants.png", "/Props/SpringStages.png" })
            {
                Texture2D t = Png(Phase52B5Builder.Folder + path);
                Color32[] p = t.GetPixels32();
                int w = t.width, h = t.height, edge = 0, bad = 0;
                for (int y = 1; y < h - 1; y += 1)
                    for (int x = 1; x < w - 1; x++)
                    {
                        Color32 c = p[y * w + x];
                        if (c.a > 8) continue;
                        // next to any texel that filtering and mipmaps will show (the painted edges fade out softly, so not only opaque ones)
                        if (p[y * w + x + 1].a < 64 && p[y * w + x - 1].a < 64 && p[(y + 1) * w + x].a < 64 && p[(y - 1) * w + x].a < 64) continue;
                        edge++;
                        if (c.r + c.g + c.b < 6 || (c.r > 250 && c.g > 250 && c.b > 250)) bad++;
                    }
                Object.DestroyImmediate(t);
                Assert.Greater(edge, 500, path + " has edges to check");
                Assert.Less(bad / (float)edge, 0.01f, path + ": transparent texels next to the painting carry its colour");
            }
        }

        [Test] public void PaintedTrees_StandOnTheOldTrees_WhoseTrunksStillBlock_AndWhoseShadowsStay()
        {
            var b5 = OpenB5();
            var trees = GameObject.Find("B5_PaintedTrees");
            Assert.IsNotNull(trees);
            Mesh mesh = trees.GetComponent<MeshFilter>().sharedMesh;
            int cards = mesh.vertexCount / 6;
            Assert.Greater(cards, 50, "the valley's trees are painted");
            Vector3[] v = mesh.vertices;
            Color[] colours = mesh.colors;
            Terrain t = b5.Terrain;
            int opens = 0;
            for (int i = 0; i < cards; i++)
            {
                Vector3 ground = v[i * 6];
                Assert.AreEqual(t.SampleHeight(ground) + t.transform.position.y, ground.y, 0.35f, "tree " + i + " stands on the ground");
                if (colours[i * 6].a > 0.5f) opens++;
            }
            Assert.Greater(opens, 30, "the trees near the paths open around the player");
            Assert.Greater(b5.Trunks.Length, 30);
            foreach (var trunk in b5.Trunks)
            {
                Assert.IsTrue(trunk.collider.enabled, trunk.collider.name + " still blocks");
                float scale = Mathf.Max(trunk.collider.transform.lossyScale.x, trunk.collider.transform.lossyScale.z);
                Assert.GreaterOrEqual(trunk.collider.radius * scale, 0.29f, "the trunk is as wide as the painted one");
                Assert.Less(trunk.collider.radius * scale, 0.45f, "and no wider: paths stay open");
            }
            Assert.Greater(b5.ShadowOnlyInB5.Length, 50);
            Assert.IsTrue(b5.ShadowOnlyInB5.All(r => r.enabled && r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly), "the old trees cast the shadow under the painted canopy");
            Assert.AreEqual("Echoes/PaintedCard25D", trees.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
        }

        [Test] public void GardenBeds_StandOnTheRealGardenLayout_WithCollision_AndThePlantsFollowTheGardensOwnStage()
        {
            var b5 = OpenB5();
            GameObject bloom = StageRoot("gardenBloomRoot"), dormant = StageRoot("gardenDormant");
            Transform garden = bloom.transform.parent;
            Assert.AreSame(garden, dormant.transform.parent);
            Transform beds = garden.Find("B5_GardenBeds");
            Assert.IsNotNull(beds);
            Assert.AreEqual(4, beds.childCount);
            for (int i = 0; i < 4; i++)
            {
                Transform bed = beds.GetChild(i);
                Assert.AreEqual(Phase52B5Builder.BedCentres[i].x, bed.localPosition.x, 1e-3f);
                Assert.AreEqual(Phase52B5Builder.BedCentres[i].y, bed.localPosition.z, 1e-3f);
                var box = bed.GetComponent<BoxCollider>();
                Assert.IsNotNull(box, "a raised bed is solid");
                Assert.IsTrue(b5.B5Colliders.Contains(box));
                Assert.IsTrue(box.enabled);
                // the old soil boxes of this bed lie inside the new frame
                Vector3 local = garden.InverseTransformPoint(bed.position);
                Assert.Less(Mathf.Abs(local.x - Phase52B5Builder.BedCentres[i].x), 0.01f);
                Transform lush = bloom.transform.Find("B5_BloomBed" + i);
                Assert.IsNotNull(lush, "the lush plants of bed " + i + " live under the garden's Bloom root");
                Assert.AreEqual(bed.localPosition, lush.localPosition);
            }
            Assert.IsNotNull(dormant.transform.Find("B5_DormantBeds"), "the withered plants live under the Dormant root");
            Assert.IsFalse(bloom.activeSelf, "the scene starts in the silent valley: no lush plants");
            Assert.IsTrue(dormant.activeSelf);
            // the paths between the beds stay wide enough for the player's capsule (radius 0.3)
            float laneX = 2f * (Mathf.Abs(Phase52B5Builder.BedCentres[0].x) - Phase52B5Builder.BedSize.x * 0.5f - 0.02f);
            float laneZ = 2f * (Mathf.Abs(Phase52B5Builder.BedCentres[0].y) - Phase52B5Builder.BedSize.z * 0.5f);
            Assert.Greater(laneX, 0.7f);
            Assert.Greater(laneZ, 0.7f);
        }

        [Test] public void EveryInteraction_StaysReachable_WithTheB5Collision()
        {
            OpenB5();
            Physics.SyncTransforms();
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
                        if (!Physics.Raycast(new Vector3(spot.x, terrainY + 6f, spot.z), Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (ground.collider is CharacterController || ground.point.y - terrainY > 0.45f) continue;
                        spot.y = ground.point.y;
                        Vector3 bottom = spot + Vector3.up * 0.45f, top = spot + Vector3.up * 1.35f;
                        free = !Physics.OverlapCapsule(bottom, top, 0.35f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider) && !(c is CharacterController));
                    }
                Assert.IsTrue(free, target.name + " can still be reached in B5");
            }
        }

        [Test] public void SpringFootprint_IsSolid_AndOnlyOldStonesOutsideThePaintingStopBlocking()
        {
            var b5 = OpenB5();
            Physics.SyncTransforms();
            Vector3 centre = new Vector3(Phase52B5Builder.SpringCentre.x, 0f, Phase52B5Builder.SpringCentre.y);
            centre.y = b5.Terrain.SampleHeight(centre) + b5.Terrain.transform.position.y + 0.8f;
            Assert.IsTrue(Physics.CheckSphere(centre, 0.3f, ~0, QueryTriggerInteraction.Ignore), "the painted pool and rim cannot be walked through");
            Assert.LessOrEqual(b5.DisabledInB5.Length, 6);
            Transform spring = GameObject.Find("RuinedSpring").transform;
            foreach (Collider c in b5.DisabledInB5)
            {
                Assert.IsTrue(c.transform.IsChildOf(spring), "only the old spring's stones are switched off");
                Assert.IsFalse(c.enabled);
            }
        }

        [Test] public void GatheringSpots_KeepTheirNodes_WithPaintedReadyAndCutVisuals()
        {
            OpenB5();
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(n => n.ItemId == ItemIds.Herb || n.ItemId == ItemIds.Flower).ToList();
            Assert.AreEqual(11, nodes.Count, "six herbs and five Dawn Blossoms");
            foreach (var node in nodes)
            {
                Transform ready = node.transform.Find("Ready"), depleted = node.transform.Find("Depleted");
                Assert.IsNotNull(ready.Find("B5_Ready"), node.NodeId + " has a painted ready plant");
                Assert.IsNotNull(depleted.Find("B5_Depleted"), node.NodeId + " has a painted cut plant");
                var so = new SerializedObject(node);
                Assert.AreSame(ready.gameObject, so.FindProperty("readyVisual").objectReferenceValue, "the node still switches its own ready visual");
                Assert.AreSame(depleted.gameObject, so.FindProperty("depletedVisual").objectReferenceValue);
                Assert.AreEqual(2.4f, node.Range, 1e-3f, "the gathering range is unchanged");
            }
        }

        [Test] public void Minimap_IsTheB5ValleyBaked_AndBeforeModeShowsTheB4Bake()
        {
            var b5 = OpenB5();
            var minimap = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            Assert.AreSame(minimap.Map, b5.MinimapImage);
            Assert.AreEqual(Phase52B5Builder.MinimapBasePath, AssetDatabase.GetAssetPath(b5.B5Minimap));
            Assert.AreEqual(Phase52B4Builder.MinimapBasePath, AssetDatabase.GetAssetPath(b5.BeforeMinimap));
            Assert.AreSame(b5.B5Minimap, minimap.Map.texture, "the scene shows the B5 bake");
            Texture2D raw = Png(Phase52B5Builder.MinimapBasePath);
            Color[] px = raw.GetPixels();
            float mean = px.Average(c => c.grayscale), variance = px.Average(c => (c.grayscale - mean) * (c.grayscale - mean));
            Object.DestroyImmediate(raw);
            Assert.Greater(Mathf.Sqrt(variance), 0.04f, "a picture of the valley, not a flat colour");
            Assert.AreEqual(Phase52B4Builder.MapBounds, minimap.WorldBounds, "the same bounds: the map's scale and the markers are unchanged");
            Assert.AreEqual(45f, minimap.MapYaw, 1e-3f);
            b5.Apply(true);
            Assert.AreSame(b5.BeforeMinimap, minimap.Map.texture);
            b5.Apply(false);
            Assert.AreSame(b5.B5Minimap, minimap.Map.texture);
        }

        [Test] public void BeforeMode_RestoresTheB4Presentation_InTheSameScene()
        {
            var b5 = OpenB5();
            b5.Apply(true);
            Assert.IsTrue(b5.B5Only.All(g => !g.activeSelf), "B5 objects are off");
            Assert.IsTrue(b5.HiddenInB5.All(r => r.enabled), "B4 renderers are back");
            Assert.IsTrue(b5.ShadowOnlyInB5.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On));
            Assert.IsTrue(b5.B5Colliders.All(c => !c.enabled), "B5 collision is off");
            Assert.IsTrue(b5.DisabledInB5.All(c => c.enabled), "the old spring stones block again");
            Assert.IsTrue(b5.Restyled.All(s => s.renderer.sharedMaterial == s.before));
            Assert.IsTrue(b5.Trunks.All(s => Mathf.Approximately(s.collider.radius, s.beforeRadius)));
            Assert.AreSame(b5.BeforeTerrain, b5.Terrain.terrainData);
            b5.Apply(false);
            Assert.IsTrue(b5.B5Only.All(g => g.activeSelf));
            Assert.IsTrue(b5.HiddenInB5.All(r => !r.enabled));
            Assert.IsTrue(b5.B5Colliders.All(c => c.enabled));
            Assert.IsTrue(b5.Restyled.All(s => s.renderer.sharedMaterial == s.b5));
            Assert.AreSame(b5.B5Terrain, b5.Terrain.terrainData);
        }

        [Test] public void B5Scene_KeepsTheB4CaretakerCameraAndPlayerRoot()
        {
            OpenB5();
            var b4 = Object.FindFirstObjectByType<Phase52B4Pilot>();
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, players.Length);
            Assert.AreSame(b4.B4Sheet, players[0].GetComponentInChildren<CharacterVisual2D5D>(true).Sheet, "the B4 drawings, unchanged");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<SpriteSheet25D>(Phase52B4Builder.HeroSheetPath), b4.B4Sheet);
            Assert.AreEqual(4.5f, players[0].MoveSpeed, 1e-4f, "the controller speed is unchanged, so the drawn stride still matches it");
            var follow = Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Assert.AreEqual(45f, follow.Yaw, 1e-3f);
            Assert.AreEqual(48f, follow.Pitch, 1e-3f, "default pitch 48; the high camera is kept");
        }

        [Test] public void SilentValleyLight_IsLessBlue_InB5_AndTheB4LightIsKeptForBeforeMode()
        {
            var b5 = OpenB5();
            Assert.AreSame(Object.FindFirstObjectByType<ValleyLighting>(), b5.Lighting);
            Color before = b5.BeforeLight.sun, after = b5.B5Light.sun;
            Assert.Greater(before.a, 0f);
            Assert.Greater(after.a, 0f);
            Assert.Less(after.b - after.r, before.b - before.r, "the B5 silent valley's sun is less blue than B4's");
            float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            Assert.AreEqual(Luma(before), Luma(after), 0.05f, "at the same muted level");
            Assert.AreEqual(Luma(b5.BeforeLight.ambientSky), Luma(b5.B5Light.ambientSky), 0.05f);
        }

        [Test] public void OriginalB2B3AndB4Scenes_ContainNoB5Presentation()
        {
            foreach (string scene in new[] { AssetPaths.ValleyScene, Phase52PilotBuilder.Valley, Phase52B3Builder.Valley, Phase52B4Builder.Valley })
            {
                EditorSceneManager.OpenScene(scene);
                Assert.IsNull(Object.FindFirstObjectByType<Phase52B5Pilot>(FindObjectsInactive.Include), scene);
                Assert.IsNull(Object.FindFirstObjectByType<StagePainting25D>(FindObjectsInactive.Include), scene);
                Assert.IsFalse(Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Echoes/PaintedCard25D"), scene);
            }
        }
    }
}
