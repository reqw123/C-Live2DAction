using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user request ("我不要攻擊的碰撞顯示 改成防禦"). Replaces the short-lived
    // PlayerAttackHitboxTelegraph (追加93, removed same day): instead of drawing the player's
    // ATTACK capsule, this draws the GUARD wedge - a flat horizontal pie-slice fanning out in
    // front of the player across PlayerGuard.GuardArcDegrees, visible while the guard is up.
    //
    // Re-runnable; run "Remove Player Guard Telegraph" to take it off.
    internal static class PlayerGuardVisualizerSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color WedgeColor = new Color(0.2f, 0.6f, 1f, 1f);

        [MenuItem("Tools/Live2DAction/Add Player Guard Telegraph")]
        public static void Add()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerGuardVisualizerSetup: no Player in " + ScenePath);
                return;
            }

            PlayerGuard g = player.GetComponent<PlayerGuard>();
            if (g == null)
            {
                Debug.LogError("PlayerGuardVisualizerSetup: Player has no PlayerGuard - run 'Add Player Katana Guard' first.");
                return;
            }

            // Clean up the retired attack-telegraph component if it's still on there.
            var stale = player.GetComponent("PlayerAttackHitboxVisualizer") as Component;
            if (stale != null)
            {
                Object.DestroyImmediate(stale);
            }

            PlayerGuardVisualizer vis = player.GetComponent<PlayerGuardVisualizer>();
            if (vis == null)
            {
                vis = player.AddComponent<PlayerGuardVisualizer>();
            }
            vis.EditorConfigure(g, WedgeColor);

            EditorUtility.SetDirty(vis);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlayerGuardVisualizerSetup: PlayerGuardVisualizer on Player. Blue wedge = the " +
                      "frontal block cone (PlayerGuard.GuardArcDegrees), shown while right mouse is held.");
        }

        [MenuItem("Tools/Live2DAction/Remove Player Guard Telegraph")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first.");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            var vis = player != null ? player.GetComponent<PlayerGuardVisualizer>() : null;
            if (vis == null)
            {
                Debug.Log("PlayerGuardVisualizerSetup: nothing to remove.");
                return;
            }
            Object.DestroyImmediate(vis);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlayerGuardVisualizerSetup: removed.");
        }
    }
}
