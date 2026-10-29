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
    /// Phase 5.2-B5 survey of a pilot scene (diagnostic only; the scene is opened and never saved): what the B4 view shows around the
    /// plaza, the workshop, the willow, the spring and the garden. Writes survey.txt (terrain layers and where each is used, every tree
    /// with its collider and whether it is drawn, the garden's pieces, the stream, the stage roots) and EDITOR_PREVIEW renders from the
    /// fixed gameplay camera, stages switched in edit mode the way PreviewTools does. Never Windows-player evidence.
    /// </summary>
    public static class Phase52B5Survey
    {
        static readonly (string name, Vector2 xz, float faceYaw, int stage)[] Spots =
        {
            ("garden-west-gate-S0", new Vector2(10.2f, -14.2f), 90f, 0),
            ("garden-west-gate-S2", new Vector2(10.2f, -14.2f), 90f, 2),
            ("garden-south-S0", new Vector2(15.0f, -19.5f), 0f, 0),
            ("plaza-east-S0", new Vector2(8.0f, -1.0f), 225f, 0),
            ("path-north-S0", new Vector2(1.5f, 17.0f), 0f, 0),
            ("path-garden-S0", new Vector2(8.6f, -8.2f), 135f, 0),
            ("spring-S1", new Vector2(-18.4f, 12.6f), 300f, 1),
            ("spring-S0", new Vector2(-18.4f, 12.6f), 300f, 0),
            ("windmill-approach-S0", new Vector2(3.6f, 22.0f), 0f, 0),
            ("hen-yard-S0", new Vector2(19.0f, -3.0f), 90f, 0),
            ("spring-S2", new Vector2(-18.4f, 12.6f), 300f, 2),
            ("spring-S3", new Vector2(-18.4f, 12.6f), 300f, 3),
            ("garden-west-gate-S3", new Vector2(10.2f, -14.2f), 90f, 3),
            ("behind-tree-S0", new Vector2(14.6f, 19.6f), 225f, 0),
            ("plaza-S0", new Vector2(4.0f, -3.5f), 225f, 0),
            ("workshop-front-S0", new Vector2(20.4f, 7.4f), 90f, 0),
            ("willow-S0", new Vector2(12.6f, 8.6f), 45f, 0),
            ("stream-S0", new Vector2(-24.6f, 6.4f), 300f, 0),
            ("stream-S1", new Vector2(-24.6f, 6.4f), 300f, 1),
            ("rocks-S0", new Vector2(-29.0f, 26.0f), 0f, 0),
        };

        static readonly (string name, Vector2 xz)[] Zones =
        {
            ("plaza", Vector2.zero), ("workshop", ValleyLayout.Cottage), ("willow", new Vector2(14.5f, 11f)), ("spring", ValleyLayout.Spring),
            ("garden", ValleyLayout.Garden), ("henyard", ValleyLayout.HenYard), ("windmill", ValleyLayout.Windmill),
        };

        public static void Run()
        {
            string dir = Argument("-echoesPreviewDir");
            string scenePath = Argument("-echoesScene") ?? Phase52B4Builder.Valley;
            if (string.IsNullOrEmpty(dir)) throw new IOException("-echoesPreviewDir required");
            Directory.CreateDirectory(dir);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var pilot = UnityEngine.Object.FindFirstObjectByType<Phase52Pilot>();
            pilot.Apply(false);
            UnityEngine.Object.FindFirstObjectByType<Phase52B3Pilot>().Apply(false);
            var b4 = UnityEngine.Object.FindFirstObjectByType<Phase52B4Pilot>();
            b4.Apply(false);
            var b5 = UnityEngine.Object.FindFirstObjectByType<Phase52B5Pilot>();
            if (b5 != null)
            {
                b5.Apply(Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b5-before") >= 0);
                b5.ApplyLighting(b5.BeforeMode);   // the scene is never saved, so the lighting's skybox copy is harmless here
            }
            string label = b5 == null ? "b4" : b5.BeforeMode ? "b5scene-before" : "b5";
            var lines = new List<string> { "EDITOR_PREVIEW survey of " + scenePath + " (opened, never saved). Not Windows-player evidence.", "" };
            Terrain terrain = pilot.Terrain;

            // ---- terrain layers and where each one dominates (alphamap texels with weight > 0.5, per zone within 12 m) ----
            TerrainData data = terrain.terrainData;
            float[,,] maps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            lines.Add("TERRAIN " + AssetDatabase.GetAssetPath(data) + " alphamap " + data.alphamapWidth + "x" + data.alphamapHeight + " size " + data.size + " origin " + terrain.transform.position);
            for (int l = 0; l < data.terrainLayers.Length; l++)
            {
                TerrainLayer layer = data.terrainLayers[l];
                int total = 0;
                var perZone = Zones.ToDictionary(z => z.name, z => 0);
                for (int z = 0; z < data.alphamapHeight; z++)
                    for (int x = 0; x < data.alphamapWidth; x++)
                    {
                        if (maps[z, x, l] < 0.5f) continue;
                        total++;
                        var p = new Vector2(terrain.transform.position.x + (x + 0.5f) / data.alphamapWidth * data.size.x, terrain.transform.position.z + (z + 0.5f) / data.alphamapHeight * data.size.z);
                        foreach (var zone in Zones) if (Vector2.Distance(p, zone.xz) < 12f) perZone[zone.name]++;
                    }
                lines.Add("  layer " + l + " " + (layer != null ? layer.name + " tile " + layer.tileSize + " tex " + (layer.diffuseTexture != null ? AssetDatabase.GetAssetPath(layer.diffuseTexture) : "-") : "null")
                    + " dominant texels " + total + " | " + string.Join(", ", perZone.Select(kv => kv.Key + " " + kv.Value)));
            }

            // ---- trees: every tree-like root, drawn or not, with its collider and its distance to the five places ----
            lines.Add("");
            lines.Add("TREES (root name, position, drawn renderers / all, collider, hidden by B4, nearest zone)");
            var hiddenB4 = new HashSet<Renderer>(b4.HiddenInB4);
            var replacedB2 = new HashSet<Renderer>(pilot.ReplacedTreeRenderers ?? Array.Empty<Renderer>());
            var roots = new List<Transform>();
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name;
                bool treeRoot = n.StartsWith("Tree_") || n.EndsWith("_Pilot") || (n == "Tree" && (t.parent == null || t.parent.name != "Tree_Round" && t.parent.name != "Tree_Tall" && t.parent.name != "Tree_Blossom" && !t.parent.name.StartsWith("Tree_")));
                if (treeRoot) roots.Add(t);
            }
            foreach (Transform t in roots.OrderBy(t => Nearest(t.position).d))
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                var col = t.GetComponent<Collider>();
                var (zone, d) = Nearest(t.position);
                lines.Add("  " + Path(t) + " at " + t.position.ToString("F1") + " active " + t.gameObject.activeInHierarchy + " drawn " + rs.Count(r => r.enabled) + "/" + rs.Length
                    + " collider " + (col == null ? "none" : col.GetType().Name + (col is CapsuleCollider c ? " r=" + c.radius.ToString("F2") + " h=" + c.height.ToString("F1") : "") + (col.enabled ? "" : " (disabled)"))
                    + " hiddenB4 " + rs.Count(hiddenB4.Contains) + " replacedB2 " + rs.Count(replacedB2.Contains) + " nearest " + zone + " " + d.ToString("F1") + " m");
            }

            // ---- the stage roots ResonanceView switches, the garden and the stream ----
            var view = UnityEngine.Object.FindFirstObjectByType<ResonanceView>();
            var so = new SerializedObject(view);
            lines.Add("");
            foreach (string field in new[] { "troubledStream", "clearStream", "bloomRoot", "gardenDormant", "gardenBloomRoot", "yardLifeRoot", "villageLifeRoot" })
            {
                var go = (GameObject)so.FindProperty(field).objectReferenceValue;
                if (go == null) { lines.Add("STAGE ROOT " + field + " = null"); continue; }
                lines.Add("STAGE ROOT " + field + " = " + Path(go.transform) + " children " + go.transform.childCount + " renderers " + go.GetComponentsInChildren<Renderer>(true).Length
                    + " materials " + string.Join(",", go.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial != null ? r.sharedMaterial.name : "-").Distinct().Take(8)));
                foreach (Transform child in go.transform)
                    if (field != "gardenDormant" && field != "gardenBloomRoot")
                        lines.Add("    " + child.name + " at " + child.position.ToString("F1") + " scale " + child.localScale.x.ToString("F2") + " nearest " + Nearest(child.position).zone + " " + Nearest(child.position).d.ToString("F1"));
            }
            Transform garden = GameObject.Find("Garden").transform;
            lines.Add("GARDEN " + Path(garden) + " at " + garden.position.ToString("F2") + " rotation " + garden.eulerAngles.ToString("F0"));
            foreach (Transform child in garden)
            {
                var rs = child.GetComponentsInChildren<Renderer>(true);
                var cs = child.GetComponentsInChildren<Collider>(true);
                lines.Add("  " + child.name + " local " + child.localPosition.ToString("F2") + " renderers " + rs.Length + " colliders " + cs.Length + " (" + string.Join(",", cs.Select(c => c.GetType().Name).Distinct()) + ")");
            }
            foreach (string name in new[] { "StreamBankDetails", "Water", "WindmillPilot", "DistantTreeLine", "Flowers", "Rocks", "Trees", "B4_GroundFoliage", "Phase52_RouteOnly" })
            {
                var go = GameObject.Find(name);
                lines.Add("GROUP " + name + (go == null ? " not found" : " children " + go.transform.childCount + " renderers " + go.GetComponentsInChildren<Renderer>(true).Length + " drawn " + go.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled)));
            }
            foreach (var node in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
                lines.Add("NODE " + node.NodeId + " at " + node.Position.ToString("F1") + " nearest " + Nearest(node.Position).zone + " " + Nearest(node.Position).d.ToString("F1"));

            // ---- EDITOR_PREVIEW renders from the B4 gameplay camera ----
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var presenter = player.GetComponentInChildren<CharacterVisual2D5D>(true);
            var follow = UnityEngine.Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Camera camera = follow.GetComponent<Camera>();
            var v4 = b4.B4View;
            follow.Configure(v4.yaw, v4.pitch, v4.distance, v4.minDistance, v4.maxDistance, v4.fieldOfView);
            foreach (var s in Spots)
            {
                ShowStage(so, s.stage);
                Vector3 at = new Vector3(s.xz.x, 0f, s.xz.y);
                at.y = terrain.SampleHeight(at) + terrain.transform.position.y + 0.02f;
                player.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, s.faceYaw, 0f));
                Physics.SyncTransforms();
                follow.Snap();
                presenter.Step(1f / 60f, camera);
                presenter.Step(1f / 60f, camera);
                foreach (var npc in UnityEngine.Object.FindObjectsByType<NpcVisual2D5D>(FindObjectsSortMode.None)) npc.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                Shader.SetGlobalVector("_EchoesFocus", new Vector4(at.x, at.y + 1.05f, at.z, 1f));
                foreach (var painting in UnityEngine.Object.FindObjectsByType<StagePainting25D>(FindObjectsSortMode.None)) painting.Refresh();
                Render(camera, System.IO.Path.Combine(dir, "editor-preview-" + label + "-" + s.name + ".png"));
            }
            File.WriteAllLines(System.IO.Path.Combine(dir, "survey.txt"), lines);
            Debug.Log("[Echoes][P52 B5] survey written to " + dir + " (" + lines.Count + " lines)");
        }

        static (string zone, float d) Nearest(Vector3 p)
        {
            var xz = new Vector2(p.x, p.z);
            var best = Zones.Select(z => (z.name, Vector2.Distance(xz, z.xz))).OrderBy(z => z.Item2).First();
            return (best.Item1, best.Item2);
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        /// <summary>The objects each stage switches on (the edit-mode stand-in PreviewTools uses; lighting via ValleyLighting.ApplyNow).</summary>
        internal static void ShowStage(SerializedObject so, int stage)
        {
            bool stream = stage >= 1, garden = stage >= 2, valley = stage >= 3;
            void Set(string field, bool on) { var go = (GameObject)so.FindProperty(field).objectReferenceValue; if (go != null) go.SetActive(on); }
            Set("troubledStream", !stream);
            Set("clearStream", stream);
            Set("bloomRoot", stream);
            Set("gardenDormant", !garden);
            Set("gardenBloomRoot", garden);
            Set("yardLifeRoot", garden);
            Set("villageLifeRoot", valley);
            var water = (Material)so.FindProperty(stream ? "clearWater" : "murkyWater").objectReferenceValue;
            var pond = (Renderer)so.FindProperty("pond").objectReferenceValue;
            if (pond != null) pond.sharedMaterial = water;
            var springWater = (Renderer)so.FindProperty("springWater").objectReferenceValue;
            if (springWater != null) { springWater.sharedMaterial = water; springWater.transform.localScale = so.FindProperty(stream ? "springWaterFull" : "springWaterDry").vector3Value; }
            foreach (string field in new[] { "windmillDamaged", "windmillRepaired" })
            {
                var list = so.FindProperty(field);
                for (int i = 0; i < list.arraySize; i++) ((GameObject)list.GetArrayElementAtIndex(i).objectReferenceValue).SetActive(field == "windmillRepaired" ? valley : !valley);
            }
            var variant = UnityEngine.Object.FindFirstObjectByType<StageVariant25D>();
            if (variant != null)
            {
                var vso = new SerializedObject(variant);
                ((GameObject)vso.FindProperty("before").objectReferenceValue).SetActive(!stream);
                ((GameObject)vso.FindProperty("after").objectReferenceValue).SetActive(stream);
            }
            var lighting = UnityEngine.Object.FindFirstObjectByType<ValleyLighting>();
            if (lighting != null) lighting.ApplyNow(stream ? 1f : 0f, stream ? 1f : 0f, garden ? 1f : 0f, valley ? 1f : 0f);
        }

        internal static void Render(Camera camera, string file)
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
