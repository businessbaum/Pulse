using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pulse
{
    // The look of a run: post-processing, fog and light. Everything is set up in code, in one place.
    public static class Look
    {
        // Fog distances are measured from the camera, which sits about 37 m from the machine.
        public const float FogStart = 42f, FogEnd = 94f, OutdoorSun = 1.35f;
        public static readonly Color OutdoorAmbient = new Color(0.34f, 0.37f, 0.44f);

        static ColorAdjustments grade;
        static float exposure;

        // Brightens the whole picture, in stops. For the flash of a lightning strike or an explosion.
        public static float Exposure
        {
            get => exposure;
            set
            {
                exposure = value;
                // With flashes switched off the picture is never made brighter than it is.
                if (grade != null) grade.postExposure.Override(Options.Flashes ? value : Mathf.Min(value, 0f));
            }
        }

        // indoors: the lobby. No fog, a dimmer and warmer light.
        public static void Apply(Camera camera, bool indoors = false)
        {
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            // Glowing materials and particles bloom; plain lit surfaces stay below the threshold.
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.6f);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            grade = profile.Add<ColorAdjustments>();
            grade.contrast.Override(14f);
            grade.saturation.Override(6f);

            var volume = new GameObject("Look").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            // Distant ground fades into the background colour, which hides the edge of the world.
            RenderSettings.fog = !indoors;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Tokens.Soot;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = indoors ? new Color(0.44f, 0.4f, 0.38f) : OutdoorAmbient;
            var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.93f, 0.82f);
                sun.intensity = indoors ? 0.95f : OutdoorSun;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            }
        }
    }
}
