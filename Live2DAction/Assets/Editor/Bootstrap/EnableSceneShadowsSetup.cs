using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("讓場景變得好看"): the scene's only light
    // (Directional Light) had shadows completely off (Light.shadows == LightShadows.None) -
    // nothing in the scene casts or receives a shadow, a big part of why everything reads as
    // visually flat. Turns on soft shadows both on the light itself and on the URP pipeline
    // asset - soft shadows are gated behind UniversalRenderPipelineAsset.supportsSoftShadows;
    // setting only the Light's own shadow type without also flipping this asset-level switch
    // would silently keep rendering hard-edged (or no) shadows regardless of the Light's own
    // setting.
    //
    // 2026-08-16 correction: supportsSoftShadows turned out to be a GET-ONLY public property in
    // URP 17 (CS0200 compile error broke the whole project's compilation and dropped the Editor
    // into Safe Mode - see Editor.log). Must go through SerializedObject on the underlying
    // field instead; confirmed the actual serialized field name by grepping
    // Live2DAction_URP.asset's own YAML for "m_SoftShadowsSupported" rather than guessing.
    internal static class EnableSceneShadowsSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string PipelineAssetPath = "Assets/_Project/Settings/Live2DAction_URP.asset";

        [MenuItem("Tools/Live2DAction/Enable Scene Shadows")]
        public static void Apply()
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                Debug.LogError("Could not load URP asset at " + PipelineAssetPath);
                return;
            }

            var pipelineSo = new SerializedObject(pipelineAsset);
            SerializedProperty softShadowsProp = pipelineSo.FindProperty("m_SoftShadowsSupported");
            if (softShadowsProp == null)
            {
                Debug.LogError("UniversalRenderPipelineAsset has no m_SoftShadowsSupported field - Unity/URP version mismatch, check manually in the Inspector instead.");
                return;
            }
            softShadowsProp.boolValue = true;
            pipelineSo.ApplyModifiedPropertiesWithoutUndo();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject lightGo = GameObject.Find("Directional Light");
            if (lightGo == null)
            {
                Debug.LogError("Directional Light GameObject not found in " + ScenePath);
                return;
            }

            Light light = lightGo.GetComponent<Light>();
            if (light == null)
            {
                Debug.LogError("Directional Light GameObject has no Light component");
                return;
            }

            light.shadows = LightShadows.Soft;
            // Slightly softened from full-black (1.0) so shadowed areas still read as lit,
            // not pitch black - a reasonable starting point, tune by eye in the Editor.
            light.shadowStrength = 0.8f;

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled soft shadows on Directional Light and the URP pipeline asset.");
        }
    }
}
