using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Echoes.EditorTools
{
    /// <summary>
    /// EDITOR PREVIEW of the B4 pilot scene (diagnostic only, never Windows-player evidence): renders the fixed gameplay camera at the
    /// workshop, the plaza, the willow and the spring at pitch 42/48/54 and in the B3 "before" presentation, plus occlusion checks
    /// (behind the workshop, behind the willow), to choose camera values and check the painted props before a player build.
    /// </summary>
    public static class Phase52B4Preview
    {
        static readonly (string name, Vector2 xz, float faceYaw)[] Spots =
        {
            ("workshop-front", new Vector2(20.4f, 7.4f), 90f),
            ("workshop-side", new Vector2(25.6f, 3.6f), 0f),
            ("plaza", new Vector2(4.0f, -3.5f), 225f),
            ("willow", new Vector2(12.6f, 8.6f), 45f),
            ("spring", new Vector2(-22.6f, 11.6f), 45f),
            ("behind-workshop", new Vector2(27.2f, 11.8f), 225f),
            ("behind-willow", new Vector2(15.6f, 12.6f), 225f),
        };

        public static void Capture()
        {
            string dir = Argument("-echoesPreviewDir");
            if (string.IsNullOrEmpty(dir)) throw new IOException("-echoesPreviewDir required");
            Directory.CreateDirectory(dir);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(Phase52B4Builder.Valley, OpenSceneMode.Single);
            var pilot = UnityEngine.Object.FindFirstObjectByType<Phase52Pilot>();
            pilot.Apply(false);
            UnityEngine.Object.FindFirstObjectByType<Phase52B3Pilot>().Apply(false);
            var b4 = UnityEngine.Object.FindFirstObjectByType<Phase52B4Pilot>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var presenter = player.GetComponentInChildren<CharacterVisual2D5D>(true);
            var follow = UnityEngine.Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Camera camera = follow.GetComponent<Camera>();
            var report = new List<string>();
            foreach (var mode in new[] { ("b4-p42", false, 42f), ("b4-p48", false, 48f), ("b4-p54", false, 54f), ("before-b3", true, 30f) })
            {
                b4.Apply(mode.Item2);
                if (!mode.Item2)
                {
                    var v = b4.B4View;
                    follow.Configure(v.yaw, mode.Item3, v.distance, v.minDistance, v.maxDistance, v.fieldOfView);
                }
                foreach (var s in Spots)
                {
                    Vector3 at = new Vector3(s.xz.x, 0f, s.xz.y);
                    at.y = pilot.Terrain.SampleHeight(at) + pilot.Terrain.transform.position.y + 0.02f;
                    player.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, s.faceYaw, 0f));
                    Physics.SyncTransforms();
                    follow.Snap();
                    presenter.Step(1f / 60f, camera);
                    presenter.Step(1f / 60f, camera);
                    foreach (var npc in UnityEngine.Object.FindObjectsByType<NpcVisual2D5D>(FindObjectsSortMode.None)) npc.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                    Shader.SetGlobalVector("_EchoesFocus", new Vector4(at.x, at.y + 1.05f, at.z, 1f));
                    Render(camera, Path.Combine(dir, mode.Item1 + "-" + s.name + ".png"));
                }
                report.Add(mode.Item1 + ": camera yaw " + follow.Yaw + " pitch " + follow.Pitch + " distance " + follow.Distance + " fov " + camera.fieldOfView + " height above pivot " + (follow.Distance * Mathf.Sin(follow.Pitch * Mathf.Deg2Rad)).ToString("F1"));
            }
            foreach (var prop in UnityEngine.Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.Add("prop " + prop.name + " anchor " + prop.transform.position.ToString("F2") + " frame " + prop.Frame.ToString("F2") + " base L " + prop.BaseLeft.ToString("F2") + " F " + prop.BaseFront.ToString("F2") + " R " + prop.BaseRight.ToString("F2"));
            File.WriteAllLines(Path.Combine(dir, "NOTE.txt"), new[] { "EDITOR_PREVIEW renders of the B4 pilot scene. Not Windows-player evidence." }.Concat(report));
        }

        static void Render(Camera camera, string file)
        {
            var rt = RenderTexture.GetTemporary(1920, 1080, 24);
            rt.antiAliasing = 2;
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }

}
