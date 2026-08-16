using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("讓場景變得好看"): the scene had zero Volume
    // components anywhere - no Bloom, no tonemapping, no color grading, no vignette. Adds a
    // single Global Volume with a small, conservative starting profile (ACES tonemapping is
    // the biggest single lever - without it URP's default is a flat linear/None response that
    // looks washed out on bright surfaces). Deliberately scoped to camera-only effects
    // (Bloom/Tonemapping/ColorAdjustments/Vignette) - Ambient Occlusion needs an SSAO Renderer
    // Feature added to the URP Renderer asset first (a separate, riskier change to a shared
    // asset), left out of this pass.
    internal static class PostProcessingVolumeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ProfilePath = "Assets/_Project/Settings/PostProcessing/GreyboxVolumeProfile.asset";

        [MenuItem("Tools/Live2DAction/Add Post-Processing Volume")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings/PostProcessing"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "PostProcessing");
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            ConfigureBloom(profile);
            ConfigureTonemapping(profile);
            ConfigureColorAdjustments(profile);
            ConfigureVignette(profile);
            EditorUtility.SetDirty(profile);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject volumeGo = GameObject.Find("PostProcessingVolume");
            if (volumeGo == null)
            {
                volumeGo = new GameObject("PostProcessingVolume");
            }

            Volume volume = volumeGo.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeGo.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            GameObject mainCameraGo = GameObject.Find("Main Camera");
            if (mainCameraGo != null)
            {
                UniversalAdditionalCameraData cameraData = mainCameraGo.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                {
                    cameraData.renderPostProcessing = true;
                }
            }
            else
            {
                Debug.LogWarning("Main Camera GameObject not found - could not confirm renderPostProcessing is enabled on it.");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added a Global Volume (Bloom/ACES Tonemapping/Color Adjustments/Vignette) and enabled post-processing on Main Camera.");
        }

        private static void ConfigureBloom(VolumeProfile profile)
        {
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.25f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;
        }

        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            if (!profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }
            colorAdjustments.active = true;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 8f;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = 6f;
        }

        private static void ConfigureVignette(VolumeProfile profile)
        {
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.2f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;
        }
    }
}
