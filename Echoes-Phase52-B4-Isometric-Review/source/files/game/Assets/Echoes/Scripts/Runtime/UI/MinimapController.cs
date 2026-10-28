using UnityEngine;
using UnityEngine.UI;

namespace Echoes
{
    /// <summary>
    /// The real minimap (Phase 5.2-B4): a north-up image of the valley baked from the terrain and buildings in the Editor, shown turned so
    /// that up on the map is up on the screen (the fixed camera's yaw). The map slides so the player stays in the middle; the player arrow
    /// turns with her facing; landmark icons ride on the map; the objective marker follows MinimapTargets and clamps to the edge when the
    /// target is out of view. Only UI transforms change per frame; nothing is rendered at runtime.
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        [SerializeField] RectTransform window;          // the clipped viewport inside the frame
        [SerializeField] RawImage map;
        [SerializeField] RectTransform playerArrow;
        [SerializeField] RectTransform objectiveMarker;
        [SerializeField] RectTransform objectiveEdgeArrow;
        [SerializeField] RectTransform[] landmarkIcons;
        [SerializeField] Vector3[] landmarkWorld;
        [SerializeField] Vector4 worldBounds;           // min x, min z, size x, size z of the baked image
        [SerializeField] float metresAcross = 46f;      // how much of the valley the window shows
        [SerializeField] float mapYaw = 45f;            // the fixed camera's yaw: world direction that points up on the screen
        [SerializeField] Transform player;
        [SerializeField] Transform mira, spring, heart, workshop, wren, hen, alder;

        float pixelsPerMetre;
        Vector3 objectiveWorld;

        public Vector2 PlayerMapPoint { get; private set; }
        public MinimapTargets.Kind Objective { get; private set; }
        public bool ObjectiveOnEdge { get; private set; }
        public Vector2 ObjectiveWindowPosition => objectiveMarker != null ? objectiveMarker.anchoredPosition : Vector2.zero;
        public float MapYaw => mapYaw;
        public Vector4 WorldBounds => worldBounds;
        public RectTransform Window => window;
        public RawImage Map => map;
        public RectTransform PlayerArrow => playerArrow;

        void Start() => Refresh();
        void LateUpdate() => Refresh();

        /// <summary>World X/Z to the map image's local position (unrotated, centred), in UI units.</summary>
        public Vector2 WorldToMap(Vector3 world)
        {
            return new Vector2((world.x - worldBounds.x) / worldBounds.z - 0.5f, (world.z - worldBounds.y) / worldBounds.w - 0.5f) * (pixelsPerMetre * worldBounds.z);
        }

        /// <summary>A map-local position to the window's local position, given the current scroll.</summary>
        public Vector2 MapToWindow(Vector2 mapLocal) => (Vector2)(Quaternion.Euler(0f, 0f, mapYaw) * mapLocal) + map.rectTransform.anchoredPosition;

        public void Refresh()
        {
            if (window == null || map == null || player == null || worldBounds.z <= 0f) return;
            pixelsPerMetre = window.rect.width / Mathf.Max(1f, metresAcross);
            map.rectTransform.sizeDelta = new Vector2(worldBounds.z, worldBounds.w) * pixelsPerMetre;
            map.rectTransform.localEulerAngles = new Vector3(0f, 0f, mapYaw);
            Vector2 p = WorldToMap(player.position);
            PlayerMapPoint = p;
            map.rectTransform.anchoredPosition = -(Vector2)(Quaternion.Euler(0f, 0f, mapYaw) * p);
            if (playerArrow != null) playerArrow.localEulerAngles = new Vector3(0f, 0f, mapYaw - player.eulerAngles.y);
            if (landmarkIcons != null)
                for (int i = 0; i < landmarkIcons.Length && i < landmarkWorld.Length; i++)
                {
                    if (landmarkIcons[i] == null) continue;
                    landmarkIcons[i].anchoredPosition = WorldToMap(landmarkWorld[i]);
                    landmarkIcons[i].localEulerAngles = new Vector3(0f, 0f, -mapYaw);   // icons stay upright
                }
            UpdateObjective();
        }

        void UpdateObjective()
        {
            if (objectiveMarker == null) return;
            Objective = MinimapTargets.Current(GameSession.Current);
            Transform target = TargetFor(Objective);
            bool show = target != null;
            if (objectiveMarker.gameObject.activeSelf != show) objectiveMarker.gameObject.SetActive(show);
            if (!show)
            {
                if (objectiveEdgeArrow != null) objectiveEdgeArrow.gameObject.SetActive(false);
                return;
            }
            objectiveWorld = target.position;
            Vector2 w = MapToWindow(WorldToMap(objectiveWorld));
            Vector2 half = window.rect.size * 0.5f - new Vector2(10f, 10f);
            float scale = Mathf.Max(Mathf.Abs(w.x) / half.x, Mathf.Abs(w.y) / half.y);
            ObjectiveOnEdge = scale > 1f;
            objectiveMarker.anchoredPosition = ObjectiveOnEdge ? w / scale : w;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            objectiveMarker.localScale = new Vector3(pulse, pulse, 1f);
            if (objectiveEdgeArrow != null)
            {
                if (objectiveEdgeArrow.gameObject.activeSelf != ObjectiveOnEdge) objectiveEdgeArrow.gameObject.SetActive(ObjectiveOnEdge);
                if (ObjectiveOnEdge) objectiveEdgeArrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(w.y, w.x) * Mathf.Rad2Deg - 90f);
            }
        }

        Transform TargetFor(MinimapTargets.Kind kind)
        {
            switch (kind)
            {
                case MinimapTargets.Kind.Mira: return mira;
                case MinimapTargets.Kind.Spring: return spring;
                case MinimapTargets.Kind.Heart: return heart;
                case MinimapTargets.Kind.Workshop: return workshop;
                case MinimapTargets.Kind.Wren: return wren;
                case MinimapTargets.Kind.Hen: return hen;
                case MinimapTargets.Kind.Alder: return alder;
                case MinimapTargets.Kind.None: return null;
                default: return NearestReady(MinimapTargets.ItemFor(kind));
            }
        }

        Transform NearestReady(string item)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (ResourceNode node in ResourceNodes())
            {
                if (node.ItemId != item || !node.IsReady) continue;
                float d = (node.Position - player.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = node.transform; }
            }
            return best;
        }

        ResourceNode[] nodes;
        ResourceNode[] ResourceNodes() => nodes != null && nodes.Length > 0 ? nodes : (nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None));
    }
}
