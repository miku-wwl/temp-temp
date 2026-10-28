using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Echoes.EditorTools
{
    /// <summary>
    /// Phase 5.2-B4 isolated pilot: a high fixed three-quarter camera, the owner-supplied B4 caretaker drawings, painted static props for the
    /// workshop, the willow and the spring, painted ground foliage, a cobbled plaza and a real minimap. Copies the B3 pilot scenes into
    /// Art/Phase52B4/Scenes and changes presentation only; gameplay objects, colliders, interactions and saves are the B3 ones. Everything B4
    /// adds is listed on Phase52B4Pilot, which can show the B3 presentation again in the same scene ("before" mode).
    /// </summary>
    public static class Phase52B4Builder
    {
        public const string Folder = "Assets/Echoes/Art/Phase52B4";
        public const string SceneFolder = Folder + "/Scenes";
        public const string Valley = SceneFolder + "/Valley.unity";
        public const string Menu = SceneFolder + "/MainMenu.unity";
        public const string HeroSheetPath = Folder + "/CaretakerB4Sheet.asset";
        public const string TerrainPath = Folder + "/B4Terrain.asset";
        public const string MinimapBasePath = Folder + "/UI/MinimapBase.png";
        public static readonly Vector4 MapBounds = new Vector4(-56f, -56f, 112f, 112f);

        // the fixed B4 view (yaw: the painted props were drawn with the workshop's facades at 45 degrees to the view)
        public const float Yaw = 45f, Pitch = 48f, Distance = 14f, MinDistance = 10f, MaxDistance = 17f, FieldOfView = 35f;

        /// <summary>Painted prop placement: metres per painting pixel and where the painted footprint's centre stands.</summary>
        public static readonly Dictionary<string, (float metresPerPixel, Vector2 centre, bool single)> Props = new Dictionary<string, (float, Vector2, bool)>
        {
            ["Cottage"] = (0.0068f, new Vector2(25.0f, 8.0f), false),
            ["Willow"] = (0.0060f, new Vector2(14.5f, 11.0f), true),
            ["Spring"] = (0.0050f, new Vector2(-21.0f, 16.0f), false),
        };

        [MenuItem("Echoes/Phase 5.2-B4/Generate high-angle 2.5D pilot")]
        public static void Generate()
        {
            AssetPaths.EnsureFolder(SceneFolder);
            Material heroMaterial = TextureMaterial("CaretakerB4Sprite", Folder + "/Sprites/CaretakerB4Atlas.png", "Echoes/Sprite25D", 4096, TextureImporterCompression.CompressedHQ, TextureWrapMode.Clamp);
            heroMaterial.SetFloat("_AmbientWeight", 0.60f);
            heroMaterial.SetFloat("_SunWeight", 0.70f);
            heroMaterial.SetFloat("_ShadowFloor", 0.45f);
            heroMaterial.SetFloat("_DepthPull", 0.45f);
            var propMaterials = new Dictionary<string, Material>();
            foreach (string name in new[] { "Cottage", "Willow", "Spring", "SpringDormant" })
                propMaterials[name] = TextureMaterial("Prop_" + name, Folder + "/Props/" + name + ".png", "Echoes/StaticProp25D", 2048, TextureImporterCompression.CompressedHQ, TextureWrapMode.Clamp);
            Material foliageMaterial = TextureMaterial("GroundFoliage", Folder + "/Props/Foliage.png", "Echoes/Foliage25D", 2048, TextureImporterCompression.CompressedHQ, TextureWrapMode.Clamp);
            SpriteSheet25D heroSheet = HeroSheet(heroMaterial.mainTexture as Texture2D);
            TerrainLayer cobble = CobbleLayer();
            foreach (string icon in new[] { "arrow", "ring", "edge", "dot" }) UiSprite(Folder + "/UI/Icon_" + icon + ".png");
            UiSprite(Folder + "/UI/MinimapFrame.png");
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(Phase52B3Builder.Valley, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, Valley, true)) throw new IOException("Could not copy the B3 pilot Valley.");
            scene = EditorSceneManager.OpenScene(Valley, OpenSceneMode.Single);
            // opening a scene unloads unreferenced in-memory assets: load everything again from disk
            heroMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/CaretakerB4Sprite.mat");
            foreach (string name in propMaterials.Keys.ToArray()) propMaterials[name] = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Prop_" + name + ".mat");
            foliageMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/GroundFoliage.mat");
            heroSheet = AssetDatabase.LoadAssetAtPath<SpriteSheet25D>(HeroSheetPath);
            cobble = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Folder + "/PlazaCobbleB4.terrainlayer");

            var pilot = UnityEngine.Object.FindFirstObjectByType<Phase52Pilot>();
            var b3 = UnityEngine.Object.FindFirstObjectByType<Phase52B3Pilot>();
            var b4 = pilot.gameObject.AddComponent<Phase52B4Pilot>();
            var b4Only = new List<GameObject>();
            var hidden = new List<Renderer>();
            var shadowOnly = new List<Renderer>();
            Terrain terrain = pilot.Terrain;

            // ---- terrain: the B2 pilot terrain plus a cobbled plaza ----
            b4.Terrain = terrain;
            b4.BeforeTerrain = pilot.PilotTerrain;
            b4.B4Terrain = PlazaTerrain(pilot.PilotTerrain, cobble, terrain.transform.position);
            terrain.terrainData = b4.B4Terrain;
            terrain.GetComponent<TerrainCollider>().terrainData = b4.B4Terrain;

            // ---- caretaker: the B4 drawings on the unchanged presenter ----
            var presenter = b3.CharacterVisual.GetComponent<CharacterVisual2D5D>();
            var card = presenter.GetComponentInChildren<Sprite25DQuad>(true);
            var so = new SerializedObject(presenter);
            b4.BeforeSheet = (SpriteSheet25D)so.FindProperty("sheet").objectReferenceValue;
            b4.B4Sheet = heroSheet;
            Wire.Set(presenter, "sheet", heroSheet);
            b4.Hero = presenter;
            b4.HeroCard = card.GetComponent<MeshRenderer>();
            b4.BeforeHeroMaterial = b4.HeroCard.sharedMaterial;
            b4.B4HeroMaterial = heroMaterial;
            b4.HeroCard.sharedMaterial = heroMaterial;

            // ---- camera ----
            var follow = UnityEngine.Object.FindFirstObjectByType<FixedFollowCamera25D>();
            Wire.Set(follow, "yaw", Yaw);
            Wire.Set(follow, "pitch", Pitch);
            Wire.Set(follow, "distance", Distance);
            Wire.Set(follow, "minDistance", MinDistance);
            Wire.Set(follow, "maxDistance", MaxDistance);
            Wire.Set(follow, "fieldOfView", FieldOfView);
            Wire.Set(follow, "maxOrbitDegrees", 10f);
            Wire.Set(follow, "terrainOnlyCollision", true);
            b4.FollowCamera = follow;
            b4.B4View = new Phase52B4Pilot.CameraView { yaw = Yaw, pitch = Pitch, distance = Distance, minDistance = MinDistance, maxDistance = MaxDistance, fieldOfView = FieldOfView };

            // ---- painted static props ----
            var root = new GameObject("B4_StaticProps");
            b4Only.Add(root);
            Transform cottage = GameObject.Find("WorkshopCottage").transform;
            foreach (Renderer r in cottage.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (r is MeshRenderer && HouseBody.Contains(r.name)) shadowOnly.Add(r);   // the house body keeps its real shadow on the ground
                else hidden.Add(r);
            }
            var cottageProp = AddProp(root.transform, "Cottage", propMaterials["Cottage"], terrain);
            MoveChimneySmoke(cottage, cottageProp);
            var oldColliders = cottage.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
            var footprint = AddFootprintCollider(cottageProp, cottage);
            foreach (Collider c in oldColliders) c.enabled = false;
            b4.DisabledInB4 = oldColliders;
            b4.B4Colliders = new[] { footprint };

            Transform willowMesh = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Willow_Pilot")).OrderBy(r => Vector2.Distance(new Vector2(r.transform.position.x, r.transform.position.z), Props["Willow"].centre)).First().transform;
            hidden.Add(willowMesh.GetComponent<Renderer>());
            var trunk = willowMesh.GetComponent<CapsuleCollider>();
            if (trunk != null) { trunk.radius = 0.5f; trunk.height = 3f; trunk.center = new Vector3(0f, 1.5f, 0f); }
            AddProp(root.transform, "Willow", propMaterials["Willow"], terrain);

            Transform spring = GameObject.Find("RuinedSpring").transform;
            foreach (Renderer r in spring.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) hidden.Add(r);
            var springRestored = AddProp(root.transform, "Spring", propMaterials["Spring"], terrain);
            var springDormant = AddProp(root.transform, "Spring", propMaterials["SpringDormant"], terrain, "SpringDormant");
            var variant = root.AddComponent<StageVariant25D>();
            Wire.Set(variant, "view", UnityEngine.Object.FindFirstObjectByType<ResonanceView>());
            Wire.Set(variant, "before", springDormant.gameObject);
            Wire.Set(variant, "after", springRestored.gameObject);

            // ---- plaza: the Heart's primitive stones take the painted fieldstone (the crystal and its light stay) ----
            Material plazaStone = StoneMaterial("PlazaStoneB4", new Color(1.04f, 1.0f, 0.93f), new Color(1.08f, 1.04f, 0.94f), 1.6f);
            Material plazaStoneDark = StoneMaterial("PlazaStoneDarkB4", new Color(0.80f, 0.78f, 0.74f), new Color(0.92f, 0.90f, 0.84f), 1.2f);
            Material plazaDais = StoneMaterial("PlazaDaisB4", new Color(1.10f, 1.06f, 0.98f), new Color(1.16f, 1.11f, 1.0f), 2.6f);
            Material plazaDaisInner = StoneMaterial("PlazaDaisInnerB4", new Color(0.94f, 0.91f, 0.86f), new Color(1.0f, 0.96f, 0.9f), 2.2f);
            Transform heart = GameObject.Find("HeartOfTheValley").transform;
            var restyled = new List<Renderer>();
            var restyleBefore = new List<Material>();
            var restyleB4 = new List<Material>();
            foreach (MeshRenderer r in heart.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Moss") { hidden.Add(r); continue; }
                if (!PlazaStones.Any(prefix => r.name.StartsWith(prefix))) continue;
                bool dark = r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("StoneDark");
                restyled.Add(r);
                restyleBefore.Add(r.sharedMaterial);
                restyleB4.Add(r.name == "DaisOuter" ? plazaDais : r.name == "DaisInner" ? plazaDaisInner : dark ? plazaStoneDark : plazaStone);
                r.sharedMaterial = restyleB4[restyleB4.Count - 1];
            }
            b4.RestyledInB4 = restyled.ToArray();
            b4.RestyleBefore = restyleBefore.ToArray();
            b4.RestyleB4 = restyleB4.ToArray();

            // ---- ground foliage (and the B2 low-poly plants it replaces in these places) ----
            var foliage = Foliage(foliageMaterial, terrain, new[] { cottageProp, springRestored });
            b4Only.Add(foliage);
            foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var xz = new Vector2(r.transform.position.x, r.transform.position.z);
                if (r.name.StartsWith("Plant") && r.transform.parent != null && r.transform.parent.name != foliage.name && (InFoliageZone(xz) || xz.magnitude < 9.6f))
                    hidden.Add(r);
            }
            // what else stands on the plaza (diagnostic: the low-poly pieces the high camera now looks down on)
            var onPlaza = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && !hidden.Contains(r) && !r.transform.IsChildOf(heart) && new Vector2(r.transform.position.x, r.transform.position.z).magnitude < 9.6f && r.bounds.size.y < 2.5f)
                .Select(r => r.name + "<" + (r.transform.parent != null ? r.transform.parent.name : "-")).Distinct().Take(40);
            Debug.Log("[Echoes][P52 B4] other renderers on the plaza: " + string.Join(", ", onPlaza));

            // ---- minimap and HUD ----
            var hud = UnityEngine.Object.FindFirstObjectByType<HudController>();
            var minimap = Minimap(hud.transform, terrain);
            b4Only.Add(minimap);
            var status = hud.transform.Find("StatusPanel") as RectTransform;
            if (status == null) status = GameObject.Find("StatusPanel").GetComponent<RectTransform>();
            b4.StatusPanel = status;
            b4.BeforeStatusPosition = status.anchoredPosition;
            b4.B4StatusPosition = new Vector2(-24f - 232f - 12f, -24f);
            status.anchoredPosition = b4.B4StatusPosition;

            b4.B4Only = b4Only.ToArray();
            b4.HiddenInB4 = hidden.Distinct().ToArray();
            b4.ShadowOnlyInB4 = shadowOnly.Distinct().ToArray();
            foreach (Renderer r in b4.HiddenInB4) r.enabled = false;
            foreach (Renderer r in b4.ShadowOnlyInB4) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            BakeMinimap(b4);
            var raw = minimap.GetComponentsInChildren<RawImage>(true).First();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MinimapBasePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(Phase52B3Builder.Menu, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, Menu, true)) throw new IOException("Could not copy the B3 pilot menu.");
            AssetDatabase.SaveAssets();
            Debug.Log("[Echoes][P52 B4] high-angle 2.5D pilot generated: " + Valley + "; B4 adds " + b4.B4Only.Length + " roots, hides " + b4.HiddenInB4.Length + " renderers, "
                + b4.ShadowOnlyInB4.Length + " shadow-only; original, B2 and B3 scenes untouched.");
        }

        // ------------------------------------------------------------------------------------------------ camera basis

        public static Vector3 CameraRight => Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.right;
        public static Vector3 CameraUp => Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.up;
        public static Vector3 CameraForward => Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward;

        /// <summary>Where the ground under a point of the camera-facing card plane is (along the fixed view direction).</summary>
        public static Vector3 GroundUnder(Vector3 cardPoint, float groundY)
        {
            Vector3 f = CameraForward;
            float t = (groundY - cardPoint.y) / f.y;
            return cardPoint + f * t;
        }

        // ------------------------------------------------------------------------------------------------ props

        [Serializable] sealed class PropInfo { public int[] size; public int[] bottomProfile; public int bottomStep; }
        [Serializable] sealed class PropTable { public PropInfo Cottage, Willow, Spring; }

        static PropInfo Info(string name)
        {
            var table = JsonUtility.FromJson<PropTable>(File.ReadAllText(Folder + "/Props/Props.json"));
            return name == "Cottage" ? table.Cottage : name == "Willow" ? table.Willow : table.Spring;
        }

        /// <summary>Ground lines checked by eye on the cleaned paintings (crop pixels, y down); the lower envelope alone picks a hanging
        /// lantern on the workshop's right and a root tip on the willow.</summary>
        static readonly Dictionary<string, Vector2[]> GroundOverride = new Dictionary<string, Vector2[]>
        {
            ["Cottage"] = new[] { new Vector2(8f, 708f), new Vector2(608f, 991f), new Vector2(1112f, 861f) },
            ["Willow"] = new[] { new Vector2(790f, 1184f) },
        };

        /// <summary>
        /// The painting's ground line: left end, front corner (the lowest point), right end, in image pixels (y down).
        /// For a single-point prop (a tree) all three are the lowest point near the middle (the trunk's foot).
        /// </summary>
        static (Vector2 left, Vector2 front, Vector2 right) GroundLine(string name, PropInfo info, bool single)
        {
            if (GroundOverride.TryGetValue(name, out Vector2[] o)) return o.Length == 1 ? (o[0], o[0], o[0]) : (o[0], o[1], o[2]);
            int[] p = info.bottomProfile;
            int step = info.bottomStep;
            var columns = Enumerable.Range(0, p.Length).Where(i => p[i] >= 0).ToArray();
            int first = columns.First(), last = columns.Last();
            int front = single ? columns.Where(i => i > p.Length * 0.35f && i < p.Length * 0.65f).OrderByDescending(i => p[i]).First()
                               : columns.Where(i => i > p.Length * 0.25f && i < p.Length * 0.80f).OrderByDescending(i => p[i]).First();
            Vector2 F = new Vector2(front * step, p[front]);
            if (single) return (F, F, F);
            return (new Vector2(first * step, p[first]), F, new Vector2(last * step, p[last]));
        }

        static StaticProp25D AddProp(Transform parent, string name, Material material, Terrain terrain, string objectName = null)
        {
            var (metres, centre, single) = Props[name];
            PropInfo info = Info(name);
            float W = info.size[0], H = info.size[1];
            var (L, F, R) = GroundLine(name, info, single);
            Vector3 right = CameraRight, up = CameraUp;
            // card offset of an image point relative to the front point F, and the ground it stands on
            Vector3 Offset(Vector2 px) => right * ((px.x - F.x) * metres) + up * ((F.y - px.y) * metres);
            Vector3 GroundOffset(Vector3 d) { Vector3 g = GroundUnder(d, 0f); g.y = 0f; return g; }
            // place F so that the painted footprint's centre ((L + R) / 2, or the trunk) stands on the requested centre
            Vector2 middle = single ? F : (L + R) * 0.5f;
            Vector3 centreGround = GroundOffset(Offset(middle));
            Vector3 anchor = new Vector3(centre.x, 0f, centre.y) - centreGround;
            anchor.y = Height(terrain, anchor);
            Vector3 Ground(Vector2 px)
            {
                Vector3 g = anchor + GroundOffset(Offset(px));
                g.y = Height(terrain, g);
                return g;
            }
            var go = new GameObject(objectName ?? name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = anchor;
            go.GetComponent<MeshFilter>().sharedMesh = PropCardMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var prop = go.AddComponent<StaticProp25D>();
            prop.Configure(new Vector4(-F.x * metres, -(H - F.y) * metres, W * metres, H * metres), Ground(L), Ground(F), Ground(R), new Vector3(L.x / W, F.x / W, R.x / W), single);
            return prop;
        }

        /// <summary>The old house's body, which stands where the painted workshop stands and casts its shadow (the yard props do not).</summary>
        static readonly HashSet<string> HouseBody = new HashSet<string> { "Foundation", "Walls", "GableCore", "RoofLeft", "RoofRight", "RidgeCap", "Chimney", "CornerPost" };

        static readonly string[] PlazaStones = { "DaisOuter", "DaisInner", "ObeliskBase", "ObeliskShaft", "ObeliskBreak", "FallenBlock", "Pillar", "FallenDrum", "Rubble" };

        /// <summary>The plaza fieldstone: the seamless painted cobble (from mossy_stone_tile.png) on the triplanar stone shader.</summary>
        static Material StoneMaterial(string name, Color tint, Color topTint, float metresPerTile)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Echoes/TriplanarStone")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Echoes/TriplanarStone");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Terrain/PlazaCobble.png"));
            material.SetColor("_BaseColor", tint);
            material.SetColor("_TopColor", topTint);
            material.SetFloat("_MetresPerTile", metresPerTile);
            EditorUtility.SetDirty(material);
            return material;
        }

        static float Height(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static Mesh PropCardMesh()
        {
            string path = Folder + "/PropCard.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "PropCard" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1, 1, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(new Vector3(0f, 4f, 0f), new Vector3(24f, 24f, 24f));   // the shader places the corners up to ~10 m away
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>The chimney smoke rises from the painted chimney (a fixed point of the painting) instead of the hidden 3D one.</summary>
        static void MoveChimneySmoke(Transform cottage, StaticProp25D prop)
        {
            Transform smoke = cottage.Find("ChimneySmoke");
            if (smoke == null) return;
            var (metres, _, _) = Props["Cottage"];
            PropInfo info = Info("Cottage");
            Vector2 chimney = new Vector2(650f, 45f);   // the painted chimney's top in the cropped painting
            Vector4 f = prop.Frame;
            Vector3 local = CameraRight * (f.x + chimney.x * metres) + CameraUp * (f.y + (info.size[1] - chimney.y) * metres);
            smoke.position = prop.transform.position + local;
        }

        /// <summary>
        /// The painted workshop's footprint (the parallelogram on its ground line, inset 0.35 m, 3 m tall) becomes its collider in B4, so the
        /// player can walk right up to the painted counters and door; the old box house's colliders stand in front of the painted facade.
        /// A convex prism follows the painting's skewed footprint exactly, where a box would leave its corners open.
        /// </summary>
        static Collider AddFootprintCollider(StaticProp25D prop, Transform cottage)
        {
            Vector3 F = prop.BaseFront, L = prop.BaseLeft, R = prop.BaseRight;
            Vector3 B = L + R - F;
            Vector3 centre = (L + R) * 0.5f;
            var go = new GameObject("PaintedFootprintCollider_B4");
            go.transform.SetParent(cottage, true);
            go.transform.SetPositionAndRotation(new Vector3(centre.x, Mathf.Min(F.y, L.y, R.y, B.y) - 0.2f, centre.z), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var corners = new List<Vector3>();
            foreach (Vector3 c in new[] { F, R, B, L })
            {
                Vector3 local = c - centre;
                local.y = 0f;
                local -= local.normalized * 0.35f * Mathf.Sqrt(2f);   // inset towards the centre
                corners.Add(local);
            }
            var verts = corners.Concat(corners.Select(c => c + Vector3.up * 3.2f)).ToArray();
            int[] tris =
            {
                0, 2, 1, 0, 3, 2,       // bottom
                4, 5, 6, 4, 6, 7,       // top
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7,
            };
            string path = Folder + "/WorkshopFootprintB4.asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "WorkshopFootprintB4", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            return collider;
        }

        // ------------------------------------------------------------------------------------------------ foliage

        static readonly Vector2 PlazaCentre = Vector2.zero;
        static bool InFoliageZone(Vector2 p)
        {
            float plaza = Vector2.Distance(p, PlazaCentre);
            return (plaza > 9.6f && plaza < 15.5f)
                || Vector2.Distance(p, ValleyLayout.Cottage) < 11f
                || Vector2.Distance(p, Props["Willow"].centre) < 6.5f
                || Vector2.Distance(p, ValleyLayout.Spring) < 8.5f
                || ValleyLayout.DistanceToPaths(p) < 4.5f && p.x > 5f && p.x < 22f && p.y > -2f && p.y < 10f;
        }

        [Serializable] sealed class FoliageRect { public int x, y, w, h; }
        [Serializable] sealed class FoliageItems { public FoliageRect grass, flowers, fern; }
        [Serializable] sealed class FoliageAtlas { public int[] atlas; public FoliageItems items; }

        static GameObject Foliage(Material material, Terrain terrain, StaticProp25D[] footprints)
        {
            var atlas = JsonUtility.FromJson<FoliageAtlas>(File.ReadAllText(Folder + "/Props/Foliage.json"));
            var kinds = new[] { atlas.items.grass, atlas.items.flowers, atlas.items.fern };
            var interactables = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Select(i => new Vector2(i.Position.x, i.Position.z)).ToArray();
            var rng = new System.Random(5204);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var corners = new List<Vector2>();
            var tris = new List<int>();
            int count = 0;
            for (int attempt = 0; attempt < 9000 && count < 900; attempt++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 80 - 36), (float)(rng.NextDouble() * 70 - 30));
                if (!InFoliageZone(p)) continue;
                if (ValleyLayout.DistanceToPaths(p) < 1.5f || ValleyLayout.NearGameplaySpot(p, 1.6f)) continue;
                if (interactables.Any(i => Vector2.Distance(i, p) < 1.8f)) continue;
                if (footprints.Any(f => InsideFootprint(f, p, 0.4f))) continue;
                if (Vector2.Distance(p, Props["Willow"].centre) < 1.1f) continue;
                int kind = rng.NextDouble() < 0.5 ? 0 : rng.NextDouble() < 0.6 ? 1 : 2;
                FoliageRect r = kinds[kind];
                float height = kind == 1 ? 0.42f + (float)rng.NextDouble() * 0.2f : 0.5f + (float)rng.NextDouble() * 0.35f;
                float width = height * r.w / r.h;
                bool flip = rng.NextDouble() < 0.5;
                Vector3 ground = new Vector3(p.x, 0f, p.y);
                ground.y = Height(terrain, ground);
                float u0 = (float)r.x / atlas.atlas[0], u1 = (float)(r.x + r.w) / atlas.atlas[0], v0 = (float)r.y / atlas.atlas[1], v1 = (float)(r.y + r.h) / atlas.atlas[1];
                if (flip) (u0, u1) = (u1, u0);
                int b = verts.Count;
                for (int c = 0; c < 4; c++) verts.Add(ground);
                uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v0)); uvs.Add(new Vector2(u0, v1)); uvs.Add(new Vector2(u1, v1));
                corners.Add(new Vector2(-width / 2, -0.04f)); corners.Add(new Vector2(width / 2, -0.04f)); corners.Add(new Vector2(-width / 2, height)); corners.Add(new Vector2(width / 2, height));
                tris.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
                count++;
            }
            var mesh = new Mesh { name = "GroundFoliageB4", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, corners);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(3f);
            mesh.bounds = bounds;
            string path = Folder + "/GroundFoliageB4.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("B4_GroundFoliage", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Debug.Log("[Echoes][P52 B4] ground foliage: " + count + " painted clumps");
            return go;
        }

        static bool InsideFootprint(StaticProp25D prop, Vector2 p, float margin)
        {
            Vector2 F = new Vector2(prop.BaseFront.x, prop.BaseFront.z), L = new Vector2(prop.BaseLeft.x, prop.BaseLeft.z), R = new Vector2(prop.BaseRight.x, prop.BaseRight.z);
            Vector2 a = L - F, b = R - F, d = p - F;
            float det = a.x * b.y - a.y * b.x;
            if (Mathf.Abs(det) < 1e-4f) return Vector2.Distance(p, F) < margin;
            float s = (d.x * b.y - d.y * b.x) / det, t = (a.x * d.y - a.y * d.x) / det;
            float ma = margin / Mathf.Max(0.1f, a.magnitude), mb = margin / Mathf.Max(0.1f, b.magnitude);
            return s > -ma && s < 1 + ma && t > -mb && t < 1 + mb;
        }

        // ------------------------------------------------------------------------------------------------ terrain

        static TerrainLayer CobbleLayer()
        {
            string texPath = Folder + "/Terrain/PlazaCobble.png";
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;   // RGB only: URP reads a diffuse alpha as smoothness (the B2 ground-glint lesson)
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.DXT1;
            standalone.maxTextureSize = 1024;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
            string path = Folder + "/PlazaCobbleB4.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.tileSize = new Vector2(3.4f, 3.4f);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>A copy of the B2 pilot terrain (heights untouched) whose plaza stone becomes the painted cobble; cliffs keep their stone.</summary>
        static TerrainData PlazaTerrain(TerrainData source, TerrainLayer cobble, Vector3 origin)
        {
            AssetDatabase.DeleteAsset(TerrainPath);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), TerrainPath)) throw new IOException("Could not copy the pilot terrain.");
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            var layers = data.terrainLayers.ToList();
            int stone = layers.FindIndex(l => l != null && l.name == "Stone");
            int w = data.alphamapWidth, h = data.alphamapHeight, n = layers.Count;
            float[,,] old = data.GetAlphamaps(0, 0, w, h);
            layers.Add(cobble);
            data.terrainLayers = layers.ToArray();
            var map = new float[h, w, n + 1];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    for (int l = 0; l < n; l++) map[z, x, l] = old[z, x, l];
                    float wx = origin.x + (x + 0.5f) / w * data.size.x, wz = origin.z + (z + 0.5f) / h * data.size.z;
                    float d = new Vector2(wx, wz).magnitude;
                    float plaza = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9.2f, 10.6f, d));
                    if (stone >= 0 && plaza > 0f)
                    {
                        float moved = map[z, x, stone] * plaza;
                        map[z, x, stone] -= moved;
                        map[z, x, n] += moved;
                    }
                }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        // ------------------------------------------------------------------------------------------------ minimap

        [Serializable] sealed class FrameInfo { public int size; public float windowFraction; }

        static GameObject Minimap(Transform hud, Terrain terrain)
        {
            const float frameSize = 232f;
            var info = JsonUtility.FromJson<FrameInfo>(File.ReadAllText(Folder + "/UI/MinimapFrame.json"));
            var pause = hud.Find("PauseMenu");
            var rootRect = UiKit.NewRect("Minimap_B4", hud).Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-24f, -24f), new Vector2(frameSize, frameSize));
            if (pause != null) rootRect.SetSiblingIndex(pause.GetSiblingIndex());   // under the pause menu
            var window = UiKit.NewRect("Window", rootRect).Place(UiKit.Center, UiKit.Center, Vector2.zero, Vector2.one * frameSize * info.windowFraction);
            window.gameObject.AddComponent<RectMask2D>();
            var backdrop = window.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.16f, 0.20f, 0.14f, 1f);
            backdrop.raycastTarget = false;
            var mapRect = UiKit.NewRect("Map", window).Place(UiKit.Center, UiKit.Center, Vector2.zero, new Vector2(400f, 400f));
            var raw = mapRect.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var landmarks = new List<RectTransform>();
            var positions = new List<Vector3>();
            void Landmark(string name, Vector3 world, Color colour, float size)
            {
                var r = UiKit.NewRect(name, mapRect).Place(UiKit.Center, UiKit.Center, Vector2.zero, Vector2.one * size);
                var img = r.gameObject.AddComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/UI/Icon_dot.png");
                img.color = colour;
                img.raycastTarget = false;
                landmarks.Add(r);
                positions.Add(world);
            }
            var view = UnityEngine.Object.FindFirstObjectByType<ResonanceView>();
            Landmark("Workshop", new Vector3(ValleyLayout.Cottage.x, 0, ValleyLayout.Cottage.y), new Color(1f, 0.72f, 0.30f), 16f);
            Landmark("Spring", new Vector3(ValleyLayout.Spring.x, 0, ValleyLayout.Spring.y), new Color(0.45f, 0.86f, 1f), 16f);
            Landmark("Windmill", new Vector3(ValleyLayout.Windmill.x, 0, ValleyLayout.Windmill.y), new Color(0.96f, 0.94f, 0.88f), 16f);
            Landmark("Garden", new Vector3(ValleyLayout.Garden.x, 0, ValleyLayout.Garden.y), new Color(0.55f, 0.92f, 0.45f), 16f);
            Landmark("Heart", Vector3.zero, new Color(1f, 0.90f, 0.55f), 13f);
            var arrow = UiKit.NewRect("PlayerArrow", window).Place(UiKit.Center, UiKit.Center, Vector2.zero, new Vector2(22f, 22f));
            var arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/UI/Icon_arrow.png");
            arrowImage.raycastTarget = false;
            var objective = UiKit.NewRect("Objective", window).Place(UiKit.Center, UiKit.Center, Vector2.zero, new Vector2(24f, 24f));
            var objectiveImage = objective.gameObject.AddComponent<Image>();
            objectiveImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/UI/Icon_ring.png");
            objectiveImage.raycastTarget = false;
            var edge = UiKit.NewRect("EdgeArrow", objective).Place(UiKit.Center, UiKit.Center, Vector2.zero, new Vector2(12f, 12f));
            var edgeImage = edge.gameObject.AddComponent<Image>();
            edgeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/UI/Icon_edge.png");
            edgeImage.raycastTarget = false;
            edge.pivot = new Vector2(0.5f, -0.9f);
            var frame = UiKit.NewRect("Frame", rootRect).Place(UiKit.Center, UiKit.Center, Vector2.zero, Vector2.one * frameSize);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/UI/MinimapFrame.png");
            frameImage.raycastTarget = false;
            var north = UiKit.Label(rootRect, "North", "N", 20, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            // north is up on the baked map, which is turned by the camera yaw: the N mark sits on the frame where world north points
            float a = (90f + Yaw) * Mathf.Deg2Rad;
            north.rectTransform.Place(UiKit.Center, UiKit.Center, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * frameSize * 0.43f, new Vector2(26f, 26f));

            var controller = rootRect.gameObject.AddComponent<MinimapController>();
            Wire.Set(controller, "window", window);
            Wire.Set(controller, "map", raw);
            Wire.Set(controller, "playerArrow", arrow);
            Wire.Set(controller, "objectiveMarker", objective);
            Wire.Set(controller, "objectiveEdgeArrow", edge);
            Wire.Set(controller, "landmarkIcons", landmarks.Cast<UnityEngine.Object>().ToArray());
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("landmarkWorld");
            prop.arraySize = positions.Count;
            for (int i = 0; i < positions.Count; i++) prop.GetArrayElementAtIndex(i).vector3Value = positions[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Wire.Set(controller, "worldBounds", MapBounds);
            Wire.Set(controller, "metresAcross", 46f);
            Wire.Set(controller, "mapYaw", Yaw);
            Wire.Set(controller, "player", UnityEngine.Object.FindFirstObjectByType<PlayerController>().transform);
            Wire.Set(controller, "mira", UnityEngine.Object.FindFirstObjectByType<NpcInteractable>().transform);
            Wire.Set(controller, "spring", UnityEngine.Object.FindFirstObjectByType<SpringInteractable>().transform);
            Wire.Set(controller, "heart", UnityEngine.Object.FindFirstObjectByType<HeartInteractable>().transform);
            Wire.Set(controller, "workshop", UnityEngine.Object.FindFirstObjectByType<WorkshopInteractable>().transform);
            Wire.Set(controller, "wren", UnityEngine.Object.FindFirstObjectByType<GardenKeeperInteractable>().transform);
            Wire.Set(controller, "hen", UnityEngine.Object.FindFirstObjectByType<ChickenInteractable>().transform);
            Wire.Set(controller, "alder", UnityEngine.Object.FindFirstObjectByType<WindmillRestorerInteractable>().transform);
            return rootRect.gameObject;
        }

        /// <summary>
        /// Renders the valley straight down (orthographic, north up) with its real terrain, water, trees and 3D buildings, and keeps it as the
        /// minimap image. Painted cards, characters, particles and UI are hidden while it renders (they face the game camera, not the sky).
        /// </summary>
        static void BakeMinimap(Phase52B4Pilot b4)
        {
            var off = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                bool card = r.GetComponent<StaticProp25D>() != null || r.GetComponent<Sprite25DQuad>() != null || r.name == "B4_GroundFoliage" || r is ParticleSystemRenderer
                    || r.sharedMaterial != null && r.sharedMaterial.shader != null && (r.sharedMaterial.shader.name == "Echoes/ContactShadow");
                if (card && r.enabled) { r.enabled = false; off.Add(r); }
            }
            // the drifting clouds float over the valley and the fog would grey the picture: neither belongs on a map
            GameObject clouds = GameObject.Find("Clouds");
            if (clouds != null)
                foreach (Renderer r in clouds.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; off.Add(r); }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var restored = new List<Renderer>();
            foreach (Renderer r in b4.HiddenInB4) if (r != null && !r.enabled && !(r is ParticleSystemRenderer)) { r.enabled = true; restored.Add(r); }
            var shadowModes = b4.ShadowOnlyInB4.ToDictionary(r => r, r => r.shadowCastingMode);
            foreach (Renderer r in b4.ShadowOnlyInB4) r.shadowCastingMode = ShadowCastingMode.On;
            var camGo = new GameObject("MinimapBakeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = MapBounds.w * 0.5f;
            cam.aspect = MapBounds.z / MapBounds.w;
            cam.transform.position = new Vector3(MapBounds.x + MapBounds.z * 0.5f, 120f, MapBounds.y + MapBounds.w * 0.5f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.22f, 0.16f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 260f;
            const int size = 1024;
            var rt = RenderTexture.GetTemporary(size, size, 24);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            ShaderUtil.allowAsyncCompilation = false;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            RenderSettings.fog = fog;
            foreach (Renderer r in off) r.enabled = true;
            foreach (Renderer r in restored) r.enabled = false;
            foreach (var kv in shadowModes) kv.Key.shadowCastingMode = kv.Value;
            // a warm, slightly richer "map" grade; the edge of the valley darkens so the playable area reads at a glance
            Color32[] px = image.GetPixels32();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color c = px[y * size + x];
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    c = Color.Lerp(new Color(lum, lum, lum), c, 1.25f);
                    c = new Color(c.r * 1.10f + 0.03f, c.g * 1.06f + 0.02f, c.b * 0.96f, 1f);
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float vignette = Mathf.SmoothStep(1f, 0.55f, Mathf.InverseLerp(0.38f, 0.5f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))));
                    px[y * size + x] = c * vignette;
                }
            image.SetPixels32(px);
            image.Apply();
            File.WriteAllBytes(MinimapBasePath, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(MinimapBasePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MinimapBasePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------------------------------------ assets

        static Material TextureMaterial(string name, string texturePath, string shader, int maxSize, TextureImporterCompression compression, TextureWrapMode wrap)
        {
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (importer == null) throw new FileNotFoundException("Run tools/art/phase52b4/make_b4_hero.py and make_b4_props.py first: " + texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find(shader);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void UiSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        [Serializable] sealed class AtlasFrame { public string dir; public string kind; public int index; public int x, y, w, h; }
        [Serializable] sealed class HeroAtlas { public int[] atlas; public float pixelsPerMeter; public float strideMeters; public float idleFps; public AtlasFrame[] frames; }
        [Serializable] sealed class Direction { public float[] pivot; public int[] size; }

        static SpriteSheet25D HeroSheet(Texture2D atlasTexture)
        {
            string json = File.ReadAllText(Folder + "/Sprites/CaretakerB4Atlas.json");
            var data = JsonUtility.FromJson<HeroAtlas>(json);
            var sheet = AssetDatabase.LoadAssetAtPath<SpriteSheet25D>(HeroSheetPath);
            if (sheet == null) { sheet = ScriptableObject.CreateInstance<SpriteSheet25D>(); AssetDatabase.CreateAsset(sheet, HeroSheetPath); }
            sheet.atlas = atlasTexture;
            sheet.pixelsPerMeter = data.pixelsPerMeter;
            sheet.strideMeters = data.strideMeters;
            sheet.idleFps = data.idleFps;
            var dirs = new List<SpriteSheet25D.Direction>();
            foreach (string key in new[] { "S", "N", "W", "E" })
            {
                var d = JsonUtility.FromJson<Direction>(Section(json, "\"" + key + "\""));
                dirs.Add(new SpriteSheet25D.Direction { key = key, pivotPx = new Vector2(d.pivot[0], d.pivot[1]), run = Frames(data, key, "run"), idle = Frames(data, key, "idle") });
            }
            sheet.directions = dirs.ToArray();
            EditorUtility.SetDirty(sheet);
            return sheet;
        }

        static SpriteSheet25D.Frame[] Frames(HeroAtlas data, string dir, string kind)
        {
            var list = data.frames.Where(f => f.dir == dir && f.kind == kind).OrderBy(f => f.index).ToList();
            return list.Select(f => new SpriteSheet25D.Frame
            {
                uv = new Vector4((float)f.x / data.atlas[0], (float)f.y / data.atlas[1], (float)f.w / data.atlas[0], (float)f.h / data.atlas[1]),
                sizePx = new Vector2(f.w, f.h),
            }).ToArray();
        }

        /// <summary>The JSON object that follows <paramref name="quotedKey"/> inside "directions" (JsonUtility cannot read dictionaries).</summary>
        static string Section(string json, string quotedKey)
        {
            int start = json.IndexOf("\"directions\"", StringComparison.Ordinal);
            int i = json.IndexOf(quotedKey + ": {", start, StringComparison.Ordinal);
            if (i < 0) throw new InvalidDataException("Missing " + quotedKey + " in atlas metadata");
            int open = json.IndexOf('{', i), depth = 0;
            for (int j = open; j < json.Length; j++)
            {
                if (json[j] == '{') depth++;
                else if (json[j] == '}' && --depth == 0) return json.Substring(open, j - open + 1);
            }
            throw new InvalidDataException("Unterminated " + quotedKey);
        }

        // ------------------------------------------------------------------------------------------------ build

        public static void BuildWindowsPilot()
        {
            string output = Argument("-echoesBuildPath");
            if (string.IsNullOrEmpty(output) || Directory.Exists(output)) throw new IOException("Use a fresh explicit -echoesBuildPath; existing builds are preserved.");
            Directory.CreateDirectory(output);
            string version = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = "0.5.2-pilot.4";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Menu, Valley }, locationPathName = Path.Combine(output, "EchoesOfTheValley.exe"), target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None });
                var s = report.summary;
                Debug.Log("[Echoes][P52 B4] PILOT_BUILD " + s.result + " id=" + s.guid + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " bytes=" + s.totalSize + "; owner approval PENDING, not a release candidate.");
                if (s.result != BuildResult.Succeeded) throw new Exception("B4 pilot build failed.");
            }
            finally { PlayerSettings.bundleVersion = version; }
        }

        static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }
}
