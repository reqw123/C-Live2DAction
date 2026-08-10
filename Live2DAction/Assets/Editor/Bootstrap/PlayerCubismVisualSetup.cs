using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Rendering;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // One-time (repeatable) swap of the Player's placeholder capsule mesh for the
    // 076 Live2D model, rendered as a camera-facing standee via CubismBillboard.
    // 076 is an internal-prototype-only placeholder - see Docs/ASSET_LICENSES.md.
    internal static class PlayerCubismVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string Model3JsonPath = "Assets/_Project/Live2D/PlaceholderCharacter/c_7001.model3.json";
        private const string ShaderName = "Live2DAction/CubismUnlitURP";
        private const float TargetHeightMeters = 1.8f;

        [MenuItem("Tools/Live2DAction/Replace Player Visual With Live2D Standee")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            // Destroy by index rather than Find("Visual") by name: re-running this after a
            // previous attempt (e.g. to fix scale) must still clean up the old standee even
            // though CubismModel3Json.ToModel() does not appear to preserve a Find-able name
            // on the instantiated root at Play time (see Docs/KNOWN_ISSUES.md).
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(player.transform.GetChild(i).gameObject);
            }

            CubismModel3Json modelJson = CubismModel3Json.LoadAtPath(Model3JsonPath);
            CubismModel model = modelJson.ToModel();
            GameObject modelGo = model.gameObject;
            modelGo.name = "Visual";

            ApplyUrpShader(modelGo);

            // CanvasHeight/Width are in pixels; PixelsPerUnit converts that to the Unity
            // units the instantiated mesh is actually built in (confirmed empirically: this
            // model's ArtMesh vertices already sit in a roughly [-0.2, 0.2] local range at
            // scale 1, not in the raw 1200-pixel canvas range).
            float canvasHeightUnityUnits = model.CanvasInformation.CanvasHeight / model.CanvasInformation.PixelsPerUnit;
            float scale = canvasHeightUnityUnits > 0.0001f ? TargetHeightMeters / canvasHeightUnityUnits : 1f;

            modelGo.transform.SetParent(player.transform, false);
            modelGo.transform.localPosition = Vector3.zero;
            modelGo.transform.localRotation = Quaternion.identity;
            modelGo.transform.localScale = Vector3.one * scale;

            modelGo.AddComponent<CubismBillboard>();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Replaced Player visual with Live2D standee (scale={scale}, canvasHeightUnityUnits={canvasHeightUnityUnits}).");
        }

        private static void ApplyUrpShader(GameObject modelGo)
        {
            Shader urpShader = Shader.Find(ShaderName);
            if (urpShader == null)
            {
                Debug.LogError("Could not find shader " + ShaderName + " - model will render with the incompatible built-in RP shader.");
                return;
            }

            CubismRenderer[] renderers = modelGo.GetComponentsInChildren<CubismRenderer>();
            foreach (CubismRenderer renderer in renderers)
            {
                if (renderer.Material != null)
                {
                    renderer.Material.shader = urpShader;
                }
            }
        }
    }
}
