using System;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Presentation switch of the isolated Phase 5.2-B5 pilot scene (a copy of the B4 scene; no original, B2, B3 or B4 scene contains it).
    /// B5 adds the stage-painted spring, the painted stream, painted trees, the 3D raised beds with painted plants, painted ground plants,
    /// the meadow / soil terrain and a re-baked minimap; "before" mode ("-echoes-b5-before", or Apply(true) for matched evidence stills)
    /// shows the B4 presentation in the same scene and the same place. Gameplay objects, interactions and saves are identical in both
    /// modes; the colliders B5 adds (the beds, the spring's painted footprint) and widens (tree trunks) follow the paintings.
    /// </summary>
    [DefaultExecutionOrder(-380)]
    public sealed class Phase52B5Pilot : MonoBehaviour
    {
        [Serializable] public struct MaterialSwap { public Renderer renderer; public Material before; public Material b5; }
        [Serializable] public struct MeshSwap { public MeshFilter filter; public Mesh before; public Mesh b5; }
        [Serializable] public struct CapsuleSwap { public CapsuleCollider collider; public float beforeRadius; public float b5Radius; }

        public GameObject[] B5Only = Array.Empty<GameObject>();
        public Renderer[] HiddenInB5 = Array.Empty<Renderer>();
        public Renderer[] ShadowOnlyInB5 = Array.Empty<Renderer>();      // old trees: their real shadow stays under the painted canopy
        public Collider[] B5Colliders = Array.Empty<Collider>();         // the raised beds, the spring's painted footprint
        public Collider[] DisabledInB5 = Array.Empty<Collider>();        // old spring stones that would stand outside the new painting
        public MaterialSwap[] Restyled = Array.Empty<MaterialSwap>();
        public MeshSwap[] Meshes = Array.Empty<MeshSwap>();
        public CapsuleSwap[] Trunks = Array.Empty<CapsuleSwap>();
        public Terrain Terrain;
        public TerrainData BeforeTerrain;
        public TerrainData B5Terrain;
        public UnityEngine.UI.RawImage MinimapImage;
        public Texture BeforeMinimap;
        public Texture B5Minimap;
        [Serializable] public struct MutedLight { public Color sun, ambientSky, ambientEquator, ambientGround, fog; }
        public ValleyLighting Lighting;
        public MutedLight BeforeLight;   // the B1-B4 silent valley: muted and blue
        public MutedLight B5Light;       // B5: muted and grey-green, not blue, so the painted meadow and stone keep their colour

        public bool BeforeMode { get; private set; }

        void Start()
        {
            BeforeMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b5-before") >= 0;
            Apply(BeforeMode);
            Debug.Log("[Echoes][P52 B5] painted environment pilot, before=" + BeforeMode + "; owner approval PENDING; world expansion disabled.");
        }

        public void Apply(bool before)
        {
            BeforeMode = before;
            foreach (GameObject go in B5Only) if (go != null) go.SetActive(!before);
            foreach (Renderer r in HiddenInB5) if (r != null) r.enabled = before;
            foreach (Renderer r in ShadowOnlyInB5)
                if (r != null)
                {
                    r.enabled = true;
                    r.shadowCastingMode = before ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            foreach (Collider c in B5Colliders) if (c != null) c.enabled = !before;
            foreach (Collider c in DisabledInB5) if (c != null) c.enabled = before;
            foreach (MaterialSwap s in Restyled) if (s.renderer != null) s.renderer.sharedMaterial = before ? s.before : s.b5;
            foreach (MeshSwap s in Meshes) if (s.filter != null) s.filter.sharedMesh = before ? s.before : s.b5;
            foreach (CapsuleSwap s in Trunks) if (s.collider != null) s.collider.radius = before ? s.beforeRadius : s.b5Radius;
            if (Terrain != null)
            {
                TerrainData data = before ? BeforeTerrain : B5Terrain;
                if (data != null)
                {
                    Terrain.terrainData = data;
                    TerrainCollider collider = Terrain.GetComponent<TerrainCollider>();
                    if (collider != null) collider.terrainData = data;
                }
            }
            if (MinimapImage != null) MinimapImage.texture = before ? BeforeMinimap : B5Minimap;
            if (Application.isPlaying) ApplyLighting(before);   // in the Editor the lighting would replace the scene's skybox with a copy
        }

        /// <summary>The silent valley's muted light for this presentation (ValleyLighting composes every stage from it).</summary>
        public void ApplyLighting(bool before)
        {
            if (Lighting == null) return;
            MutedLight m = before ? BeforeLight : B5Light;
            if (m.sun.a > 0f) Lighting.SetMutedBase(m.sun, m.ambientSky, m.ambientEquator, m.ambientGround, m.fog);
        }
    }
}
