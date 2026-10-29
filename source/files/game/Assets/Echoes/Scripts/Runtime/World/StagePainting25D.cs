using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Shows the painting of the valley's Resonance stage on an Echoes/StageProp25D card (Phase 5.2-B5). The stage value is the sum of the
    /// three stage blends ResonanceView animates (stream, garden, valley), so when a stage is reached in play the painting changes over the
    /// same seconds as the light, and a loaded save shows its stage at once. It only reads ResonanceView: nothing is decided or saved here.
    /// The card's frame and ground line stay StaticProp25D's; this adds _Stage to the same property block.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class StagePainting25D : MonoBehaviour
    {
        static readonly int StageId = Shader.PropertyToID("_Stage");

        [SerializeField] ResonanceView view;
        MaterialPropertyBlock block;
        MeshRenderer target;
        float applied = -1f;

        /// <summary>The stage shown: 0 Silence .. 3 Restored, fractional while a stage is being reached.</summary>
        public float Stage { get; private set; }

        void OnEnable() => applied = -1f;

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            Stage = view == null ? 0f : Mathf.Clamp(view.StreamBlend + view.GardenBlend + view.ValleyBlend, 0f, 3f);
            if (Mathf.Abs(Stage - applied) < 0.0005f) return;
            if (target == null) target = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetFloat(StageId, Stage);
            target.SetPropertyBlock(block);
            applied = Stage;
        }
    }
}
