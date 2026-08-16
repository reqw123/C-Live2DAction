using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("讓場景變得好看"): the URP pipeline asset was set to
    // the "Ultra" quality tier but msaaSampleCount was still 1 (off) - edges everywhere were
    // aliased despite the quality level's name implying otherwise. 4x is the common sweet spot
    // (visibly smoother edges, moderate GPU cost) - not 8x, which roughly doubles that cost
    // again for a much smaller further improvement.
    internal static class EnableMsaaSetup
    {
        private const string PipelineAssetPath = "Assets/_Project/Settings/Live2DAction_URP.asset";

        [MenuItem("Tools/Live2DAction/Enable MSAA (4x)")]
        public static void Apply()
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                Debug.LogError("Could not load URP asset at " + PipelineAssetPath);
                return;
            }

            pipelineAsset.msaaSampleCount = 4;
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled 4x MSAA on the URP pipeline asset.");
        }
    }
}
