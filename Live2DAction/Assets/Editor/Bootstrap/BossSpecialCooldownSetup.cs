using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §9.2 (M4 項目 8). Sets a shared cooldown
    // between the 武士's periodic specials (Breakdance / LeapSlam / OverheadSlam) so several coming
    // due together can't fire back-to-back. 屁孩王 is left at 0 (it shares BossStateMachine but only
    // has one special). Re-runnable; Clear zeroes it.
    internal static class BossSpecialCooldownSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float DefaultCooldown = 7f; // spec suggests 6-10

        [MenuItem("Tools/Live2DAction/Set 武士 Shared Special Cooldown (item 8)")]
        public static void Enable() => Configure(DefaultCooldown);

        [MenuItem("Tools/Live2DAction/Clear 武士 Shared Special Cooldown (item 8)")]
        public static void Disable() => Configure(0f);

        private static void Configure(float seconds)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int changed = 0;
            foreach (BossStateMachine bsm in Object.FindObjectsByType<BossStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bsm.transform.root.name != "武士")
                {
                    continue;
                }
                var so = new SerializedObject(bsm);
                SerializedProperty p = so.FindProperty("sharedSpecialCooldownSeconds");
                if (p == null)
                {
                    Debug.LogError("BossSpecialCooldownSetup: no 'sharedSpecialCooldownSeconds' field - recompile first.");
                    return;
                }
                p.floatValue = seconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            if (changed == 0)
            {
                Debug.LogWarning("BossSpecialCooldownSetup: no 武士 BossStateMachine found.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"BossSpecialCooldownSetup: 武士 sharedSpecialCooldownSeconds = {seconds}.");
        }
    }
}
