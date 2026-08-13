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
    // Adds the two Fairy Tail doujin Live2D models (076/Natsu, 077/Lucy) into the 3D
    // greybox scene as standalone camera-facing standees, reusing the exact billboard
    // technique PlayerCubismVisualSetup.cs used for 076 before Maya replaced it as Player's
    // visual (URP-shader swap, CanvasHeight/PixelsPerUnit-based scale, CubismBillboard for
    // yaw-only camera facing).
    //
    // CLAUDE.md rule 2: 076/077 are internal-prototype-only placeholders and must NEVER ship
    // in any Build handed to anyone (Alpha and later). Both GameObjects are named with a
    // "_DoNotShip" suffix (matching MechaModel_DoNotShip's convention) so this is visible at
    // a glance in the Hierarchy, not just in a comment. See Docs/ASSET_LICENSES.md.
    internal static class Live2DStandeeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ShaderName = "Live2DAction/CubismUnlitURP";
        private const float TargetHeightMeters = 1.8f;

        private static readonly StandeeSpec[] Standees =
        {
            new StandeeSpec("076_DoNotShip", "Assets/_Project/Live2D/PlaceholderCharacter/c_7001.model3.json", new Vector3(-6f, 0f, -8f)),
            new StandeeSpec("077_DoNotShip", "Assets/_Project/Live2D/PlaceholderCharacter077/c_7002.model3.json", new Vector3(-3f, 0f, -8f)),
        };

        [MenuItem("Tools/Live2DAction/Add 076-077 Live2D Standees (DoNotShip)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (StandeeSpec spec in Standees)
            {
                CreateStandee(spec);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added 076/077 Live2D standees (DoNotShip) to the scene.");
        }

        private static void CreateStandee(StandeeSpec spec)
        {
            GameObject existing = GameObject.Find(spec.Name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            CubismModel3Json modelJson = CubismModel3Json.LoadAtPath(spec.Model3JsonPath);
            CubismModel model = modelJson.ToModel();
            GameObject modelGo = model.gameObject;
            modelGo.name = spec.Name;

            ApplyUrpShader(modelGo);

            float canvasHeightUnityUnits = model.CanvasInformation.CanvasHeight / model.CanvasInformation.PixelsPerUnit;
            float scale = canvasHeightUnityUnits > 0.0001f ? TargetHeightMeters / canvasHeightUnityUnits : 1f;

            modelGo.transform.position = spec.Position;
            modelGo.transform.rotation = Quaternion.identity;
            modelGo.transform.localScale = Vector3.one * scale;

            modelGo.AddComponent<CubismBillboard>();

            Debug.Log($"Created {spec.Name} (scale={scale}, canvasHeightUnityUnits={canvasHeightUnityUnits}).");
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

        private readonly struct StandeeSpec
        {
            public readonly string Name;
            public readonly string Model3JsonPath;
            public readonly Vector3 Position;

            public StandeeSpec(string name, string model3JsonPath, Vector3 position)
            {
                Name = name;
                Model3JsonPath = model3JsonPath;
                Position = position;
            }
        }
    }
}
