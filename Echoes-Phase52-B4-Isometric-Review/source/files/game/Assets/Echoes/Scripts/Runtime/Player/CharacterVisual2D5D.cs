using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Visual-only 2.5D presentation of the player (Phase 5.2-B3). The PlayerController root stays authoritative for position, collision,
    /// interaction and saving; this child only reads where the root went and which way it faces, then shows the matching illustrated frame.
    /// Four drawn directions (S, N, W and E = W mirrored); diagonals use the side views. The run cycle advances by distance travelled divided
    /// by the cycle's stride, so the planted foot does not skate at the controller's speed.
    /// It runs after the cameras (the legacy orbit rig still writes the camera transform in its own LateUpdate before the fixed camera
    /// overrides it), so the facing is always measured against the view that is actually rendered.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class CharacterVisual2D5D : MonoBehaviour
    {
        [SerializeField] SpriteSheet25D sheet;
        [SerializeField] PlayerController player;
        [SerializeField] Sprite25DQuad card;
        [SerializeField] Transform contactShadow;
        [SerializeField] float runEnterSpeed = 0.6f;
        [SerializeField] float runExitSpeed = 0.3f;
        [Tooltip("Half-width of the N and S sectors in degrees; the side views take the rest, so every diagonal (45 and 135 degrees) shows a side view.")]
        [SerializeField] float backFrontHalfWidth = 35f;
        [Tooltip("Extra degrees a direction is kept before switching; smaller than 45 - backFrontHalfWidth so diagonals never depend on the previous view.")]
        [SerializeField] float hysteresis = 6f;
        [SerializeField] float turnHoldSeconds = 0.07f;
        [SerializeField] float scale = 1f;

        Vector3 lastPosition;
        bool hasLast;
        float idleTime;
        string turnVia;
        float turnTimer;

        public SpriteSheet25D Sheet => sheet;
        public string Direction { get; private set; } = "S";
        public string ShownDirection { get; private set; } = "S";
        public bool Running { get; private set; }
        public float RunPhase { get; private set; }
        public int FrameIndex { get; private set; }
        public bool Mirrored { get; private set; }
        public float FacingAngle { get; private set; }
        public float LastSpeed { get; private set; }

        void LateUpdate() => Step(Time.deltaTime, Camera.main);

        /// <summary>Shows another drawing set (the B4 pilot's before/after switch); the run phase carries on.</summary>
        public void UseSheet(SpriteSheet25D other)
        {
            if (other != null) sheet = other;
        }

        /// <summary>One presentation step (public so tests can drive it). A jump of more than 3 m in one step is a teleport, not running.</summary>
        public void Step(float dt, Camera view)
        {
            if (sheet == null || player == null || view == null || dt <= 0f) return;
            Vector3 position = player.transform.position;
            float travelled = 0f;
            if (hasLast)
            {
                travelled = Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).magnitude;
                if (travelled > 3f) travelled = 0f;
            }
            lastPosition = position;
            hasLast = true;
            LastSpeed = travelled / dt;
            Running = Running ? LastSpeed > runExitSpeed : LastSpeed > runEnterSpeed;

            Vector3 facing = Flat(player.transform.forward), camForward = Flat(view.transform.forward), camRight = Flat(view.transform.right);
            FacingAngle = Mathf.Atan2(Vector3.Dot(facing, camRight), Vector3.Dot(facing, camForward)) * Mathf.Rad2Deg;
            string wanted = Sector(FacingAngle, backFrontHalfWidth);
            if (wanted != Direction && !InSector(FacingAngle, Direction, backFrontHalfWidth, hysteresis))
            {
                if (Opposite(wanted, Direction))
                {
                    // a half turn passes through a perpendicular view for a moment, so it reads as turning rather than flipping
                    turnVia = Direction == "N" || Direction == "S" ? (FacingAngle >= 0f ? "E" : "W") : "S";
                    turnTimer = turnHoldSeconds;
                }
                Direction = wanted;
            }
            if (turnTimer > 0f) turnTimer -= dt;
            ShownDirection = turnTimer > 0f && turnVia != null ? turnVia : Direction;

            SpriteSheet25D.Direction frames = sheet.Resolve(ShownDirection, out bool mirrored, out float phaseOffset);
            if (frames == null) return;
            Mirrored = mirrored;
            if (Running)
            {
                RunPhase = Mathf.Repeat(RunPhase + travelled / Mathf.Max(0.1f, sheet.strideMeters), 1f);
                FrameIndex = Mathf.Min(frames.run.Length - 1, Mathf.FloorToInt(Mathf.Repeat(RunPhase + phaseOffset, 1f) * frames.run.Length));
                card.Show(frames.run[FrameIndex], frames.pivotPx, sheet.pixelsPerMeter, mirrored, scale);
            }
            else
            {
                idleTime += dt;
                FrameIndex = Mathf.FloorToInt(idleTime * sheet.idleFps) % frames.idle.Length;
                card.Show(frames.idle[FrameIndex], frames.pivotPx, sheet.pixelsPerMeter, mirrored, scale);
            }
            PlaceShadow(position);
        }

        void PlaceShadow(Vector3 feet)
        {
            if (contactShadow == null) return;
            if (Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(player.transform))
                contactShadow.SetPositionAndRotation(hit.point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.up, hit.normal));
            else
                contactShadow.SetPositionAndRotation(feet + Vector3.up * 0.02f, Quaternion.identity);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>0 = facing away from the camera (N), 180 = facing it (S), +90 = facing screen-right (E), -90 = screen-left (W).</summary>
        public static string Sector(float angle, float backFrontHalfWidth)
        {
            float a = Mathf.Abs(angle);
            if (a <= backFrontHalfWidth) return "N";
            if (a >= 180f - backFrontHalfWidth) return "S";
            return angle > 0f ? "E" : "W";
        }

        static bool InSector(float angle, string direction, float half, float h)
        {
            float a = Mathf.Abs(angle);
            switch (direction)
            {
                case "N": return a <= half + h;
                case "S": return a >= 180f - half - h;
                case "E": return angle > half - h && angle < 180f - half + h;
                default: return angle < -(half - h) && angle > -(180f - half + h);
            }
        }

        static bool Opposite(string a, string b) => (a == "N" && b == "S") || (a == "S" && b == "N") || (a == "E" && b == "W") || (a == "W" && b == "E");
    }
}
