using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Echoes.Tests
{
    /// <summary>
    /// Phase 5.2-B5: the painted environment pilot scene on the real player, controller, interaction, quest, save, render and minimap
    /// systems. The presentation changes must leave gameplay as it was; where B5 adds collision (raised beds) or visual states (the
    /// spring's stage paintings, painted gathering spots, the opening in a tree canopy) these tests drive them through real play.
    /// </summary>
    public class Phase52B5PlayTests : ValleyPlayTestBase
    {
#if UNITY_EDITOR
        const string B5Valley = "Assets/Echoes/Art/Phase52B5/Scenes/Valley.unity";

        Phase52B5Pilot b5;
        Phase52B4Pilot b4;
        StagePainting25D springPainting;

        IEnumerator LoadB5()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(B5Valley, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            FindSceneObjects();
            b5 = Object.FindFirstObjectByType<Phase52B5Pilot>();
            b4 = Object.FindFirstObjectByType<Phase52B4Pilot>();
            springPainting = Object.FindFirstObjectByType<StagePainting25D>();
            Assert.IsNotNull(b5);
            Assert.IsFalse(b5.BeforeMode, "the B5 presentation is the default");
            Assert.IsNotNull(springPainting);
        }

        Vector3 Ground(float x, float z)
        {
            var v = new Vector3(x, 0f, z);
            Terrain t = Terrain.activeTerrain;
            v.y = t.SampleHeight(v) + t.transform.position.y + 0.05f;
            return v;
        }

        [UnityTest] public IEnumerator B5Scene_RestoresTheStream_TheSpringPaintingFollowsTheStage_AndContinueKeepsIt()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            yield return null;
            Assert.AreEqual(0f, springPainting.Stage, 1e-3f, "the silent valley shows the drained spring");
            yield return RestoreTheStream();   // exploration, Mira, two herbs and a blossom, the tonic at the workshop, the spring
            Assert.AreEqual(ResonanceStage.AwakeningStream, Session.Resonance);
            float waited = 0f;
            bool between = false;
            while ((resonanceView.IsTransitioning || resonanceView.ShownStage != ResonanceStage.AwakeningStream) && waited < 15f)
            {
                if (springPainting.Stage > 0.05f && springPainting.Stage < 0.95f) between = true;
                waited += Time.deltaTime;
                yield return null;
            }
            yield return null;
            Assert.IsTrue(between, "the painting changes over the stage transition, not in one cut");
            Assert.AreEqual(1f, springPainting.Stage, 1e-3f, "the restored stream shows the stage-1 painting (water back, lanterns still unlit)");

            Assert.IsTrue(host.SaveNow(), "the B5 scene saves like the game");
            GameLaunch.Mode = LaunchMode.Continue;
            yield return LoadB5();
            Assert.AreEqual(StreamQuestStage.Completed, Session.StreamQuest, "Continue restores the quest in the B5 scene");
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Object.FindFirstObjectByType<StagePainting25D>().Stage, 1e-3f, "a continued game shows the stage-1 spring at once");
        }

        [UnityTest] public IEnumerator RaisedBeds_StopThePlayer_InB5_AndNotInTheB4BeforeMode()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            Transform garden = ((GameObject)typeof(ResonanceView).GetField("gardenBloomRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(resonanceView)).transform.parent;
            // start in the lane between the two west beds and walk north into the north-west bed
            Vector3 lane = garden.TransformPoint(new Vector3(-2f, 0f, 0f));
            foreach (bool before in new[] { false, true })
            {
                b5.Apply(before);
                Physics.SyncTransforms();
                player.Teleport(Ground(lane.x, lane.z));
                yield return null;
                Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized, right = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
                Vector3 north = garden.TransformDirection(Vector3.forward);
                player.InputOverride = new Vector2(Vector3.Dot(north, right), Vector3.Dot(north, forward)).normalized;
                yield return new WaitForSeconds(0.8f);
                player.InputOverride = null;
                float z = garden.InverseTransformPoint(player.transform.position).z;
                if (!before) Assert.Less(z, 0.55f, "B5: the bed's frame stops the player at its edge");
                else Assert.Greater(z, 1.2f, "before mode (B4): the beds were not solid");
            }
            b5.Apply(false);
        }

        [UnityTest] public IEnumerator GatheringAHerb_SwapsThePaintedPlantForItsCutStump_AndTheTimerBringsItBack()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            ResourceNode node = Node("herb_3");
            Transform ready = node.transform.Find("Ready"), depleted = node.transform.Find("Depleted");
            Assert.IsTrue(ready.Find("B5_Ready").gameObject.activeInHierarchy, "a ready herb shows the painted plant");
            Assert.IsFalse(depleted.Find("B5_Depleted").gameObject.activeInHierarchy);
            int before = Session.Inventory.Count(node.ItemId);
            yield return GatherFrom("herb_3");
            Assert.AreEqual(before + 1, Session.Inventory.Count(node.ItemId), "gathering works as in B4");
            Assert.IsFalse(ready.Find("B5_Ready").gameObject.activeInHierarchy, "the gathered herb is not shown as still there");
            Assert.IsTrue(depleted.Find("B5_Depleted").gameObject.activeInHierarchy, "its cut stump is");
            Session.Tick(node.CooldownSeconds + 0.5f);
            yield return null;
            yield return null;
            Assert.IsTrue(node.IsReady);
            Assert.IsTrue(ready.Find("B5_Ready").gameObject.activeInHierarchy, "it grows back with the node's own timer");
        }

        /// <summary>
        /// The caretaker stands behind a painted tree by the hen yard. The real camera renders the frame with and without her, and again with
        /// nothing but the terrain and her: the share of her that changes the picture is the share that can be seen. With the canopy's
        /// opening most of her shows; with the opening switched off (only for this check) the same canopy hides much more of her.
        /// </summary>
        [UnityTest] public IEnumerator PlayerBehindAPaintedTree_StaysVisibleThroughTheCanopysOpening()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            Vector3 trunk = new Vector3(14.5f, 0f, -5.5f);
            Vector3 away = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 behind = trunk + away * 2.6f;
            player.Teleport(Ground(behind.x, behind.z));
            player.transform.rotation = Quaternion.LookRotation(-away);
            var follow = Object.FindFirstObjectByType<FixedFollowCamera25D>();
            follow.Snap();
            for (int i = 0; i < 4; i++) yield return null;
            follow.Snap();
            yield return null;
            var trees = GameObject.Find("B5_PaintedTrees").GetComponent<MeshRenderer>();
            float open = VisibleShare(b4.HeroCard, player.transform.position);
            var block = new MaterialPropertyBlock();
            trees.GetPropertyBlock(block);
            block.SetFloat("_FadeStrength", 0f);
            trees.SetPropertyBlock(block);
            float closed = VisibleShare(b4.HeroCard, player.transform.position);
            trees.SetPropertyBlock(null);
            Debug.Log("[Echoes][P52 B5 TEST] behind the painted tree: visible share " + open.ToString("F3") + " with the opening, " + closed.ToString("F3") + " without");
            Assert.Less(closed, 0.5f, "without the opening the canopy hides much of her (the test stands her behind it)");
            Assert.Greater(open, 0.6f, "with the opening most of her is visible");
            Assert.Greater(open - closed, 0.25f, "the opening is what keeps her in view");
        }

        /// <summary>Visible share of the caretaker: changed pixels in her screen box, against the same count with only the terrain and her
        /// (rendered into a texture: a batch-mode test has no screen to capture).</summary>
        static float VisibleShare(Renderer hero, Vector3 feet)
        {
            Camera cam = Camera.main;
            Vector3 a = cam.WorldToScreenPoint(feet), b = cam.WorldToScreenPoint(feet + Vector3.up * 1.75f);
            float tall = Mathf.Abs(b.y - a.y);
            int w = cam.pixelWidth, h = cam.pixelHeight;
            int x0 = Mathf.Clamp((int)(a.x - 0.32f * tall), 0, w - 1), x1 = Mathf.Clamp((int)(a.x + 0.32f * tall), 0, w - 1);
            int y0 = Mathf.Clamp((int)Mathf.Min(a.y, b.y) - 2, 0, h - 1), y1 = Mathf.Clamp((int)Mathf.Max(a.y, b.y) + 2, 0, h - 1);
            var rt = new RenderTexture(w, h, 24);
            // the hidden-silhouette pass marks where she is but is not her: off while counting (the presenter's next frame restores its block)
            var block = new MaterialPropertyBlock();
            hero.GetPropertyBlock(block);
            Color tint = hero.sharedMaterial.GetColor("_OccludedTint");
            block.SetColor("_OccludedTint", new Color(tint.r, tint.g, tint.b, 0f));
            hero.SetPropertyBlock(block);
            int seen = HeroPixels(cam, hero, rt, x0, x1, y0, y1);
            var others = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r != hero).ToList();
            foreach (Renderer r in others) r.enabled = false;
            int open = HeroPixels(cam, hero, rt, x0, x1, y0, y1);
            foreach (Renderer r in others) r.enabled = true;
            rt.Release();
            Object.Destroy(rt);
            Assert.Greater(open, 200, "the caretaker covers enough pixels to measure");
            return seen / (float)open;
        }

        static int HeroPixels(Camera cam, Renderer hero, RenderTexture rt, int x0, int x1, int y0, int y1)
        {
            Color32[] with = Grab(cam, rt);
            hero.enabled = false;
            Color32[] without = Grab(cam, rt);
            hero.enabled = true;
            int changed = 0, w = rt.width;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Color32 p = with[y * w + x], q = without[y * w + x];
                    if (Mathf.Max(Mathf.Abs(p.r - q.r), Mathf.Abs(p.g - q.g), Mathf.Abs(p.b - q.b)) > 64) changed++;
                }
            return changed;
        }

        static Color32[] Grab(Camera cam, RenderTexture rt)
        {
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            Color32[] pixels = image.GetPixels32();
            Object.Destroy(image);
            return pixels;
        }

        [UnityTest] public IEnumerator SilentValleyLight_IsTheB5One_AndBeforeModeRestoresTheB4Light()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            yield return null;
            Assert.AreEqual(b5.B5Light.sun, lighting.MutedSun, "the B5 silent valley is lit grey-green, not blue");
            Assert.AreEqual(b5.B5Light.sun, lighting.Sun.color, "and the sun really has that colour before the Heart wakes");
            b5.Apply(true);
            yield return null;
            Assert.AreEqual(b5.BeforeLight.sun, lighting.MutedSun, "before mode: the B4 light");
            Assert.AreEqual(b5.BeforeLight.sun, lighting.Sun.color);
            b5.Apply(false);
            yield return null;
            Assert.AreEqual(b5.B5Light.sun, lighting.Sun.color);
        }

        [UnityTest] public IEnumerator Minimap_ShowsTheB5Bake_AndStillFollowsThePlayerAndTheObjective()
        {
            TestSaveFolder.Begin();
            yield return LoadB5();
            var minimap = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            Assert.IsTrue(minimap.isActiveAndEnabled);
            Assert.AreSame(b5.B5Minimap, minimap.Map.texture);
            yield return null;
            Assert.AreEqual(MinimapTargets.Kind.Mira, minimap.Objective);
            Vector2 p0 = minimap.PlayerMapPoint;
            Vector3 w0 = player.transform.position;
            player.InputOverride = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.6f);
            player.InputOverride = null;
            yield return null;
            Vector2 moved = minimap.PlayerMapPoint - p0, expected = minimap.WorldToMap(player.transform.position) - minimap.WorldToMap(w0);
            Assert.Greater(moved.magnitude, 2f, "the map scrolls as the player walks");
            Assert.Less((moved - expected).magnitude, 0.5f);
            yield return StandNextTo(npc);
            InteractAndReadAll(npc);
            yield return null;
            Assert.AreEqual(MinimapTargets.Current(Session), minimap.Objective, "the marker follows the objective panel");
        }
#endif
    }
}
