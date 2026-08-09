using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.EditorTools
{
    internal static class ProjectSetup
    {
        [MenuItem("Tools/Live2DAction/Bootstrap Phase 1 Project")]
        public static void Run()
        {
            CreateFolders();
            CreateUrpAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Live2DAction Phase 1 project bootstrap complete.");
        }

        private static void CreateFolders()
        {
            string[] folders =
            {
                "Assets/_Project",
                "Assets/_Project/Scenes",
                "Assets/_Project/Settings",
                "Assets/_Project/Game",
                "Assets/_Project/Game/Core",
                "Assets/_Project/Game/Input",
                "Assets/_Project/Game/Characters",
                "Assets/_Project/Game/Camera",
                "Assets/_Project/Game/Combat",
                "Assets/_Project/Tests",
                "Assets/_Project/Tests/EditMode",
                "Assets/_Project/Tests/PlayMode",
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static void CreateUrpAsset()
        {
            const string rendererPath = "Assets/_Project/Settings/Live2DAction_Renderer.asset";
            const string pipelineAssetPath = "Assets/_Project/Settings/Live2DAction_URP.asset";

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);

            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, pipelineAssetPath);

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }
        }
    }
}
