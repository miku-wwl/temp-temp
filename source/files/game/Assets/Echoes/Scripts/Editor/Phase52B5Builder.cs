using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes.EditorTools
{
    /// <summary>
    /// Phase 5.2-B5 isolated pilot: the owner's B5 environment paintings in the B4 high-angle scene. Copies the B4 pilot scenes into
    /// Art/Phase52B5/Scenes and changes presentation only: the spring becomes one card with a registered painting per Resonance stage, the
    /// stream is drawn in the spring's colours and leaves the painted rim, the low-poly trees become painted broadleaf cards (their old
    /// meshes keep casting the shadow, their trunks keep blocking), the garden gets 3D raised beds with painted plants that wither and bloom
    /// with the garden's own stage roots, painted ground plants replace the low-poly tufts and flower patches, and the terrain takes the
    /// seam-free meadow and soil layers. Gameplay objects, interactions, quests and saves are the B4 ones; the colliders B5 adds follow
    /// the paintings. Everything is listed on Phase52B5Pilot, which shows the B4 presentation again in the same scene ("before" mode).
    /// </summary>
    public static class Phase52B5Builder
    {
        public const string Folder = "Assets/Echoes/Art/Phase52B5";
        public const string SceneFolder = Folder + "/Scenes";
        public const string Valley = SceneFolder + "/Valley.unity";
        public const string Menu = SceneFolder + "/MainMenu.unity";
        public const string TerrainPath = Folder + "/B5Terrain.asset";
        public const string MinimapBasePath = Folder + "/UI/MinimapBase.png";
        public const string Version = "0.5.2-pilot.5";
        public const float SpringMetresPerPixel = 0.005f;   // the B4 spring's scale: the new painting has the same design and footprint
        public static readonly Vector2 SpringCentre = new Vector2(-21f, 16f);
        public const float TrunkRadius = 0.30f;             // the painted trunk is about 0.6 m wide at the foot
        public static readonly Vector2[] BedCentres = { new Vector2(-2f, -1.5f), new Vector2(2f, -1.5f), new Vector2(-2f, 1.5f), new Vector2(2f, 1.5f) };
        public static readonly Vector3 BedSize = new Vector3(3.04f, 0.34f, 1.84f);
        public const float BloomSoilTop = 0.30f, DormantSoilTop = 0.24f;   // RichSoil / DrySoil cube tops (Phase3Pieces)

        // ------------------------------------------------------------------------------------------------ bookkeeping

        sealed class Lists
        {
            public readonly HashSet<Renderer> b4Owned;
            public readonly List<GameObject> b5Only = new List<GameObject>();
            public readonly List<Renderer> hidden = new List<Renderer>();
            public readonly List<Renderer> shadowOnly = new List<Renderer>();
            public readonly List<Renderer> treeRenderers = new List<Renderer>();
            public readonly List<Collider> colliders = new List<Collider>();
            public readonly List<Collider> disabled = new List<Collider>();
            public readonly List<Phase52B5Pilot.MaterialSwap> restyled = new List<Phase52B5Pilot.MaterialSwap>();
            public readonly List<Phase52B5Pilot.MeshSwap> meshes = new List<Phase52B5Pilot.MeshSwap>();
            public readonly List<Phase52B5Pilot.CapsuleSwap> trunks = new List<Phase52B5Pilot.CapsuleSwap>();

            public Lists(Phase52B4Pilot b4) => b4Owned = new HashSet<Renderer>(b4.HiddenInB4.Concat(b4.ShadowOnlyInB4).Where(r => r != null));

            /// <summary>Hidden in B5 (and drawn again in before mode): only renderers B4 draws and does not already manage.</summary>
            public void Hide(Renderer r)
            {
                if (r == null || r is ParticleSystemRenderer || !r.enabled || b4Owned.Contains(r) || hidden.Contains(r) || shadowOnly.Contains(r)) return;
                hidden.Add(r);
            }

            public void HideAll(Transform root) { foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) Hide(r); }

            public void ShadowOnly(Renderer r)
            {
                if (r == null || !r.enabled || b4Owned.Contains(r) || hidden.Contains(r) || shadowOnly.Contains(r)) return;
                if (r.shadowCastingMode == ShadowCastingMode.On) shadowOnly.Add(r); else hidden.Add(r);
            }

            public void Restyle(Renderer r, Material b5)
            {
                if (r == null || b4Owned.Contains(r) || restyled.Any(s => s.renderer == r)) return;
                restyled.Add(new Phase52B5Pilot.MaterialSwap { renderer = r, before = r.sharedMaterial, b5 = b5 });
            }
        }

        [Serializable] sealed class SpringMeta { public int[] sliceSize; public int[] bottomProfile; public int bottomStep; public int slices; }
        [Serializable] sealed class TreeMeta { public int[] size; public int[] trunkFootPx; }
        [Serializable] sealed class AtlasRect { public int x, y, w, h; }
        [Serializable] sealed class Plant { public string name; public string kind; public float heightMetres; public int[] pixels; public AtlasRect lush; public AtlasRect withered; }
        [Serializable] sealed class PlantAtlas { public int[] atlas; public Plant[] plants; }
        [Serializable] sealed class FoliageRect { public int x, y, w, h; }
        [Serializable] sealed class FoliageItems { public FoliageRect grass, flowers, fern; }
        [Serializable] sealed class FoliageAtlas { public int[] atlas; public FoliageItems items; }

        /// <summary>Everything loaded again by path after the scene opens (opening a scene unloads unreferenced in-memory assets).</summary>
        sealed class Assets
        {
            public Material spring, trees, garden, foliage, waterMurky, waterClear, wood, soilRich, soilDry, rope, cloth, contactShadow;
            public TerrainLayer meadowA, meadowB, soil, cobble;
            public Func<string, Material> plazaStone;
            public SpringMeta springMeta;
            public TreeMeta treeMeta;
            public PlantAtlas plants;
            public FoliageAtlas foliageAtlas;
            public Mesh propCard;

            public Plant P(string name) => plants.plants.First(p => p.name == name);
        }

        // ------------------------------------------------------------------------------------------------ generate

        [MenuItem("Echoes/Phase 5.2-B5/Generate painted environment pilot")]
        public static void Generate()
        {
            AssetPaths.EnsureFolder(SceneFolder);
            AssetPaths.EnsureFolder(Folder + "/Materials");
            AssetPaths.EnsureFolder(Folder + "/Meshes");
            AssetPaths.EnsureFolder(Folder + "/UI");
            CreateAssets();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(Phase52B4Builder.Valley, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, Valley, true)) throw new IOException("Could not copy the B4 pilot Valley.");
            scene = EditorSceneManager.OpenScene(Valley, OpenSceneMode.Single);
            Assets A = LoadAssets();

            var pilot = UnityEngine.Object.FindFirstObjectByType<Phase52Pilot>();
            var b4 = UnityEngine.Object.FindFirstObjectByType<Phase52B4Pilot>();
            var b5 = pilot.gameObject.AddComponent<Phase52B5Pilot>();
            var L = new Lists(b4);
            Terrain terrain = pilot.Terrain;
            var view = UnityEngine.Object.FindFirstObjectByType<ResonanceView>();
            var stages = new SerializedObject(view);

            Vector2[] springFootprint = Spring(A, L, terrain, view, b4);
            Stream(A, L, springFootprint);
            var trunks = Trees(A, L, terrain, pilot, stages);
            Transform gardenRoot = ((GameObject)stages.FindProperty("gardenDormant").objectReferenceValue).transform.parent;
            Garden(A, L, gardenRoot, stages);
            Foliage(A, L, terrain, stages, springFootprint, trunks, b4);
            Nodes(A, L);
            HeartCrystal(L);
            Plaza(A, L, b4);
            Details(A, L, stages);

            b5.Terrain = terrain;
            b5.BeforeTerrain = b4.B4Terrain;
            b5.B5Terrain = B5TerrainData(b4.B4Terrain, A, terrain.transform.position, springFootprint, gardenRoot.position);
            b5.B5Only = L.b5Only.Distinct().ToArray();
            b5.HiddenInB5 = L.hidden.Distinct().ToArray();
            b5.ShadowOnlyInB5 = L.shadowOnly.Distinct().ToArray();
            b5.B5Colliders = L.colliders.ToArray();
            b5.DisabledInB5 = L.disabled.ToArray();
            b5.Restyled = L.restyled.ToArray();
            b5.Meshes = L.meshes.ToArray();
            b5.Trunks = L.trunks.ToArray();
            // the silent valley's light: the same muted level, grey-green instead of blue (B1-B4 keep theirs in before mode)
            var lighting = UnityEngine.Object.FindFirstObjectByType<ValleyLighting>();
            b5.Lighting = lighting;
            b5.BeforeLight = new Phase52B5Pilot.MutedLight { sun = lighting.Sun.color, ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor, ambientGround = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor };
            b5.B5Light = new Phase52B5Pilot.MutedLight { sun = new Color(0.92f, 0.91f, 0.88f), ambientSky = new Color(0.55f, 0.59f, 0.62f), ambientEquator = new Color(0.50f, 0.50f, 0.49f), ambientGround = new Color(0.32f, 0.31f, 0.29f), fog = new Color(0.66f, 0.68f, 0.68f) };
            var minimap = UnityEngine.Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            b5.MinimapImage = minimap.Map;
            b5.BeforeMinimap = minimap.Map.texture;
            b5.Apply(false);
            BakeMinimap(b4, b5, L.treeRenderers);
            b5.B5Minimap = AssetDatabase.LoadAssetAtPath<Texture2D>(MinimapBasePath);
            b5.Apply(false);
            foreach (var painting in UnityEngine.Object.FindObjectsByType<StagePainting25D>(FindObjectsInactive.Include, FindObjectsSortMode.None)) painting.Refresh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(Phase52B4Builder.Menu, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, Menu, true)) throw new IOException("Could not copy the B4 pilot menu.");
            AssetDatabase.SaveAssets();
            Debug.Log("[Echoes][P52 B5] painted environment pilot generated: " + Valley + "; B5 adds " + b5.B5Only.Length + " roots, hides " + b5.HiddenInB5.Length
                + " renderers, " + b5.ShadowOnlyInB5.Length + " shadow-only, " + b5.Restyled.Length + " restyled, " + b5.B5Colliders.Length + " colliders added, "
                + b5.DisabledInB5.Length + " disabled, " + b5.Trunks.Length + " trunks widened; original, B2, B3 and B4 scenes untouched.");
        }

        // ------------------------------------------------------------------------------------------------ assets

        static void CreateAssets()
        {
            var stages = StageArray(Folder + "/Props/SpringStages.png", 4);
            var spring = Mat("Prop_SpringStages", "Echoes/StageProp25D");
            spring.SetTexture("_Stages", stages);
            spring.SetFloat("_StageCount", 4f);
            spring.SetFloat("_Cutoff", 0.5f);

            var trees = Mat("Card_BroadleafTree", "Echoes/PaintedCard25D");
            trees.mainTexture = CardTexture(Folder + "/Props/BroadleafTree.png", 2048, 0.45f);
            trees.SetFloat("_Cutoff", 0.45f);
            trees.SetFloat("_DepthPull", 0.2f);
            trees.SetFloat("_FadeRadius", 0.16f);    // an ellipse 0.16 screen heights across, 0.26 tall: the caretaker at the default zoom is ~0.17 tall
            trees.SetFloat("_FadeStrength", 0.92f);

            var garden = Mat("Card_GardenPlants", "Echoes/PaintedCard25D");
            garden.mainTexture = CardTexture(Folder + "/Props/GardenPlants.png", 2048, 0.4f);
            garden.SetFloat("_Cutoff", 0.4f);
            garden.SetFloat("_DepthPull", 0.05f);

            var foliage = Mat("Card_Foliage", "Echoes/PaintedCard25D");
            foliage.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Phase52B4Builder.Folder + "/Props/Foliage.png");   // the B4 atlas, imported by B4
            foliage.SetFloat("_Cutoff", 0.35f);
            foliage.SetFloat("_DepthPull", 0.05f);

            var murky = Mat("Water_Murky", "Echoes/PaintedWater");
            murky.SetColor("_DeepColor", Rgb(28, 33, 26));
            murky.SetColor("_MidColor", Rgb(45, 52, 40));
            murky.SetColor("_LightColor", Rgb(104, 108, 86));
            murky.SetFloat("_Opacity", 0.62f);
            murky.SetFloat("_GlintStrength", 0.28f);
            murky.SetFloat("_BankSoftness", 0.6f);
            murky.SetFloat("_SunWeight", 0.5f);
            var clear = Mat("Water_Clear", "Echoes/PaintedWater");
            clear.SetColor("_DeepColor", Rgb(24, 56, 60));
            clear.SetColor("_MidColor", Rgb(50, 104, 101));
            clear.SetColor("_LightColor", Rgb(140, 190, 172));
            clear.SetFloat("_Opacity", 0.84f);
            clear.SetFloat("_GlintStrength", 0.35f);
            clear.SetFloat("_BankSoftness", 0.5f);
            clear.SetFloat("_SunWeight", 0.5f);

            // the B2 painted timber and stone textures are near-grey: their colour comes from the tint (Art_Timber's own is 0.46 / 0.33 / 0.25)
            Texture2D timber = (Texture2D)AssetDatabase.LoadAssetAtPath<Material>("Assets/Echoes/Art/Pilot/Art_Timber.mat").GetTexture("_BaseMap");
            var wood = Triplanar("Bed_Wood", timber, new Color(0.60f, 0.43f, 0.30f), new Color(0.66f, 0.48f, 0.33f), 1.1f);
            Texture2D soil = GroundTexture(Folder + "/Terrain/TilledSoil.png");
            Triplanar("Bed_SoilRich", soil, new Color(0.80f, 0.72f, 0.66f), new Color(0.84f, 0.76f, 0.70f), 1.4f);
            Triplanar("Bed_SoilDry", soil, new Color(1.02f, 0.94f, 0.85f), new Color(1.10f, 1.00f, 0.90f), 1.4f);
            // the plaza's stone: one warm grey stone (B4's cobble by luminance) for the terrain's cobble layer and for the dais, pillars and
            // blocks (B4's own tints and tile sizes), so the plaza reads as one material instead of yellow ruins on blue-grey cobbles
            Texture2D stone = GroundTexture(Folder + "/Terrain/PlazaCobbleWarm.png");
            foreach (string b4Name in new[] { "PlazaStoneB4", "PlazaStoneDarkB4", "PlazaDaisB4", "PlazaDaisInnerB4" })
            {
                var b4Material = AssetDatabase.LoadAssetAtPath<Material>(Phase52B4Builder.Folder + "/" + b4Name + ".mat");
                if (b4Material == null) continue;
                Triplanar(b4Name.Replace("B4", "B5"), stone, b4Material.GetColor("_BaseColor"), b4Material.GetColor("_TopColor"), b4Material.GetFloat("_MetresPerTile"));
            }
            GroundLayer("B5PlazaCobble", Folder + "/Terrain/PlazaCobbleWarm.png", 3.4f);
            // outputs of earlier versions of this generator that nothing uses any more (the boulders are painted cards now)
            AssetDatabase.DeleteAsset(Folder + "/Materials/Rock_Painted.mat");
            Lit("Bed_Rope", new Color(0.70f, 0.58f, 0.40f));
            Lit("Scarecrow_Cloth", new Color(0.50f, 0.46f, 0.32f));
            var shadow = Mat("Ground_ContactShadow", "Echoes/ContactShadow");
            shadow.SetColor("_Color", new Color(0.09f, 0.08f, 0.06f, 0.40f));
            shadow.SetFloat("_Softness", 1f);

            GroundLayer("B5MeadowA", Folder + "/Terrain/MeadowA.png", 5.5f);
            GroundLayer("B5MeadowB", Folder + "/Terrain/MeadowB.png", 6.3f);
            GroundLayer("B5TilledSoil", Folder + "/Terrain/TilledSoil.png", 3.2f);
            _ = wood;
        }

        static Assets LoadAssets()
        {
            Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
            TerrainLayer T(string name) => AssetDatabase.LoadAssetAtPath<TerrainLayer>(Folder + "/Terrain/" + name + ".terrainlayer");
            return new Assets
            {
                spring = M("Prop_SpringStages"), trees = M("Card_BroadleafTree"), garden = M("Card_GardenPlants"), foliage = M("Card_Foliage"),
                waterMurky = M("Water_Murky"), waterClear = M("Water_Clear"), wood = M("Bed_Wood"), soilRich = M("Bed_SoilRich"), soilDry = M("Bed_SoilDry"),
                rope = M("Bed_Rope"), cloth = M("Scarecrow_Cloth"), contactShadow = M("Ground_ContactShadow"),
                meadowA = T("B5MeadowA"), meadowB = T("B5MeadowB"), soil = T("B5TilledSoil"), cobble = T("B5PlazaCobble"),
                plazaStone = b4Name => M(b4Name.Replace("B4", "B5")),
                springMeta = JsonUtility.FromJson<SpringMeta>(File.ReadAllText(Folder + "/Props/SpringStages.json")),
                treeMeta = JsonUtility.FromJson<TreeMeta>(File.ReadAllText(Folder + "/Props/BroadleafTree.json")),
                plants = JsonUtility.FromJson<PlantAtlas>(File.ReadAllText(Folder + "/Props/GardenPlants.json")),
                foliageAtlas = JsonUtility.FromJson<FoliageAtlas>(File.ReadAllText(Phase52B4Builder.Folder + "/Props/Foliage.json")),
                propCard = AssetDatabase.LoadAssetAtPath<Mesh>(Phase52B4Builder.Folder + "/PropCard.asset"),
            };
        }

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        static Material Mat(string name, string shaderName)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new FileNotFoundException("Missing shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Triplanar(string name, Texture2D texture, Color tint, Color topTint, float metresPerTile)
        {
            var material = Mat(name, "Echoes/TriplanarStone");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_TopColor", topTint);
            material.SetFloat("_MetresPerTile", metresPerTile);
            return material;
        }

        static Material Lit(string name, Color colour)
        {
            var material = Mat(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        /// <summary>The stage strip (stage 0 on top) as a Texture2DArray: one slice per stage, BC7, mipmaps, no bleeding between stages.</summary>
        static Texture2DArray StageArray(string path, int slices)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Run tools/art/phase52b5/make_b5_spring.py first: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2DArray;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.flipbookRows = slices;
            settings.flipbookColumns = 1;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (array == null || array.depth != slices) throw new InvalidDataException("The spring stages did not import as a " + slices + "-slice Texture2DArray: " + path);
            return array;
        }

        /// <summary>A painted card texture: straight alpha, mipmaps that keep the alpha-tested coverage of leaves at a distance.</summary>
        static Texture2D CardTexture(string path, int maxSize, float cutoff)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Run the tools/art/phase52b5 scripts first: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = cutoff;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Opaque ground colour: RGB only (URP reads a diffuse alpha as smoothness, the B2 ground-glint lesson), DXT1, trilinear with 8x
        /// anisotropy so the seam-free painting does not shimmer at the high camera's grazing distance, repeat wrap (the seams were repaired
        /// in the texture itself by make_b5_ground.py; the wrap mode only repeats it).
        /// </summary>
        static Texture2D GroundTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Run tools/art/phase52b5/make_b5_ground.py first: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.DXT1;
            standalone.maxTextureSize = 1024;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static TerrainLayer GroundLayer(string name, string texturePath, float tile)
        {
            Texture2D texture = GroundTexture(texturePath);
            string path = Folder + "/Terrain/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static float Height(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static GameObject CardObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        // ------------------------------------------------------------------------------------------------ painted cards

        /// <summary>Camera-facing cards in one mesh for Echoes/PaintedCard25D: ground point per vertex, corner offsets in metres, tint.</summary>
        sealed class Cards
        {
            readonly List<Vector3> points = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>(), corners = new List<Vector2>();
            readonly List<Color> colours = new List<Color>();
            readonly List<int> triangles = new List<int>();
            float extent;
            public int Count => points.Count / 6;

            /// <summary>
            /// uv: the atlas rect; foot: where the card stands, as a fraction of its width (from the left) and height (from the bottom). Three
            /// rows of vertices (bottom, foot, top): the shader lays the part below the foot onto the ground in front of it, so the painted
            /// roots and the base of a clump are not cut off by the terrain along a straight line.
            /// </summary>
            public void Add(Vector3 ground, Rect uv, float width, float height, Vector2 foot, bool mirror, Color tint, bool opens)
            {
                int b = points.Count;
                for (int k = 0; k < 6; k++) points.Add(ground);
                float u0 = uv.xMin, u1 = uv.xMax;
                if (mirror) { (u0, u1) = (u1, u0); foot.x = 1f - foot.x; }
                float vFoot = Mathf.Lerp(uv.yMin, uv.yMax, foot.y);
                uvs.Add(new Vector2(u0, uv.yMin)); uvs.Add(new Vector2(u1, uv.yMin));
                uvs.Add(new Vector2(u0, vFoot)); uvs.Add(new Vector2(u1, vFoot));
                uvs.Add(new Vector2(u0, uv.yMax)); uvs.Add(new Vector2(u1, uv.yMax));
                float left = -foot.x * width, right = (1f - foot.x) * width, bottom = -foot.y * height, top = (1f - foot.y) * height;
                corners.Add(new Vector2(left, bottom)); corners.Add(new Vector2(right, bottom));
                corners.Add(new Vector2(left, 0f)); corners.Add(new Vector2(right, 0f));
                corners.Add(new Vector2(left, top)); corners.Add(new Vector2(right, top));
                var c = new Color(tint.r, tint.g, tint.b, opens ? 1f : 0f);
                for (int k = 0; k < 6; k++) colours.Add(c);
                triangles.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1, b + 2, b + 4, b + 3, b + 4, b + 5, b + 3 });
                extent = Mathf.Max(extent, width, height);
            }

            public Mesh Save(string name)
            {
                string path = Folder + "/Meshes/" + name + ".asset";
                AssetDatabase.DeleteAsset(path);
                var mesh = new Mesh { name = name, indexFormat = points.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(points);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, corners);
                mesh.SetColors(colours);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds;
                bounds.Expand(2f * extent + 1f);   // the shader moves the corners up to a card's size away from its ground point
                mesh.bounds = bounds;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
        }

        static Rect PlantRect(Assets A, AtlasRect r) => new Rect((float)r.x / A.plants.atlas[0], (float)r.y / A.plants.atlas[1], (float)r.w / A.plants.atlas[0], (float)r.h / A.plants.atlas[1]);
        static Rect FoliageRectUv(Assets A, FoliageRect r) => new Rect((float)r.x / A.foliageAtlas.atlas[0], (float)r.y / A.foliageAtlas.atlas[1], (float)r.w / A.foliageAtlas.atlas[0], (float)r.h / A.foliageAtlas.atlas[1]);

        /// <summary>A plant from the farm painting standing at a ground point (withered or lush), height in metres.</summary>
        static void AddPlant(Cards cards, Assets A, string name, bool withered, Vector3 ground, float scale, bool mirror, Color tint)
        {
            Plant p = A.P(name);
            AtlasRect r = withered ? p.withered : p.lush;
            float h = p.heightMetres * scale * (withered ? 0.8f : 1f);
            cards.Add(ground, PlantRect(A, r), h * r.w / Mathf.Max(1, r.h), h, new Vector2(0.5f, 0.05f), mirror, tint, false);
        }

        /// <summary>A clump from the B4 foliage atlas (grass, flowers, fern).</summary>
        static void AddFoliage(Cards cards, Assets A, int kind, Vector3 ground, float height, bool mirror, Color tint)
        {
            FoliageRect r = kind == 0 ? A.foliageAtlas.items.grass : kind == 1 ? A.foliageAtlas.items.flowers : A.foliageAtlas.items.fern;
            cards.Add(ground, FoliageRectUv(A, r), height * r.w / Mathf.Max(1, r.h), height, new Vector2(0.5f, 0.06f), mirror, tint, false);
        }

        static int Hash(Vector3 p) { unchecked { int h = (int)(p.x * 73856.093f) ^ (int)(p.z * 19349.663f) ^ (int)(p.y * 83492.791f); return h & 0x7fffffff; } }

        // ------------------------------------------------------------------------------------------------ spring

        /// <summary>
        /// The new spring: one StageProp25D card with the four registered stage paintings, placed like the B4 spring (0.005 m per painting
        /// pixel, the painted footprint's centre on the spring), its ground line from the painting's own bottom outline. The B4 cards and
        /// their StageVariant25D stay in the scene for before mode; their renderers are hidden. Returns the painted footprint (L, F, R, B).
        /// </summary>
        static Vector2[] Spring(Assets A, Lists L, Terrain terrain, ResonanceView view, Phase52B4Pilot b4)
        {
            foreach (var prop in UnityEngine.Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (prop.name == "Spring" || prop.name == "SpringDormant") L.Hide(prop.GetComponent<MeshRenderer>());

            SpringMeta meta = A.springMeta;
            float m = SpringMetresPerPixel, W = meta.sliceSize[0], H = meta.sliceSize[1];
            int[] p = meta.bottomProfile;
            var columns = Enumerable.Range(0, p.Length).Where(i => p[i] >= 0).ToArray();
            int first = columns.First(), last = columns.Last();
            int front = columns.Where(i => i > p.Length * 0.25f && i < p.Length * 0.80f).OrderByDescending(i => p[i]).First();
            Vector2 Lp = new Vector2(first * meta.bottomStep, p[first]), Fp = new Vector2(front * meta.bottomStep, p[front]), Rp = new Vector2(last * meta.bottomStep, p[last]);
            Vector3 right = Phase52B4Builder.CameraRight, up = Phase52B4Builder.CameraUp;
            Vector3 Offset(Vector2 px) => right * ((px.x - Fp.x) * m) + up * ((Fp.y - px.y) * m);
            Vector3 GroundOffset(Vector3 d) { Vector3 g = Phase52B4Builder.GroundUnder(d, 0f); g.y = 0f; return g; }
            // the painting stands on the spring's level ground (the stream's carved bed begins under its front corner: not its floor)
            float level = Height(terrain, new Vector3(SpringCentre.x, 0f, SpringCentre.y));
            Vector3 anchor = new Vector3(SpringCentre.x, 0f, SpringCentre.y) - GroundOffset(Offset((Lp + Rp) * 0.5f));
            anchor.y = level;
            Vector3 Ground(Vector2 px) { Vector3 g = anchor + GroundOffset(Offset(px)); g.y = level; return g; }

            var root = new GameObject("B5_Spring");
            L.b5Only.Add(root);
            var card = CardObject("SpringStages", root.transform, A.propCard, A.spring);
            card.transform.position = anchor;
            var prop25 = card.AddComponent<StaticProp25D>();
            Vector3 gl = Ground(Lp), gf = Ground(Fp), gr = Ground(Rp);
            prop25.Configure(new Vector4(-Fp.x * m, -(H - Fp.y) * m, W * m, H * m), gl, gf, gr, new Vector3(Lp.x / W, Fp.x / W, Rp.x / W), false);
            var painting = card.AddComponent<StagePainting25D>();
            Wire.Set(painting, "view", view);
            painting.Refresh();
            Vector3 gb = gl + gr - gf;
            var footprint = new[] { new Vector2(gl.x, gl.z), new Vector2(gf.x, gf.z), new Vector2(gr.x, gr.z), new Vector2(gb.x, gb.z) };

            // collision on the painted footprint (inset 0.35 m), like the B4 workshop; old stones left outside the painting stop blocking
            Transform spring = GameObject.Find("RuinedSpring").transform;
            var collider = FootprintPrism("SpringFootprintB5", spring, gl, gf, gr, 0.35f, 1.6f);
            L.colliders.Add(collider);
            foreach (Collider c in spring.GetComponentsInChildren<Collider>(true))
            {
                if (c == collider || !c.enabled) continue;
                Vector3 centre = c.bounds.center;
                if (!Inside(footprint, new Vector2(centre.x, centre.z), 0.3f)) L.disabled.Add(c);
            }
            // the S1 fountain particles would spray from the middle of the painted pool (the paintings carry the falls)
            var fountain = (ParticleSystem)new SerializedObject(view).FindProperty("fountain").objectReferenceValue;
            if (fountain != null) foreach (var r in fountain.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (r.enabled) { r.enabled = false; L.hidden.Add(r); }

            // a soft contact shadow round the painted base, so the stones sit in the grass instead of on it
            var shadow = CardObject("ContactShadow", root.transform, FootprintShadowMesh("SpringContactShadowB5", terrain, gl, gf, gr, 0.15f, 1.1f), A.contactShadow);
            Debug.Log("[Echoes][P52 B5] spring card: ground line L " + gl.ToString("F2") + " F " + gf.ToString("F2") + " R " + gr.ToString("F2") + "; footprint half-diagonals "
                + (Vector3.Distance(gl, gr) * 0.5f).ToString("F2") + " / " + (Vector3.Distance(gf, gb) * 0.5f).ToString("F2") + " m; " + L.disabled.Count + " old stone colliders outside it disabled");
            _ = shadow;
            return footprint;
        }

        /// <summary>A convex prism on a painted footprint (the parallelogram L, F, R, L + R - F), inset towards its centre.</summary>
        static Collider FootprintPrism(string name, Transform parent, Vector3 L, Vector3 F, Vector3 R, float inset, float height)
        {
            Vector3 B = L + R - F, centre = (L + R) * 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(new Vector3(centre.x, Mathf.Min(F.y, L.y, R.y, B.y) - 0.2f, centre.z), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var ring = new List<Vector3>();
            foreach (Vector3 c in new[] { F, R, B, L })
            {
                Vector3 local = c - centre;
                local.y = 0f;
                local -= local.normalized * inset * Mathf.Sqrt(2f);
                ring.Add(local);
            }
            var verts = ring.Concat(ring.Select(c => c + Vector3.up * (height + 0.2f))).ToArray();
            int[] tris = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
            string path = Folder + "/Meshes/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            return collider;
        }

        /// <summary>
        /// A ground-hugging band round a painted footprint for Echoes/ContactShadow: dark (uv radius 0) from slightly inside the outline,
        /// fading to nothing (uv radius 1) a little outside it; every vertex sits on the terrain.
        /// </summary>
        static Mesh FootprintShadowMesh(string name, Terrain terrain, Vector3 L, Vector3 F, Vector3 R, float inside, float outside)
        {
            Vector3 B = L + R - F, centre = (L + R) * 0.5f;
            var corners = new[] { F, R, B, L };
            const int steps = 10;
            var verts = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            Vector3 OnGround(Vector3 p) { p.y = Height(terrain, p) + 0.04f; return p; }
            int ringCount = corners.Length * steps;
            for (int i = 0; i < ringCount; i++)
            {
                Vector3 a = corners[i / steps], b = corners[(i / steps + 1) % 4];
                Vector3 p = Vector3.Lerp(a, b, (i % steps) / (float)steps);
                Vector3 outward = p - centre;
                outward.y = 0f;
                outward.Normalize();
                verts.Add(OnGround(p - outward * inside)); uv.Add(new Vector2(0.5f, 0.5f));
                verts.Add(OnGround(p + outward * outside)); uv.Add(new Vector2(1.0f, 0.5f));
            }
            for (int i = 0; i < ringCount; i++)
            {
                int a = i * 2, b = ((i + 1) % ringCount) * 2;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            string path = Folder + "/Meshes/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static bool Inside(Vector2[] poly, Vector2 p, float margin)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            if (inside) return true;
            return DistanceToPolygon(poly, p) < margin;
        }

        static float DistanceToPolygon(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ stream

        /// <summary>
        /// The trickle (stage 0) and the wide stream (from stage 1) keep their objects, which ResonanceView switches; only their material
        /// changes: the spring painting's murky and clear water instead of flat planes. The water fades in where the stream leaves the
        /// painted footprint, so it flows out from under the rim instead of lying over the stones.
        /// </summary>
        static void Stream(Assets A, Lists L, Vector2[] springFootprint)
        {
            List<Vector2> line = ValleyLayout.Stream;
            float travelled = 0f, exit = 0f;
            bool wasInside = Inside(springFootprint, line[0], 0.25f);
            for (int i = 1; i < line.Count && wasInside; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                for (int s = 1; s <= 20; s++)
                {
                    Vector2 q = Vector2.Lerp(a, b, s / 20f);
                    if (!Inside(springFootprint, q, 0.25f)) { exit = travelled + Vector2.Distance(a, q); wasInside = false; break; }
                }
                travelled += Vector2.Distance(a, b);
            }
            var fade = new Vector4(exit * 0.25f, (exit + 1.6f) * 0.25f, 0f, 0f);   // the stream mesh runs 0.25 uv per metre
            A.waterMurky.SetVector("_StartFade", fade);
            A.waterClear.SetVector("_StartFade", fade);
            EditorUtility.SetDirty(A.waterMurky);
            EditorUtility.SetDirty(A.waterClear);
            Transform water = GameObject.Find("World/Water").transform;
            L.Restyle(water.Find("StreamTrickle").GetComponent<MeshRenderer>(), A.waterMurky);
            L.Restyle(water.Find("Stream").GetComponent<MeshRenderer>(), A.waterClear);
            Debug.Log("[Echoes][P52 B5] the stream leaves the painted spring " + exit.ToString("F2") + " m from its source; it fades in over 1.6 m");
        }

        // ------------------------------------------------------------------------------------------------ trees

        /// <summary>
        /// Every low-poly tree that B4 still draws becomes the owner's painted broadleaf as a camera-facing card on its own root (one mesh,
        /// one draw call, mirrored and tinted per tree so the valley is not one tree repeated). The old tree keeps casting its real shadow
        /// (shadow-only) and its trunk capsule keeps blocking, widened to the painted trunk. Near trees open a dithered window round the
        /// player when she walks behind or under them. The stage-3 saplings get painted young trees that pop up with their stage; the
        /// stage-1 saplings round the spring would stand in front of the painted spring and hide it, so they become flowers (Foliage).
        /// Returns the trunk positions (ground plants keep clear of them).
        /// </summary>
        static List<Vector3> Trees(Assets A, Lists L, Terrain terrain, Phase52Pilot pilot, SerializedObject stages)
        {
            TreeMeta meta = A.treeMeta;
            float aspect = (float)meta.size[0] / meta.size[1];
            var foot = new Vector2((float)meta.trunkFootPx[0] / meta.size[0], (float)(meta.size[1] - meta.trunkFootPx[1]) / meta.size[1]);
            var candidates = new List<(Transform root, bool distant)>();
            if (pilot.Route != null) foreach (Transform t in pilot.Route.transform) if (t.name.EndsWith("_Pilot")) candidates.Add((t, false));
            foreach (string group in new[] { "World/Trees", "World/WindmillPilot" })
            {
                GameObject g = GameObject.Find(group);
                if (g != null) foreach (Transform t in g.transform) if (t.name.StartsWith("Tree")) candidates.Add((t, false));
            }
            GameObject line = GameObject.Find("World/DistantTreeLine");
            if (line != null) foreach (Transform t in line.transform) candidates.Add((t, true));

            var cards = new Cards();
            var placed = new List<Vector3>();
            var capsules = UnityEngine.Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Where(c => c.enabled && c.name.StartsWith("Tree")).ToList();
            foreach (var (root, distant) in candidates)
            {
                var visible = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !L.b4Owned.Contains(r)).ToArray();
                if (visible.Length == 0) continue;                       // replaced by the B2 pilot or by the B4 willow painting
                if (placed.Any(p => (new Vector2(p.x, p.z) - new Vector2(root.position.x, root.position.z)).sqrMagnitude < 0.36f))
                {
                    foreach (var r in visible) L.Hide(r);
                    continue;
                }
                Bounds bounds = visible[0].bounds;
                foreach (var r in visible) bounds.Encapsulate(r.bounds);
                float oldHeight = bounds.max.y - root.position.y;
                float height = distant ? Mathf.Clamp(oldHeight * 1.05f, 4f, 9f) : Mathf.Clamp(oldHeight, 3.8f, 6.4f);
                if (!distant && NearWorkPlace(new Vector2(root.position.x, root.position.z))) height = Mathf.Min(height * 0.8f, 5f);
                int h = Hash(root.position);
                float value = 0.92f + 0.12f * ((h >> 3) % 100) / 100f;
                var tint = new Color(value * (0.95f + 0.08f * ((h >> 9) % 100) / 100f), value, value * (0.90f + 0.08f * ((h >> 15) % 100) / 100f));
                Vector3 ground = root.position;
                ground.y = Height(terrain, ground);
                cards.Add(ground, new Rect(0f, 0f, 1f, 1f), height * aspect, height, foot, (h & 1) == 1, tint, !distant);
                placed.Add(ground);
                foreach (var r in visible)
                {
                    L.treeRenderers.Add(r);
                    if (distant) L.Hide(r); else L.ShadowOnly(r);
                }
                if (!distant)
                    foreach (var capsule in capsules.Where(c => (new Vector2(c.transform.position.x, c.transform.position.z) - new Vector2(ground.x, ground.z)).sqrMagnitude < 0.25f))
                    {
                        if (L.trunks.Any(s => s.collider == capsule)) continue;
                        float scale = Mathf.Max(1e-3f, Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z));
                        L.trunks.Add(new Phase52B5Pilot.CapsuleSwap { collider = capsule, beforeRadius = capsule.radius, b5Radius = Mathf.Max(capsule.radius, TrunkRadius / scale) });
                    }
            }
            var trees = CardObject("B5_PaintedTrees", null, cards.Save("PaintedTreesB5"), A.trees);
            L.b5Only.Add(trees);

            // the stage-3 saplings (village life): a painted young tree on each, scaled and popped with the sapling itself
            var sapling = new Cards();
            sapling.Add(Vector3.zero, new Rect(0f, 0f, 1f, 1f), 6.0f * aspect, 6.0f, foot, false, Color.white, true);
            Mesh saplingMesh = sapling.Save("PaintedSaplingB5");
            var village = (GameObject)stages.FindProperty("villageLifeRoot").objectReferenceValue;
            foreach (Transform t in village.transform)
            {
                if (!t.name.StartsWith("Tree_Blossom")) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) L.Hide(r);
                L.b5Only.Add(CardObject("B5_PaintedSapling", t, saplingMesh, A.trees));
            }
            Debug.Log("[Echoes][P52 B5] painted trees: " + cards.Count + " cards (" + candidates.Count(c => c.distant) + " on the distant ridge), " + L.trunks.Count + " trunks widened to " + TrunkRadius + " m");
            return placed;
        }

        /// <summary>Close to where the player works (the plaza, the workshop, the spring, the garden, the hen yard, Mira, the windmill's foot):
        /// a painted canopy there is kept smaller, so it covers less of the place on screen.</summary>
        static bool NearWorkPlace(Vector2 p) =>
            p.magnitude < 16f || Vector2.Distance(p, ValleyLayout.Cottage) < 10f || Vector2.Distance(p, ValleyLayout.Spring) < 10f
            || Vector2.Distance(p, ValleyLayout.Garden) < 10f || Vector2.Distance(p, ValleyLayout.HenYard) < 9f || Vector2.Distance(p, ValleyLayout.Villager) < 6f
            || Vector2.Distance(p, ValleyLayout.WindmillRestorer) < 7f;

        // ------------------------------------------------------------------------------------------------ garden

        /// <summary>
        /// What grows in each bed, row by row from the back (the long side away from the camera): salad, roots, flowers and herbs, kitchen
        /// herbs. Each row alternates its plants along the bed; the wide lavender gets three places, everything else five.
        /// </summary>
        static readonly string[][][] BedRows =
        {
            new[] { new[] { "lettuceA" }, new[] { "redLettuce", "lettuceA" }, new[] { "lettuceB", "redLettuce" }, new[] { "seedlingA", "lettuceB" } },
            new[] { new[] { "carrotA", "carrotB" }, new[] { "carrotB", "carrotA" }, new[] { "carrotA", "seedlingB" }, new[] { "seedlingB" } },
            new[] { new[] { "lavender" }, new[] { "rosemary", "daisies" }, new[] { "daisies", "oregano" }, new[] { "oregano", "daisies" } },
            new[] { new[] { "chives" }, new[] { "basil", "purpleBasil" }, new[] { "thyme", "basil" }, new[] { "seedlingC", "thyme" } },
        };
        static readonly float[] RowZ = { 0.60f, 0.21f, -0.20f, -0.60f };
        const float PlantScale = 1.6f;   // the painting's plants at the size that reads from the high camera (a lettuce about half a metre)

        /// <summary>Planting slots of a bed: (plant, bed-local x, z, scale), deterministic per bed.</summary>
        static List<(string plant, float x, float z, float scale)> BedPlanting(int bed)
        {
            var slots = new List<(string, float, float, float)>();
            var rng = new System.Random(707 + bed);
            for (int row = 0; row < RowZ.Length; row++)
            {
                string[] mix = BedRows[bed][row];
                bool wide = mix[0] == "lavender";
                float[] xs = wide ? new[] { -0.85f, 0.1f, 0.98f } : new[] { -1.16f, -0.58f, 0f, 0.58f, 1.16f };
                for (int k = 0; k < xs.Length; k++)
                {
                    float jx = (float)(rng.NextDouble() - 0.5) * 0.14f, jz = (float)(rng.NextDouble() - 0.5) * 0.08f;
                    slots.Add((mix[(k + row) % mix.Length], xs[k] + jx, RowZ[row] + jz, PlantScale * (0.9f + 0.2f * (float)rng.NextDouble())));
                }
            }
            return slots;
        }

        /// <summary>
        /// The garden on its real layout: 3D raised beds (chunky boards, corner posts with rope, painted timber) with their own colliders
        /// (B4's frames had none, so the player walked through the beds); the soil boxes take the painted tilled soil (dry while dormant,
        /// rich in bloom); the plants are cut from the owner's painting and stand on the beds. Lush plants live under the garden's Bloom root
        /// and withered ones under its Dormant root, so ResonanceView's own switch (stage 2) decides which the player sees, and the bloom
        /// pops up bed by bed with the stage transition. The low-poly stalks, flowers, bushes and hedges are hidden.
        /// </summary>
        static void Garden(Assets A, Lists L, Transform garden, SerializedObject stages)
        {
            var dormant = ((GameObject)stages.FindProperty("gardenDormant").objectReferenceValue).transform;
            var bloom = ((GameObject)stages.FindProperty("gardenBloomRoot").objectReferenceValue).transform;
            foreach (Transform child in garden)
                if (child.name.StartsWith("BedFrame")) L.HideAll(child);
            foreach (Transform child in dormant)
            {
                var r = child.GetComponent<Renderer>();
                if (child.name.StartsWith("DrySoil")) L.Restyle(r, A.soilDry);
                else L.HideAll(child);
            }
            foreach (Transform child in bloom)
            {
                var r = child.GetComponent<Renderer>();
                if (child.name.StartsWith("RichSoil")) L.Restyle(r, A.soilRich);
                else L.HideAll(child);
            }
            foreach (string part in new[] { "GardenFence", "GateArch" })
            {
                Transform t = garden.Find(part);
                if (t != null) foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) L.Restyle(r, A.wood);
            }
            Transform scarecrow = garden.Find("Scarecrow");
            if (scarecrow != null)
            {
                foreach (var r in scarecrow.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Coat" || r.name == "Sleeves") L.Restyle(r, A.cloth);
                    else if (r.name == "Pole" || r.name == "Arms") L.Restyle(r, A.wood);
                // with the beds solid the paths cross at the scarecrow: its pole blocks too
                var pole = new GameObject("ScarecrowPoleB5");
                pole.transform.SetParent(scarecrow, false);
                var capsule = pole.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 1f, 0f);
                capsule.height = 2f;
                capsule.radius = 0.12f;
                L.colliders.Add(capsule);
            }

            // 3D raised beds with collision
            var beds = new GameObject("B5_GardenBeds").transform;
            beds.SetParent(garden, false);
            L.b5Only.Add(beds.gameObject);
            for (int i = 0; i < BedCentres.Length; i++)
            {
                var bed = new GameObject("Bed" + i).transform;
                bed.SetParent(beds, false);
                bed.localPosition = new Vector3(BedCentres[i].x, 0f, BedCentres[i].y);
                float hx = BedSize.x * 0.5f - 0.06f, hz = BedSize.z * 0.5f - 0.06f, top = BedSize.y;
                Pieces.Prim(bed, PrimitiveType.Cube, "BoardNorth", new Vector3(0f, top * 0.5f, hz), new Vector3(BedSize.x, top, 0.12f), A.wood);
                Pieces.Prim(bed, PrimitiveType.Cube, "BoardSouth", new Vector3(0f, top * 0.5f, -hz), new Vector3(BedSize.x, top, 0.12f), A.wood);
                Pieces.Prim(bed, PrimitiveType.Cube, "BoardEast", new Vector3(hx, top * 0.5f, 0f), new Vector3(0.12f, top, BedSize.z - 0.24f), A.wood);
                Pieces.Prim(bed, PrimitiveType.Cube, "BoardWest", new Vector3(-hx, top * 0.5f, 0f), new Vector3(0.12f, top, BedSize.z - 0.24f), A.wood);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Pieces.Prim(bed, PrimitiveType.Cube, "Post", new Vector3(sx * hx, 0.24f, sz * hz), new Vector3(0.2f, 0.48f, 0.2f), A.wood);
                        Pieces.Prim(bed, PrimitiveType.Cube, "Rope", new Vector3(sx * hx, 0.31f, sz * hz), new Vector3(0.225f, 0.07f, 0.225f), A.rope);
                    }
                var box = bed.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(BedSize.x + 0.04f, 0.5f, BedSize.z);
                L.colliders.Add(box);
            }

            // plants: lush under Bloom (one pop-up group per bed), withered under Dormant
            int lushCount = 0;
            for (int i = 0; i < BedCentres.Length; i++)
            {
                var lush = new Cards();
                var rng = new System.Random(808 + i);
                foreach (var s in BedPlanting(i))
                    AddPlant(lush, A, s.plant, false, new Vector3(s.x, BloomSoilTop, s.z), s.scale, rng.NextDouble() < 0.5, Tint(rng, 0.06f));
                lushCount += lush.Count;
                var bed = CardObject("B5_BloomBed" + i, bloom, lush.Save("GardenBloomBed" + i + "B5"), A.garden);
                bed.transform.localPosition = new Vector3(BedCentres[i].x, 0f, BedCentres[i].y);
                L.b5Only.Add(bed);
            }
            var dry = new Cards();
            for (int i = 0; i < BedCentres.Length; i++)
            {
                var rng = new System.Random(909 + i);
                foreach (var s in BedPlanting(i).Where((_, k) => k % 2 == 0))
                {
                    float jx = (float)(rng.NextDouble() - 0.5) * 0.12f, jz = (float)(rng.NextDouble() - 0.5) * 0.08f;
                    var at = new Vector3(BedCentres[i].x + s.x + jx, DormantSoilTop, BedCentres[i].y + s.z + jz);
                    AddPlant(dry, A, s.plant, true, at, s.scale * (0.8f + 0.15f * (float)rng.NextDouble()), rng.NextDouble() < 0.5, Tint(rng, 0.05f));
                }
            }
            L.b5Only.Add(CardObject("B5_DormantBeds", dormant, dry.Save("GardenDormantBedsB5"), A.garden));

            // hedges outside the fence (Phase3Pieces' hedge spots) and the gate arch: painted clumps, withered while dormant
            Vector3[] hedges =
            {
                new Vector3(-4.7f, 0f, 3.7f), new Vector3(4.7f, 0f, 3.7f), new Vector3(-4.7f, 0f, -3.7f), new Vector3(4.7f, 0f, -3.7f),
                new Vector3(0f, 0f, 3.9f), new Vector3(0f, 0f, -3.9f), new Vector3(4.9f, 0f, 0f),
                new Vector3(-4.15f, 0f, -1.3f), new Vector3(-4.15f, 0f, 1.3f),   // at the arch posts
            };
            var green = new Cards();
            var straw = new Cards();
            var hr = new System.Random(4242);
            foreach (Vector3 h in hedges)
                for (int k = 0; k < 3; k++)
                {
                    var at = h + new Vector3((float)(hr.NextDouble() - 0.5) * 0.9f, 0f, (float)(hr.NextDouble() - 0.5) * 0.7f);
                    int kind = k == 0 ? 2 : k == 1 ? 0 : 1;
                    AddFoliage(green, A, kind, at, 0.62f + 0.3f * (float)hr.NextDouble(), hr.NextDouble() < 0.5, Tint(hr, 0.06f));
                    if (k < 2) AddFoliage(straw, A, 0, at, 0.42f + 0.18f * (float)hr.NextDouble(), hr.NextDouble() < 0.5, new Color(0.86f, 0.70f, 0.42f));
                }
            L.b5Only.Add(CardObject("B5_BloomHedges", bloom, green.Save("GardenBloomHedgesB5"), A.foliage));
            L.b5Only.Add(CardObject("B5_DormantHedges", dormant, straw.Save("GardenDormantHedgesB5"), A.foliage));
            Debug.Log("[Echoes][P52 B5] garden: " + BedCentres.Length + " 3D beds with colliders, " + lushCount + " painted plants in bloom, " + dry.Count + " withered");
        }

        static Color Tint(System.Random rng, float spread)
        {
            float v = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * spread;
            return new Color(v * (1f + ((float)rng.NextDouble() - 0.5f) * spread), v, v * (1f - ((float)rng.NextDouble()) * spread));
        }

        // ------------------------------------------------------------------------------------------------ ground plants

        /// <summary>The B4 foliage zones (Phase52B4Builder.InFoliageZone): B4's 900 painted clumps already cover them.</summary>
        static bool B4FoliageZone(Vector2 p)
        {
            float plaza = p.magnitude;
            return (plaza > 9.6f && plaza < 15.5f)
                || Vector2.Distance(p, ValleyLayout.Cottage) < 11f
                || Vector2.Distance(p, new Vector2(14.5f, 11f)) < 6.5f
                || Vector2.Distance(p, ValleyLayout.Spring) < 8.5f
                || ValleyLayout.DistanceToPaths(p) < 4.5f && p.x > 5f && p.x < 22f && p.y > -2f && p.y < 10f;
        }

        /// <summary>
        /// Painted ground plants for the rest of the visible valley (the paths, the garden's and the hen yard's surroundings, the stream banks
        /// near the spring, the windmill knoll): the B4 foliage atlas with a little tint variation and three sizes, kept off paths, gameplay
        /// spots, interaction points, trunks, beds and water. The low-poly tufts, flower patches and bank bushes they replace are hidden. The
        /// stage-1 and stage-3 flower patches, the hen yard's pots and the stage-1 saplings by the spring get painted flower clumps that pop
        /// up with their stage.
        /// </summary>
        static void Foliage(Assets A, Lists L, Terrain terrain, SerializedObject stages, Vector2[] springFootprint, List<Vector3> trunks, Phase52B4Pilot b4)
        {
            // what the painted plants replace
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.name.StartsWith("Plant") && r.transform.parent != null && r.transform.parent.name == "Phase52_RouteOnly") L.Hide(r);
            }
            foreach (string group in new[] { "World/Flowers" })
            {
                GameObject g = GameObject.Find(group);
                if (g != null) L.HideAll(g.transform);
            }
            GameObject windmill = GameObject.Find("World/WindmillPilot");
            var lowPolyFlowerSpots = new List<Vector3>();
            if (windmill != null)
                foreach (Transform t in windmill.transform)
                    if (t.name == "Broadleaf" || t.name == "Flowers") { L.HideAll(t); if (t.name == "Flowers") lowPolyFlowerSpots.Add(t.position); }
                    else if (t.name == "Planter") foreach (Transform c in t) if (c.name == "Flowers") L.HideAll(c);
            GameObject banks = GameObject.Find("World/StreamBankDetails");
            if (banks != null) foreach (Transform t in banks.transform) if (t.name == "Broadleaf") L.HideAll(t);
            GameObject flowers = GameObject.Find("World/Flowers");
            if (flowers != null) foreach (Transform t in flowers.transform) lowPolyFlowerSpots.Add(t.position);

            var interactables = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Select(i => new Vector2(i.Position.x, i.Position.z)).ToArray();
            Vector2 garden = ValleyLayout.Garden;
            bool InGarden(Vector2 p, float margin) => Mathf.Abs(p.x - garden.x) < 4.0f + margin && Mathf.Abs(p.y - garden.y) < 3.0f + margin;
            bool Zone(Vector2 p)
            {
                if (Mathf.Abs(p.x) > 44f || Mathf.Abs(p.y) > 44f) return false;
                float paths = Mathf.Min(ValleyLayout.DistanceToPaths(p), Phase52EnvironmentBuilder.Distance(p));
                return paths < 6.5f
                    || InGarden(p, 5.5f)
                    || Vector2.Distance(p, ValleyLayout.HenYard) < 7.5f
                    || Vector2.Distance(p, ValleyLayout.Spring) < 12.5f
                    || Vector2.Distance(p, ValleyLayout.Windmill) < 12f
                    || ValleyLayout.DistanceToPolyline(p, ValleyLayout.Stream) < 3.6f && Vector2.Distance(p, ValleyLayout.Spring) < 22f;
            }
            bool Free(Vector2 p)
            {
                if (ValleyLayout.DistanceToPaths(p) < 2.7f || Phase52EnvironmentBuilder.Distance(p) < 2.7f) return false;   // the painted dirt is ~2.5 m wide either side
                if (ValleyLayout.NearGameplaySpot(p, 1.5f) && !InGarden(p, 5.5f)) return false;
                if (interactables.Any(i => Vector2.Distance(i, p) < 1.8f)) return false;
                if (InGarden(p, 0.6f) || Vector2.Distance(p, ValleyLayout.HenYard) < 3.4f) return false;
                if (Inside(springFootprint, p, 0.5f)) return false;
                if (trunks.Any(t => Vector2.Distance(new Vector2(t.x, t.z), p) < 1.0f)) return false;
                if (ValleyLayout.DistanceToPolyline(p, ValleyLayout.Stream) < 1.7f) return false;
                if (Vector2.Distance(p, ValleyLayout.Pond) < ValleyLayout.PondRadius + 1.5f) return false;
                if (p.magnitude < 9.4f) return false;
                if (Vector2.Distance(p, ValleyLayout.Windmill) < 4.2f) return false;
                return true;
            }
            var cottage = UnityEngine.Object.FindObjectsByType<StaticProp25D>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Cottage");
            Vector2[] cottageFootprint = cottage == null ? null : new[] { Flat(cottage.BaseLeft), Flat(cottage.BaseFront), Flat(cottage.BaseRight), Flat(cottage.BaseLeft + cottage.BaseRight - cottage.BaseFront) };

            var ground = new Cards();
            var rng = new System.Random(5205);
            int count = 0;
            for (int attempt = 0; attempt < 40000 && count < 1400; attempt++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 88 - 44), (float)(rng.NextDouble() * 88 - 44));
                if (!Zone(p) || B4FoliageZone(p) || !Free(p)) continue;
                if (cottageFootprint != null && Inside(cottageFootprint, p, 0.6f)) continue;
                // denser beside the paths, sparser in open meadow
                float paths = Mathf.Min(ValleyLayout.DistanceToPaths(p), Phase52EnvironmentBuilder.Distance(p));
                if (paths > 3.5f && rng.NextDouble() < 0.45) continue;
                double roll = rng.NextDouble();
                int kind = roll < 0.48 ? 0 : roll < 0.78 ? 1 : 2;
                float size = kind == 1 ? 0.40f + 0.18f * (float)rng.NextDouble() : 0.48f + 0.32f * (float)rng.NextDouble();
                if (rng.NextDouble() < 0.1) size *= 1.35f;   // a few big clumps: plants of three sizes, not one
                float dry = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.70f, Mathf.PerlinNoise(p.x * 0.045f + 17.3f, p.y * 0.045f + 4.1f)));
                var tint = Color.Lerp(Tint(rng, 0.07f), new Color(1.06f, 0.98f, 0.80f), dry * 0.6f);
                var at = new Vector3(p.x, 0f, p.y);
                at.y = Height(terrain, at);
                AddFoliage(ground, A, kind, at, size, rng.NextDouble() < 0.5, tint);
                count++;
            }
            // painted flowers where the low-poly flower patches stood, a few more along the garden path
            var accents = new Cards();
            foreach (Vector3 spot in lowPolyFlowerSpots)
                for (int k = 0; k < 3; k++)
                {
                    var at = spot + new Vector3((float)(rng.NextDouble() - 0.5) * 0.9f, 0f, (float)(rng.NextDouble() - 0.5) * 0.9f);
                    at.y = Height(terrain, at);
                    if (k == 0) AddPlant(accents, A, "lavender", false, at, 0.6f + 0.25f * (float)rng.NextDouble(), rng.NextDouble() < 0.5, Tint(rng, 0.05f));
                    else AddFoliage(ground, A, 1, at, 0.42f + 0.16f * (float)rng.NextDouble(), rng.NextDouble() < 0.5, Tint(rng, 0.05f));   // the foliage atlas: its own mesh
                }
            L.b5Only.Add(CardObject("B5_GroundFoliage", null, ground.Save("GroundFoliageB5"), A.foliage));
            L.b5Only.Add(CardObject("B5_GroundFlowers", null, accents.Save("GroundFlowersB5"), A.garden));

            // stage clumps: popped up with their stage by ResonanceView (the cards scale with the patch)
            var wild = new Cards();
            AddFoliage(wild, A, 1, new Vector3(-0.12f, 0f, 0.05f), 0.34f, false, Color.white);
            AddFoliage(wild, A, 1, new Vector3(0.14f, 0f, -0.08f), 0.30f, true, new Color(1.05f, 0.98f, 1f));
            AddFoliage(wild, A, 0, new Vector3(0.02f, 0f, 0.16f), 0.30f, false, Color.white);
            Mesh wildMesh = wild.Save("ClumpWildflowersB5");
            var daisy = new Cards();
            AddPlant(daisy, A, "daisies", false, new Vector3(-0.1f, 0f, 0f), 0.9f, false, Color.white);
            AddPlant(daisy, A, "lavender", false, new Vector3(0.12f, 0f, 0.1f), 0.45f, true, Color.white);
            Mesh daisyMesh = daisy.Save("ClumpDaisiesB5");
            var potFlowers = new Cards();
            AddPlant(potFlowers, A, "daisies", false, Vector3.zero, 0.75f, false, Color.white);
            Mesh potMesh = potFlowers.Save("ClumpPotFlowersB5");
            var springBloom = new Cards();   // in place of a 0.55-scale sapling: about a metre of flowers and fern
            AddPlant(springBloom, A, "lavender", false, new Vector3(0f, 0f, 0.2f), 1.5f, false, Color.white);
            AddPlant(springBloom, A, "daisies", false, new Vector3(-0.5f, 0f, -0.2f), 1.9f, true, Color.white);
            AddPlant(springBloom, A, "rosemary", false, new Vector3(0.55f, 0f, -0.15f), 1.3f, false, Color.white);
            Mesh springBloomMesh = springBloom.Save("ClumpSpringBloomB5");

            void Patch(Transform t, Mesh mesh, Material material)
            {
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) L.Hide(r);
                L.b5Only.Add(CardObject("B5_PaintedClump", t, mesh, material));
            }
            var streamBloom = ((GameObject)stages.FindProperty("bloomRoot").objectReferenceValue).transform;
            foreach (Transform t in streamBloom)
                if (t.name.StartsWith("Tree_Blossom")) Patch(t, springBloomMesh, A.garden);
                else if (t.name.StartsWith("Flowers")) Patch(t, (t.GetSiblingIndex() % 2) == 0 ? wildMesh : daisyMesh, (t.GetSiblingIndex() % 2) == 0 ? A.foliage : A.garden);
            var village = ((GameObject)stages.FindProperty("villageLifeRoot").objectReferenceValue).transform;
            foreach (Transform t in village)
                if (t.name.StartsWith("Flowers")) Patch(t, (t.GetSiblingIndex() % 2) == 0 ? daisyMesh : wildMesh, (t.GetSiblingIndex() % 2) == 0 ? A.garden : A.foliage);
            var yard = ((GameObject)stages.FindProperty("yardLifeRoot").objectReferenceValue).transform;
            foreach (Transform t in yard)
            {
                if (t.name.StartsWith("YardFlower")) Patch(t, potMesh, A.garden);
                else if (t.name.StartsWith("Pot"))
                    foreach (Transform c in t)
                        if (c.name.StartsWith("Flower")) { foreach (var r in c.GetComponentsInChildren<Renderer>(true)) L.Hide(r); }
                if (t.name.StartsWith("Pot"))
                {
                    var flowersInPot = CardObject("B5_PotFlowers", t, potMesh, A.garden);
                    flowersInPot.transform.localPosition = new Vector3(0f, 0.34f, 0f);
                    L.b5Only.Add(flowersInPot);
                }
            }
            Debug.Log("[Echoes][P52 B5] ground plants: " + count + " painted clumps outside the B4 zones, " + accents.Count + " painted flowers at the old flower patches");
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        // ------------------------------------------------------------------------------------------------ gathering spots

        /// <summary>Herbs and Dawn Blossoms keep their ResourceNode (ready / depleted switch, range, timer); only their visuals become painted.</summary>
        static void Nodes(Assets A, Lists L)
        {
            var herb = new Cards();
            AddPlant(herb, A, "basil", false, Vector3.zero, 1.35f, false, new Color(1.06f, 1.08f, 0.98f));
            AddPlant(herb, A, "oregano", false, new Vector3(0.18f, 0f, 0.12f), 1.1f, true, Color.white);
            Mesh herbMesh = herb.Save("NodeHerbB5");
            var herbCut = new Cards();
            AddPlant(herbCut, A, "seedlingA", false, Vector3.zero, 1.2f, false, new Color(0.9f, 0.92f, 0.85f));
            Mesh herbCutMesh = herbCut.Save("NodeHerbCutB5");
            var blossom = new Cards();
            // the Dawn Blossom: the painted daisies tinted coral, larger than the wild ones, so a gatherable blossom still reads at a glance
            AddPlant(blossom, A, "daisies", false, Vector3.zero, 1.6f, false, new Color(1.28f, 0.72f, 0.64f));
            AddPlant(blossom, A, "daisies", false, new Vector3(0.18f, 0f, 0.12f), 1.25f, true, new Color(1.25f, 0.68f, 0.62f));
            Mesh blossomMesh = blossom.Save("NodeBlossomB5");
            var blossomCut = new Cards();
            AddPlant(blossomCut, A, "seedlingB", false, Vector3.zero, 1.1f, false, Color.white);
            Mesh blossomCutMesh = blossomCut.Save("NodeBlossomCutB5");
            int herbs = 0, blossoms = 0;
            foreach (var node in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool isHerb = node.ItemId == ItemIds.Herb, isFlower = node.ItemId == ItemIds.Flower;
                if (!isHerb && !isFlower) continue;
                Transform ready = node.transform.Find("Ready"), depleted = node.transform.Find("Depleted");
                if (ready == null || depleted == null) continue;
                foreach (var r in ready.GetComponentsInChildren<Renderer>(true)) L.Hide(r);
                foreach (var r in depleted.GetComponentsInChildren<Renderer>(true)) L.Hide(r);
                L.b5Only.Add(CardObject("B5_Ready", ready, isHerb ? herbMesh : blossomMesh, A.garden));
                L.b5Only.Add(CardObject("B5_Depleted", depleted, isHerb ? herbCutMesh : blossomCutMesh, A.garden));
                if (isHerb) herbs++; else blossoms++;
            }
            Debug.Log("[Echoes][P52 B5] gathering spots: " + herbs + " herbs and " + blossoms + " Dawn Blossoms painted (ResourceNode unchanged)");
        }

        // ------------------------------------------------------------------------------------------------ the Heart's crystal

        /// <summary>The Heart's floating crystal: the B1 cube becomes a cut crystal (a long hexagonal bipyramid) on the same renderer, so
        /// CompletionFeedback's glow and spin are unchanged. The mesh carries the inverse of the cube's 45/0/45 tilt, so it stands upright.</summary>
        static void HeartCrystal(Lists L)
        {
            Transform crystal = GameObject.Find("HeartOfTheValley/CrystalPivot/Crystal")?.transform;
            if (crystal == null) return;
            var filter = crystal.GetComponent<MeshFilter>();
            Quaternion undo = Quaternion.Inverse(crystal.localRotation);
            var top = new Vector3(0f, 1.15f, 0f);
            var bottom = new Vector3(0f, -0.85f, 0f);
            var ring = Enumerable.Range(0, 6).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI / 3f) * 0.48f, 0.12f * ((i % 2) * 2 - 1) * 0.3f, Mathf.Sin(i * Mathf.PI / 3f) * 0.48f)).ToArray();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c) { int o = verts.Count; verts.Add(undo * a); verts.Add(undo * b); verts.Add(undo * c); tris.AddRange(new[] { o, o + 1, o + 2 }); }
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 6];
                Face(top, b, a);
                Face(bottom, a, b);
            }
            string path = Folder + "/Meshes/HeartCrystalB5.asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "HeartCrystalB5", vertices = verts.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            L.meshes.Add(new Phase52B5Pilot.MeshSwap { filter = filter, before = filter.sharedMesh, b5 = mesh });
        }

        // ------------------------------------------------------------------------------------------------ plaza and B1 details

        /// <summary>The plaza's fieldstone (B4 restyled the Heart's primitives with the cobble): the same, warmer, so the stone and the meadow
        /// share one light instead of a blue-grey plaza in a yellow-green valley.</summary>
        static void Plaza(Assets A, Lists L, Phase52B4Pilot b4)
        {
            for (int i = 0; i < b4.RestyledInB4.Length && i < b4.RestyleB4.Length; i++)
            {
                Material b4Material = b4.RestyleB4[i];
                if (b4.RestyledInB4[i] == null || b4Material == null) continue;
                Material warm = A.plazaStone(b4Material.name);
                if (warm != null) L.Restyle(b4.RestyledInB4[i], warm);
            }
        }

        /// <summary>
        /// The B1 scenery details the high camera looks down on: the 520 low-poly grass tufts (dark teal blades) give way to the painted
        /// ground plants, the pale path stones go (the painted dirt already has its pebbles), the reeds and cattails along the stream near the
        /// spring go (painted bank plants stand there), and the boulders take the painted stone without their flat moss caps.
        /// </summary>
        static void Details(Assets A, Lists L, SerializedObject stages)
        {
            GameObject details = GameObject.Find("World/Details");
            if (details == null) return;
            foreach (string group in new[] { "GrassTufts", "PathStones" })
            {
                Transform t = details.transform.Find(group);
                if (t != null) L.HideAll(t);
            }
            Transform reeds = details.transform.Find("Reeds");
            if (reeds != null)
                foreach (Transform t in reeds)
                    if (Vector2.Distance(new Vector2(t.position.x, t.position.z), ValleyLayout.Spring) < 26f) L.HideAll(t);
            // the hen yard's fence and the stage-3 lantern posts in the same warm timber as the garden's
            GameObject henFence = GameObject.Find("World/HenYardFence");
            if (henFence != null) foreach (var r in henFence.GetComponentsInChildren<MeshRenderer>(true)) L.Restyle(r, A.wood);
            var village = (GameObject)stages.FindProperty("villageLifeRoot").objectReferenceValue;   // inactive until stage 3: not found by name
            if (village != null)
                foreach (Transform post in village.transform)
                    if (post.name.StartsWith("LanternPost"))
                        foreach (var r in post.GetComponentsInChildren<MeshRenderer>(true))
                            if (r.name == "Pole" || r.name == "Arm") L.Restyle(r, A.wood);
            GameObject rocks = GameObject.Find("World/Rocks");
            if (rocks == null) return;
            var cards = new Cards();
            Plant rock = A.P("rockA");
            float aspect = (float)rock.lush.w / rock.lush.h;
            foreach (Transform t in rocks.transform)
            {
                var renderers = t.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToArray();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                foreach (var r in renderers) L.Hide(r);
                // the painting covers about nine tenths of the boulder's collider across, so the player is not stopped by thin air
                float width = Mathf.Max(bounds.size.x, bounds.size.z) * 0.9f;
                int h = Hash(t.position);
                float v = 1.04f + 0.12f * ((h >> 4) % 100) / 100f;   // a little lighter than the painting: it stands in open meadow
                var tint = new Color(v * (0.97f + 0.06f * ((h >> 11) % 100) / 100f), v, v * (0.92f + 0.06f * ((h >> 17) % 100) / 100f));
                Vector3 foot = t.position;
                foot.y = bounds.min.y + 0.12f;
                cards.Add(foot, PlantRect(A, rock.lush), width, width / aspect, new Vector2(0.5f, 0.1f), (h & 1) == 1, tint, false);
            }
            L.b5Only.Add(CardObject("B5_PaintedRocks", null, cards.Save("PaintedRocksB5"), A.garden));
            Debug.Log("[Echoes][P52 B5] rocks: " + cards.Count + " painted boulders on the old boulders' colliders");
        }

        // ------------------------------------------------------------------------------------------------ terrain

        /// <summary>
        /// A copy of the B4 terrain (heights untouched, so collision is the same) with six layers instead of nine: every grass layer becomes
        /// the painted meadow (large drier patches by a slow noise), every dirt layer the B2 painted dirt, stone and the plaza cobble stay; the
        /// garden's walkways become tilled soil and trodden earth; a ring of damp earth meets the painted spring's rim.
        /// </summary>
        static TerrainData B5TerrainData(TerrainData b4Data, Assets A, Vector3 origin, Vector2[] springFootprint, Vector3 gardenCentre)
        {
            AssetDatabase.DeleteAsset(TerrainPath);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(b4Data), TerrainPath)) throw new IOException("Could not copy the B4 terrain.");
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            TerrainLayer[] old = data.terrainLayers;
            int Index(string name) => Array.FindIndex(old, l => l != null && l.name == name);
            int grass = Index("Grass"), meadow = Index("Meadow"), path = Index("Path"), stone = Index("Stone"), trail = Index("WindmillTrail"),
                pilotMeadow = Index("PilotMeadow"), routeMeadow = Index("RouteMeadow"), routeDirt = Index("RouteDirt"), cobble = Index("PlazaCobbleB4");
            if (grass < 0 || meadow < 0 || path < 0 || stone < 0 || routeDirt < 0 || cobble < 0) throw new InvalidDataException("Unexpected B4 terrain layers.");
            int w = data.alphamapWidth, h = data.alphamapHeight;
            float[,,] src = data.GetAlphamaps(0, 0, w, h);
            float Get(int z, int x, int layer) => layer < 0 ? 0f : src[z, x, layer];
            var layers = new[] { A.meadowA, A.meadowB, old[routeDirt], old[stone], A.cobble, A.soil };
            data.terrainLayers = layers;
            var map = new float[h, w, layers.Length];
            Vector2 garden = new Vector2(gardenCentre.x, gardenCentre.z);
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(origin.x + (x + 0.5f) / w * data.size.x, origin.z + (z + 0.5f) / h * data.size.z);
                    float g = Get(z, x, grass) + Get(z, x, meadow) + Get(z, x, pilotMeadow) + Get(z, x, routeMeadow);
                    float d = Get(z, x, routeDirt) + Get(z, x, path) + Get(z, x, trail);
                    float s = Get(z, x, stone), c = Get(z, x, cobble), soil = 0f;
                    float drier = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.70f, Mathf.PerlinNoise(p.x * 0.045f + 17.3f, p.y * 0.045f + 4.1f))) * 0.8f;
                    float a = g * (1f - drier), b = g * drier;
                    // the garden inside its fence: tilled soil with trodden earth
                    float inX = Mathf.InverseLerp(4.0f, 3.6f, Mathf.Abs(p.x - garden.x)), inZ = Mathf.InverseLerp(3.0f, 2.6f, Mathf.Abs(p.y - garden.y));
                    float gard = Mathf.SmoothStep(0f, 1f, Mathf.Min(inX, inZ));
                    if (gard > 0f) { float keep = 1f - gard; a *= keep; b *= keep; s *= keep; c *= keep; d = d * keep + gard * 0.45f; soil = gard * 0.55f; }
                    // damp earth meeting the painted spring's rim: a ragged band under a metre wide, not a pad the shape of the footprint
                    float ragged = 0.45f + 0.75f * Mathf.PerlinNoise(p.x * 1.3f + 3.7f, p.y * 1.3f + 9.2f);
                    float ring = Inside(springFootprint, p, 0f) ? 0.5f : 0.45f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, ragged, DistanceToPolygon(springFootprint, p))));
                    if (ring > 0f) { float keep = 1f - ring; a *= keep; b *= keep; s *= keep; c *= keep; d *= keep; soil = soil * keep + ring; }
                    float sum = a + b + d + s + c + soil;
                    if (sum < 1e-3f) { a = 1f; sum = 1f; }
                    map[z, x, 0] = a / sum; map[z, x, 1] = b / sum; map[z, x, 2] = d / sum; map[z, x, 3] = s / sum; map[z, x, 4] = c / sum; map[z, x, 5] = soil / sum;
                }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        // ------------------------------------------------------------------------------------------------ minimap

        /// <summary>
        /// The B4 minimap's baked image, made again from the B5 valley (Phase52B4Builder.BakeMinimap's method): an orthographic north-up render
        /// of the terrain (with the new layers), the paths, the water, the beds and the 3D buildings; painted cards, characters, particles,
        /// clouds and fog are left out, and the hidden 3D trees, house and spring are drawn for it, so the map shows every landmark.
        /// </summary>
        static void BakeMinimap(Phase52B4Pilot b4, Phase52B5Pilot b5, List<Renderer> trees)
        {
            var off = new List<Renderer>();
            string[] cardShaders = { "Echoes/StaticProp25D", "Echoes/StageProp25D", "Echoes/PaintedCard25D", "Echoes/Foliage25D", "Echoes/Sprite25D", "Echoes/ContactShadow" };
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                bool card = r is ParticleSystemRenderer || r.GetComponent<Sprite25DQuad>() != null
                    || r.sharedMaterial != null && r.sharedMaterial.shader != null && cardShaders.Contains(r.sharedMaterial.shader.name);
                if (card && r.enabled) { r.enabled = false; off.Add(r); }
            }
            GameObject clouds = GameObject.Find("Clouds");
            if (clouds != null) foreach (Renderer r in clouds.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; off.Add(r); }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var restored = new List<Renderer>();
            foreach (Renderer r in b4.HiddenInB4.Concat(trees)) if (r != null && !r.enabled && !(r is ParticleSystemRenderer) && !off.Contains(r)) { r.enabled = true; restored.Add(r); }
            var shadowModes = b4.ShadowOnlyInB4.Concat(b5.ShadowOnlyInB5).Where(r => r != null).Distinct().ToDictionary(r => r, r => r.shadowCastingMode);
            foreach (Renderer r in shadowModes.Keys) r.shadowCastingMode = ShadowCastingMode.On;
            Vector4 bounds = Phase52B4Builder.MapBounds;
            var camGo = new GameObject("MinimapBakeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = bounds.w * 0.5f;
            cam.aspect = bounds.z / bounds.w;
            cam.transform.position = new Vector3(bounds.x + bounds.z * 0.5f, 120f, bounds.y + bounds.w * 0.5f);
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
            Color32[] px = image.GetPixels32();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color c = px[y * size + x];
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    c = Color.Lerp(new Color(lum, lum, lum), c, 1.2f);
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

        // ------------------------------------------------------------------------------------------------ build

        public static void BuildWindowsPilot()
        {
            string output = Argument("-echoesBuildPath");
            if (string.IsNullOrEmpty(output) || Directory.Exists(output)) throw new IOException("Use a fresh explicit -echoesBuildPath; existing builds are preserved.");
            Directory.CreateDirectory(output);
            string version = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = Version;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Menu, Valley }, locationPathName = Path.Combine(output, "EchoesOfTheValley.exe"), target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None });
                var s = report.summary;
                Debug.Log("[Echoes][P52 B5] PILOT_BUILD " + s.result + " id=" + s.guid + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " bytes=" + s.totalSize + "; owner approval PENDING, not a release candidate.");
                if (s.result != BuildResult.Succeeded) throw new Exception("B5 pilot build failed.");
            }
            finally { PlayerSettings.bundleVersion = version; }
        }

        static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }
}
