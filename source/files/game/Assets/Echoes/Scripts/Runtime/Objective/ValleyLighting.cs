using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echoes
{
    /// <summary>
    /// The only writer of the valley's mood: sun, ambient light, fog, the gradient sky and the colour grading of the
    /// post-processing Volume. It captures the muted scene values once and composes independent contributions every time they
    /// change: the Heart payoff (muted -> awakened) and three additive warm shifts, one per restoration (stream, garden,
    /// windmill). Because the shifts are additive they can happen in any order, they never undo each other, and a loaded
    /// game can set all of them instantly. Stage 0 is muted but readable; stage 3 is a golden hour.
    /// </summary>
    public class ValleyLighting : MonoBehaviour
    {
        static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        static readonly int GroundId = Shader.PropertyToID("_GroundColor");
        static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] Light sun;
        [SerializeField] Volume volume;

        [Header("Heart awakened look (muted -> this)")]
        [SerializeField] Color heartSun = new Color(1f, 0.86f, 0.62f);
        [SerializeField] float heartSunIntensity = 1.35f;
        [SerializeField] Color heartAmbientSky = new Color(0.62f, 0.78f, 1f);
        [SerializeField] Color heartAmbientEquator = new Color(0.86f, 0.76f, 0.60f);
        [SerializeField] Color heartAmbientGround = new Color(0.46f, 0.40f, 0.30f);
        [SerializeField] Color heartFog = new Color(0.93f, 0.82f, 0.64f);
        [SerializeField] Color heartZenith = new Color(0.36f, 0.62f, 0.95f);
        [SerializeField] Color heartHorizon = new Color(0.93f, 0.91f, 0.84f);
        [SerializeField] Color heartGround = new Color(0.50f, 0.56f, 0.42f);
        [SerializeField] float heartSkyExposure = 1.08f;

        [Header("Stream restored (added on top)")]
        [SerializeField] Color streamSunShift = new Color(0f, -0.05f, -0.10f, 0f);
        [SerializeField] float streamSunIntensityShift = 0.15f;
        [SerializeField] Color streamAmbientShift = new Color(0.05f, 0.02f, -0.05f, 0f);
        [SerializeField] Color streamFogShift = new Color(0.02f, -0.02f, -0.06f, 0f);
        [SerializeField] float streamFogDensityShift = -0.0015f;
        [SerializeField] Color streamZenithShift = new Color(0f, 0.02f, 0.02f, 0f);
        [SerializeField] Color streamHorizonShift = new Color(0.04f, 0f, -0.04f, 0f);
        [SerializeField] float streamExposureShift = 0.04f;

        [Header("Garden blooming (added on top)")]
        [SerializeField] Color gardenSunShift = new Color(0f, -0.02f, -0.05f, 0f);
        [SerializeField] float gardenSunIntensityShift = 0.06f;
        [SerializeField] Color gardenAmbientShift = new Color(0.03f, 0.02f, -0.02f, 0f);
        [SerializeField] Color gardenFogShift = new Color(0.01f, 0f, -0.03f, 0f);
        [SerializeField] Color gardenZenithShift = new Color(0.02f, 0f, -0.02f, 0f);
        [SerializeField] Color gardenHorizonShift = new Color(0.05f, -0.02f, -0.06f, 0f);
        [SerializeField] float gardenExposureShift = 0.03f;

        [Header("Windmill turning: the golden hour (added on top)")]
        [SerializeField] Color valleySunShift = new Color(0f, -0.07f, -0.16f, 0f);
        [SerializeField] float valleySunIntensityShift = 0.05f;
        [SerializeField] float valleySunPitchShift = -16f;
        [SerializeField] Color valleyAmbientShift = new Color(0.05f, 0.02f, -0.05f, 0f);
        [SerializeField] Color valleyFogShift = new Color(0.03f, -0.01f, -0.07f, 0f);
        [SerializeField] float valleyFogDensityShift = -0.0008f;
        [SerializeField] Color valleyZenithShift = new Color(0.08f, -0.02f, -0.13f, 0f);
        [SerializeField] Color valleyHorizonShift = new Color(0f, -0.07f, -0.18f, 0f);
        [SerializeField] float valleyExposureShift = 0.06f;

        [Header("Colour grading and glow (post-processing Volume)")]
        [SerializeField] float baseSaturation = -8f;
        [SerializeField] Vector4 saturationShifts = new Vector4(8f, 3f, 4f, 3f);        // heart, stream, garden, valley
        [SerializeField] Vector4 postExposureShifts = new Vector4(0.12f, 0.04f, 0.04f, 0.10f);
        [SerializeField] float baseBloom = 0.22f;
        [SerializeField] Vector4 bloomShifts = new Vector4(0.05f, 0.03f, 0.06f, 0.14f);
        [SerializeField] float baseVignette = 0.22f;
        [SerializeField] float valleyVignetteShift = 0.06f;
        [SerializeField] Color coolFilter = new Color(0.94f, 0.97f, 1f);
        [SerializeField] Color warmFilter = new Color(1f, 0.98f, 0.94f);
        [SerializeField] Color goldenFilter = new Color(1f, 0.945f, 0.87f);

        Color baseSun, baseAmbientSky, baseAmbientEquator, baseAmbientGround, baseFog;
        Color baseZenith, baseHorizon, baseGround;
        float baseSunIntensity, baseFogDensity, baseExposure;
        Quaternion baseSunRotation;
        Material skyMaterial;
        bool hasGradientSky;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments colorAdjustments;
        bool captured;
        bool dirty = true;
        float heartBlend;
        float streamBlend;
        float gardenBlend;
        float valleyBlend;

        public Light Sun => sun;
        public Volume Volume => volume;
        public float HeartBlend => heartBlend;
        public float StreamBlend => streamBlend;
        public float GardenBlend => gardenBlend;
        public float ValleyBlend => valleyBlend;

        /// <summary>The current colour-grading saturation and bloom intensity (tests read what the Volume really holds).</summary>
        public float GradedSaturation => colorAdjustments != null ? colorAdjustments.saturation.value : 0f;
        public float GradedBloom => bloom != null ? bloom.intensity.value : 0f;
        public Color GradedFilter => colorAdjustments != null ? colorAdjustments.colorFilter.value : Color.white;
        public Color SkyHorizon => skyMaterial != null && hasGradientSky ? skyMaterial.GetColor(HorizonId) : Color.black;
        public Color SkyZenith => skyMaterial != null && hasGradientSky ? skyMaterial.GetColor(ZenithId) : Color.black;
        public bool HasGradientSky => hasGradientSky;

        public void SetHeartBlend(float blend)
        {
            heartBlend = Mathf.Clamp01(blend);
            dirty = true;
        }

        public void SetStreamBlend(float blend)
        {
            streamBlend = Mathf.Clamp01(blend);
            dirty = true;
        }

        public void SetGardenBlend(float blend)
        {
            gardenBlend = Mathf.Clamp01(blend);
            dirty = true;
        }

        public void SetValleyBlend(float blend)
        {
            valleyBlend = Mathf.Clamp01(blend);
            dirty = true;
        }

        /// <summary>
        /// Replaces the muted values (the silent valley before the Heart wakes) that the mood is composed from; the awakened and restored
        /// looks are unchanged because the Heart blend lerps away from them. A presentation pilot's before/after switch uses it.
        /// </summary>
        public void SetMutedBase(Color sunColor, Color ambientSky, Color ambientEquator, Color ambientGround, Color fog)
        {
            Capture();
            baseSun = sunColor;
            baseAmbientSky = ambientSky;
            baseAmbientEquator = ambientEquator;
            baseAmbientGround = ambientGround;
            baseFog = fog;
            dirty = true;
        }

        /// <summary>The muted sun colour the mood is composed from (tests compare presentations).</summary>
        public Color MutedSun { get { Capture(); return baseSun; } }

        /// <summary>Applies the mood for the given blends at once, outside Play Mode (the Preview task renders each stage this way).</summary>
        public void ApplyNow(float heart, float stream, float garden, float valley)
        {
            Capture();
            heartBlend = Mathf.Clamp01(heart);
            streamBlend = Mathf.Clamp01(stream);
            gardenBlend = Mathf.Clamp01(garden);
            valleyBlend = Mathf.Clamp01(valley);
            dirty = false;
            Apply();
        }

        void Start()
        {
            Capture();
            dirty = true;
        }

        void LateUpdate()
        {
            if (!captured || !dirty) return;
            dirty = false;
            Apply();
        }

        void Capture()
        {
            if (captured) return;
            captured = true;
            if (sun != null)
            {
                baseSun = sun.color;
                baseSunIntensity = sun.intensity;
                baseSunRotation = sun.transform.rotation;
            }
            baseAmbientSky = RenderSettings.ambientSkyColor;
            baseAmbientEquator = RenderSettings.ambientEquatorColor;
            baseAmbientGround = RenderSettings.ambientGroundColor;
            baseFog = RenderSettings.fogColor;
            baseFogDensity = RenderSettings.fogDensity;

            if (RenderSettings.skybox != null)
            {
                skyMaterial = new Material(RenderSettings.skybox);   // never edit the shared asset
                RenderSettings.skybox = skyMaterial;
                hasGradientSky = skyMaterial.HasProperty(ZenithId);
                if (hasGradientSky)
                {
                    baseZenith = skyMaterial.GetColor(ZenithId);
                    baseHorizon = skyMaterial.GetColor(HorizonId);
                    baseGround = skyMaterial.GetColor(GroundId);
                    baseExposure = skyMaterial.GetFloat(ExposureId);
                }
            }

            if (volume != null && volume.profile != null)   // .profile is the Volume's own copy, never the shared asset
            {
                volume.profile.TryGet(out bloom);
                volume.profile.TryGet(out vignette);
                volume.profile.TryGet(out colorAdjustments);
            }
        }

        void Apply()
        {
            float h = heartBlend;
            float s = streamBlend;
            float g = gardenBlend;
            float v = valleyBlend;

            if (sun != null)
            {
                sun.color = Compose(baseSun, heartSun, h, Shift(streamSunShift, s, gardenSunShift, g, valleySunShift, v));
                sun.intensity = Mathf.Lerp(baseSunIntensity, heartSunIntensity, h)
                    + streamSunIntensityShift * s + gardenSunIntensityShift * g + valleySunIntensityShift * v;
                // The golden hour: the sun sinks, so shadows lengthen and the light turns sideways.
                sun.transform.rotation = baseSunRotation * Quaternion.Euler(valleySunPitchShift * v, 0f, 0f);
            }

            Color ambientShift = Shift(streamAmbientShift, s, gardenAmbientShift, g, valleyAmbientShift, v);
            RenderSettings.ambientSkyColor = Compose(baseAmbientSky, heartAmbientSky, h, ambientShift);
            RenderSettings.ambientEquatorColor = Compose(baseAmbientEquator, heartAmbientEquator, h, ambientShift);
            RenderSettings.ambientGroundColor = Compose(baseAmbientGround, heartAmbientGround, h, ambientShift);
            RenderSettings.fogColor = Compose(baseFog, heartFog, h, Shift(streamFogShift, s, gardenFogShift, g, valleyFogShift, v));
            RenderSettings.fogDensity = baseFogDensity + streamFogDensityShift * s + valleyFogDensityShift * v;

            if (skyMaterial != null && hasGradientSky)
            {
                skyMaterial.SetColor(ZenithId, Compose(baseZenith, heartZenith, h, Shift(streamZenithShift, s, gardenZenithShift, g, valleyZenithShift, v)));
                skyMaterial.SetColor(HorizonId, Compose(baseHorizon, heartHorizon, h, Shift(streamHorizonShift, s, gardenHorizonShift, g, valleyHorizonShift, v)));
                skyMaterial.SetColor(GroundId, Compose(baseGround, heartGround, h, Color.clear));
                skyMaterial.SetFloat(ExposureId, Mathf.Lerp(baseExposure, heartSkyExposure, h)
                    + streamExposureShift * s + gardenExposureShift * g + valleyExposureShift * v);
                if (sun != null)
                {
                    skyMaterial.SetColor(SunColorId, sun.color);
                    skyMaterial.SetVector(SunDirId, -sun.transform.forward);
                }
            }

            ApplyGrading(h, s, g, v);
        }

        void ApplyGrading(float h, float s, float g, float v)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.Override(baseSaturation + saturationShifts.x * h + saturationShifts.y * s + saturationShifts.z * g + saturationShifts.w * v);
                colorAdjustments.postExposure.Override(postExposureShifts.x * h + postExposureShifts.y * s + postExposureShifts.z * g + postExposureShifts.w * v);
                colorAdjustments.colorFilter.Override(Color.Lerp(Color.Lerp(Color.Lerp(coolFilter, Color.white, h), warmFilter, g), goldenFilter, v));
            }
            if (bloom != null)
                bloom.intensity.Override(baseBloom + bloomShifts.x * h + bloomShifts.y * s + bloomShifts.z * g + bloomShifts.w * v);
            if (vignette != null)
                vignette.intensity.Override(baseVignette + valleyVignetteShift * v);
        }

        static Color Shift(Color stream, float s, Color garden, float g, Color valley, float v) => stream * s + garden * g + valley * v;

        static Color Compose(Color baseColor, Color heartColor, float heart, Color shift)
        {
            Color mixed = Color.Lerp(baseColor, heartColor, heart);
            return new Color(
                Mathf.Clamp01(mixed.r + shift.r),
                Mathf.Clamp01(mixed.g + shift.g),
                Mathf.Clamp01(mixed.b + shift.b),
                1f);
        }
    }
}
