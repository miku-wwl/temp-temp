using System;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Presentation switch of the isolated Phase 5.2-B4 pilot scene (no original, B2 or B3 scene contains it). B4 adds the high fixed
    /// camera, the painted static props, ground foliage, the cobbled plaza, the minimap and the B4 caretaker drawings; "before" mode
    /// ("-echoes-b4-before", or Apply(true) for matched evidence stills) restores the B3 presentation in the same scene and the same place.
    /// Gameplay objects, colliders, interactions and saves are identical in both modes.
    /// </summary>
    [DefaultExecutionOrder(-390)]
    public sealed class Phase52B4Pilot : MonoBehaviour
    {
        [Serializable]
        public struct CameraView { public float yaw, pitch, distance, minDistance, maxDistance, fieldOfView; }

        public GameObject[] B4Only = Array.Empty<GameObject>();
        public Renderer[] HiddenInB4 = Array.Empty<Renderer>();
        public Renderer[] ShadowOnlyInB4 = Array.Empty<Renderer>();
        public Collider[] DisabledInB4 = Array.Empty<Collider>();      // the old box house: its walls stood in front of the painted facade
        public Collider[] B4Colliders = Array.Empty<Collider>();       // the painted workshop's footprint
        public Renderer[] RestyledInB4 = Array.Empty<Renderer>();      // the plaza's stone primitives: painted fieldstone in B4
        public Material[] RestyleBefore = Array.Empty<Material>();
        public Material[] RestyleB4 = Array.Empty<Material>();
        public Terrain Terrain;
        public TerrainData BeforeTerrain;
        public TerrainData B4Terrain;
        public CharacterVisual2D5D Hero;
        public Renderer HeroCard;
        public SpriteSheet25D BeforeSheet;
        public SpriteSheet25D B4Sheet;
        public Material BeforeHeroMaterial;
        public Material B4HeroMaterial;
        public FixedFollowCamera25D FollowCamera;
        public CameraView BeforeView = new CameraView { yaw = 25f, pitch = 30f, distance = 8f, minDistance = 6f, maxDistance = 11f, fieldOfView = 35f };
        public CameraView B4View = new CameraView { yaw = 45f, pitch = 48f, distance = 14f, minDistance = 10f, maxDistance = 17f, fieldOfView = 35f };
        public RectTransform StatusPanel;
        public Vector2 BeforeStatusPosition;
        public Vector2 B4StatusPosition;

        public bool BeforeMode { get; private set; }

        void Start()
        {
            BeforeMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b4-before") >= 0;
            Apply(BeforeMode);
            Debug.Log("[Echoes][P52 B4] high-angle 2.5D pilot, before=" + BeforeMode + "; owner approval PENDING; world expansion disabled.");
        }

        public void Apply(bool before)
        {
            BeforeMode = before;
            foreach (GameObject go in B4Only) if (go != null) go.SetActive(!before);
            foreach (Renderer r in HiddenInB4) if (r != null) r.enabled = before;
            foreach (Collider c in DisabledInB4) if (c != null) c.enabled = before;
            foreach (Collider c in B4Colliders) if (c != null) c.enabled = !before;
            for (int i = 0; i < RestyledInB4.Length; i++)
                if (RestyledInB4[i] != null && i < RestyleBefore.Length && i < RestyleB4.Length)
                    RestyledInB4[i].sharedMaterial = before ? RestyleBefore[i] : RestyleB4[i];
            foreach (Renderer r in ShadowOnlyInB4)
                if (r != null)
                {
                    r.enabled = true;
                    r.shadowCastingMode = before ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            if (Terrain != null)
            {
                TerrainData data = before ? BeforeTerrain : B4Terrain;
                if (data != null)
                {
                    Terrain.terrainData = data;
                    TerrainCollider collider = Terrain.GetComponent<TerrainCollider>();
                    if (collider != null) collider.terrainData = data;
                }
            }
            if (Hero != null) Hero.UseSheet(before ? BeforeSheet : B4Sheet);
            if (HeroCard != null) HeroCard.sharedMaterial = before ? BeforeHeroMaterial : B4HeroMaterial;
            if (FollowCamera != null)
            {
                CameraView v = before ? BeforeView : B4View;
                FollowCamera.Configure(v.yaw, v.pitch, v.distance, v.minDistance, v.maxDistance, v.fieldOfView);
            }
            if (StatusPanel != null) StatusPanel.anchoredPosition = before ? BeforeStatusPosition : B4StatusPosition;
        }
    }
}
