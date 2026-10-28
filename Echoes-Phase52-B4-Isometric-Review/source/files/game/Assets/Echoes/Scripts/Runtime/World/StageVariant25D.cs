using UnityEngine;

namespace Echoes
{
    /// <summary>Shows one of two painted variants by the valley's shown Resonance stage (it only reads ResonanceView; nothing is decided here).</summary>
    public sealed class StageVariant25D : MonoBehaviour
    {
        [SerializeField] ResonanceView view;
        [SerializeField] ResonanceStage firstRestoredStage = ResonanceStage.AwakeningStream;
        [SerializeField] GameObject before;
        [SerializeField] GameObject after;

        public bool RestoredShown => after != null && after.activeSelf;

        void LateUpdate()
        {
            bool restored = view != null && view.ShownStage >= firstRestoredStage;
            if (before != null && before.activeSelf == restored) before.SetActive(!restored);
            if (after != null && after.activeSelf != restored) after.SetActive(restored);
        }
    }
}
