using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("開放世界那樣的美麗風景"), last piece of the same pass
    // as DistantMountainsSetup/MidDistanceTreeRingSetup - Linear fog (not Exponential/
    // ExponentialSquared, which are harder to reason about against exact scene distances) tuned
    // to this scene's specific layout: playable arena ends at radius 15 (BoundaryWalls),
    // near scenery ring 17-26, mid-distance trees 30-48, mountains 55-90. Starts past the mid
    // tree ring's near edge so the playable arena and near scenery stay fully clear, and fully
    // fogs out by 100 so the mountains blend into the horizon instead of the world visibly
    // "stopping" at a hard edge. Color approximates the skybox's horizon haze (lighter/whiter
    // than Skybox_Procedural's zenith _SkyTint) so the blend is seamless.
    internal static class EnableAtmosphericFogSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FogColor = new Color(0.78f, 0.84f, 0.9f);
        private const float FogStartDistance = 35f;
        private const float FogEndDistance = 100f;

        [MenuItem("Tools/Live2DAction/Enable Atmospheric Fog")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStartDistance;
            RenderSettings.fogEndDistance = FogEndDistance;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Enabled linear fog ({FogStartDistance}-{FogEndDistance} units) to blend the distant scenery into the horizon.");
        }
    }
}
