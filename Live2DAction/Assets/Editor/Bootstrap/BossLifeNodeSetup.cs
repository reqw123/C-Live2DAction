using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §8.2 (M4 項目 7). Adds a BossLifeNodeController
    // (2 Deathblow nodes) to the 武士 in GreyboxTest, so a player finisher on the posture-broken boss
    // spends a node -> phase 2 (first) -> permanent death (second), instead of the current
    // "50% of current HP + auto-revive after 5s". 屁孩王 is left without one (it shares BossStateMachine
    // but is an elite, not a fight-ending boss) - it keeps the ordinary execution path.
    //
    // Only the 武士's ExecutionAbility target routing changes; ordinary enemies are untouched
    // (ExecutionAbility.instantKillNonExecutableTargets stays false = the 2026-08-18 "扣50%" behaviour).
    // Re-runnable. Remove strips it.
    internal static class BossLifeNodeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add 武士 Deathblow Life Nodes (item 7)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject boss = GameObject.Find("武士");
            if (boss == null)
            {
                Debug.LogError("BossLifeNodeSetup: no '武士' in " + ScenePath);
                return;
            }
            if (boss.GetComponent<BossStateMachine>() == null)
            {
                Debug.LogError("BossLifeNodeSetup: 武士 has no BossStateMachine.");
                return;
            }

            var node = boss.GetComponent<BossLifeNodeController>();
            if (node == null)
            {
                node = Undo.AddComponent<BossLifeNodeController>(boss);
            }
            node.EditorConfigure(2, 2, restoreHealth: true);
            EditorUtility.SetDirty(node);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BossLifeNodeSetup: 武士 has 2 Deathblow nodes. First finisher -> phase 2, second -> permanent death.");
        }

        [MenuItem("Tools/Live2DAction/Remove 武士 Deathblow Life Nodes (item 7)")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject boss = GameObject.Find("武士");
            var node = boss != null ? boss.GetComponent<BossLifeNodeController>() : null;
            if (node == null)
            {
                Debug.LogWarning("BossLifeNodeSetup.Remove: 武士 has no BossLifeNodeController.");
                return;
            }
            Object.DestroyImmediate(node);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BossLifeNodeSetup.Remove: reverted 武士 to the ordinary execution path (50% HP + auto-revive).");
        }
    }
}
