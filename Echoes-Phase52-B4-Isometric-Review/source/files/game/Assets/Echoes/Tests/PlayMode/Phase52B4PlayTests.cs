using System.Collections;
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
    /// Phase 5.2-B4: the high-angle pilot scene on the real player, controller, interaction, quest, save and minimap systems. Presentation
    /// changes must leave gameplay exactly as it was; these tests play it through the same real interactions as the original scene's tests.
    /// </summary>
    public class Phase52B4PlayTests : ValleyPlayTestBase
    {
#if UNITY_EDITOR
        const string B4Valley = "Assets/Echoes/Art/Phase52B4/Scenes/Valley.unity";

        Phase52B4Pilot b4;
        MinimapController minimap;
        FixedFollowCamera25D follow;

        IEnumerator LoadB4()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(B4Valley, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            FindSceneObjects();
            b4 = Object.FindFirstObjectByType<Phase52B4Pilot>();
            minimap = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            follow = Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Assert.IsNotNull(b4);
            Assert.IsFalse(b4.BeforeMode, "the B4 presentation is the default");
        }

        [UnityTest] public IEnumerator B4Scene_RestoresTheStreamThroughTheWorkshopAndTheSpring_AndContinueKeepsIt()
        {
            TestSaveFolder.Begin();
            yield return LoadB4();
            var variant = Object.FindFirstObjectByType<StageVariant25D>();
            Assert.IsNotNull(variant);
            yield return null;
            Assert.IsFalse(variant.RestoredShown, "the silent valley shows the dormant spring painting");
            Vector3 start = player.transform.position;
            player.InputOverride = Vector2.up;
            yield return new WaitForSeconds(0.6f);
            player.InputOverride = null;
            Assert.Greater(Vector3.Distance(start, player.transform.position), 1f, "the player walks in the B4 scene");

            yield return RestoreTheStream();   // exploration, Mira, two herbs and a blossom, the tonic at the workshop, the spring
            Assert.AreEqual(ResonanceStage.AwakeningStream, Session.Resonance);
            float waited = 0f;
            while ((resonanceView.IsTransitioning || resonanceView.ShownStage != ResonanceStage.AwakeningStream) && waited < 15f) { waited += Time.deltaTime; yield return null; }
            yield return null;
            Assert.AreEqual(ResonanceStage.AwakeningStream, resonanceView.ShownStage);
            Assert.IsTrue(variant.RestoredShown, "the restored spring painting replaces the dormant one");

            Assert.IsTrue(host.SaveNow(), "the B4 scene saves like the game");
            GameLaunch.Mode = LaunchMode.Continue;
            yield return LoadB4();
            Assert.AreEqual(StreamQuestStage.Completed, Session.StreamQuest, "Continue restores the quest in the B4 scene");
            Assert.AreEqual(ResonanceStage.AwakeningStream, Session.Resonance);
            yield return null;
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<StageVariant25D>().RestoredShown, "a continued game shows the restored spring at once");
        }

        [UnityTest] public IEnumerator Minimap_FollowsThePlayer_AndPointsAtTheCurrentObjective()
        {
            TestSaveFolder.Begin();
            yield return LoadB4();
            Assert.IsNotNull(minimap);
            Assert.IsTrue(minimap.isActiveAndEnabled, "the minimap is on screen in B4");
            yield return null;
            Assert.AreEqual(MinimapTargets.Kind.Mira, minimap.Objective, "a new game points at Mira, like the objective panel");
            Vector2 p0 = minimap.PlayerMapPoint;
            Assert.Less(minimap.MapToWindow(p0).magnitude, 0.5f, "the player sits in the middle of the map window");
            Vector3 w0 = player.transform.position;
            player.InputOverride = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.6f);
            player.InputOverride = null;
            yield return null;
            Vector3 w1 = player.transform.position;
            Vector2 moved = minimap.PlayerMapPoint - p0, expected = minimap.WorldToMap(w1) - minimap.WorldToMap(w0);
            Assert.Greater(moved.magnitude, 2f, "the map scrolls as the player walks");
            Assert.Less((moved - expected).magnitude, 0.5f, "by the player's own world movement");
            Assert.Less(minimap.MapToWindow(minimap.PlayerMapPoint).magnitude, 0.5f, "the player stays in the middle");
            // walking right on screen moves the player to the right on the turned map too
            Vector2 onScreen = (Vector2)(Quaternion.Euler(0f, 0f, minimap.MapYaw) * moved);
            Assert.Greater(onScreen.x, 0.8f * onScreen.magnitude, "the map is turned like the view: screen right is map right");

            yield return StandNextTo(npc);
            InteractAndReadAll(npc);
            yield return null;
            Assert.AreNotEqual(MinimapTargets.Kind.Mira, minimap.Objective, "the marker moves on with the objective");
            Assert.AreEqual(MinimapTargets.Current(Session), minimap.Objective);
            Vector2 target = minimap.MapToWindow(minimap.WorldToMap(spring.transform.position));
            Vector2 marker = minimap.ObjectiveWindowPosition;
            if (minimap.Objective == MinimapTargets.Kind.Spring)
                Assert.Less(Vector2.Angle(target, marker), 2f, "the marker points at the spring (clamped to the edge when it is far)");
        }

        [UnityTest] public IEnumerator BeforeAndAfter_SwitchOnlyThePresentation()
        {
            TestSaveFolder.Begin();
            yield return LoadB4();
            Vector3 at = player.transform.position;
            int coins = Session.Coins;
            var presenter = player.GetComponentInChildren<CharacterVisual2D5D>(true);
            b4.Apply(true);
            yield return null;
            Assert.AreEqual(30f, follow.Pitch, 1e-3f, "before: the B3 camera");
            Assert.AreSame(b4.BeforeSheet, presenter.Sheet, "before: the B3 drawings");
            Assert.IsFalse(minimap.gameObject.activeInHierarchy, "before: no minimap");
            Assert.IsTrue(System.Array.TrueForAll(b4.DisabledInB4, c => c.enabled), "before: the old house collision");
            b4.Apply(false);
            yield return null;
            Assert.AreEqual(48f, follow.Pitch, 1e-3f);
            Assert.AreEqual(45f, follow.Yaw, 1e-3f);
            Assert.AreSame(b4.B4Sheet, presenter.Sheet);
            Assert.IsTrue(minimap.gameObject.activeInHierarchy);
            Assert.Less(Vector3.Distance(at, player.transform.position), 0.05f, "switching never moves the player");
            Assert.AreEqual(coins, Session.Coins, "switching never touches the session");
        }

        [UnityTest] public IEnumerator HighCamera_StaysFixed_AndDoesNotJumpInBehindThePaintedWorkshop()
        {
            TestSaveFolder.Begin();
            yield return LoadB4();
            Transform cam = follow.transform;
            Terrain t = Terrain.activeTerrain;
            Vector3 behind = new Vector3(27.2f, 0f, 11.8f);
            behind.y = t.SampleHeight(behind) + t.transform.position.y + 0.05f;
            player.Teleport(behind);
            follow.Snap();
            yield return null;
            yield return null;
            Quaternion rotation = cam.rotation;
            float d = Vector3.Distance(cam.position, player.transform.position + Vector3.up * 0.9f);
            Assert.AreEqual(follow.Distance, d, 0.6f, "behind the workshop the camera keeps its distance (the roof does not pull it in)");
            player.InputOverride = new Vector2(-1f, 0f);
            yield return new WaitForSeconds(0.5f);
            player.InputOverride = null;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Quaternion.Angle(rotation, cam.rotation), 0.5f, "no orbit: the view direction never changes in play");
            Assert.AreEqual(48f, Vector3.Angle(Vector3.ProjectOnPlane(cam.forward, Vector3.up), cam.forward), 0.5f, "pitch 48");
        }
#endif
    }
}
