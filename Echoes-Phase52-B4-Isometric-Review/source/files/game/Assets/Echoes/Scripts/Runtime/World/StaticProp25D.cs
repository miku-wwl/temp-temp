using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// A painted static 2.5D prop (Phase 5.2-B4): the card the Echoes/StaticProp25D shader builds facing the camera around this object's
    /// origin, and the painting's ground line (left end, front corner, right end in world space, with their image u) from which every
    /// pixel takes its depth. Visual only: colliders and interactions stay on the original objects. Runs in edit mode too: the values reach
    /// the shader through a property block, which a scene does not save.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StaticProp25D : MonoBehaviour
    {
        static readonly int FrameRectId = Shader.PropertyToID("_FrameRect");
        static readonly int BaseLId = Shader.PropertyToID("_BaseL");
        static readonly int BaseFId = Shader.PropertyToID("_BaseF");
        static readonly int BaseRId = Shader.PropertyToID("_BaseR");
        static readonly int BaseUId = Shader.PropertyToID("_BaseU");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        [SerializeField] Vector4 frameRect;        // metres around the anchor in view space: left, bottom, width, height
        [SerializeField] Vector3 baseLeft;
        [SerializeField] Vector3 baseFront;
        [SerializeField] Vector3 baseRight;
        [SerializeField] Vector3 baseU;            // image u of the three base points
        [SerializeField] bool fadeAroundPlayer;

        public Vector4 Frame => frameRect;
        public Vector3 BaseLeft => baseLeft;
        public Vector3 BaseFront => baseFront;
        public Vector3 BaseRight => baseRight;
        public Vector3 BaseU => baseU;
        public bool FadesAroundPlayer => fadeAroundPlayer;

        void OnEnable() => Apply();

        public void Configure(Vector4 frame, Vector3 left, Vector3 front, Vector3 right, Vector3 u, bool fade)
        {
            frameRect = frame;
            baseLeft = left;
            baseFront = front;
            baseRight = right;
            baseU = u;
            fadeAroundPlayer = fade;
            Apply();
        }

        /// <summary>The world point whose depth the painting's column at image u takes (the same piecewise line as the shader).</summary>
        public Vector3 BasePointAt(float u)
        {
            if (u <= baseU.y) return Vector3.Lerp(baseLeft, baseFront, Mathf.Clamp01((u - baseU.x) / Mathf.Max(1e-4f, baseU.y - baseU.x)));
            return Vector3.Lerp(baseFront, baseRight, Mathf.Clamp01((u - baseU.y) / Mathf.Max(1e-4f, baseU.z - baseU.y)));
        }

        void Apply()
        {
            var block = new MaterialPropertyBlock();
            block.SetVector(FrameRectId, frameRect);
            block.SetVector(BaseLId, baseLeft);
            block.SetVector(BaseFId, baseFront);
            block.SetVector(BaseRId, baseRight);
            block.SetVector(BaseUId, baseU);
            block.SetFloat(FadeId, fadeAroundPlayer ? 1f : 0f);
            GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }
    }
}
