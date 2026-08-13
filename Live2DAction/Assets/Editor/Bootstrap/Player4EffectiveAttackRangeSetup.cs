using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Real 2026-08-13 bug report: "我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作出攻
    // 擊，這代表視覺呈現與數值邏輯判定很明顯不一致，請去校正" - Player4's EnemyAI.attackRange
    // (a plain omnidirectional distance check, separately tuned) had drifted out of sync with
    // EnemyAttack.asset's actual Range/Radius (the capsule PlayerCombat's Gizmo and the real
    // hit judgment both use), so the Gizmo could show "in range" while EnemyAI still refused to
    // attack. See EnemyAI's own "combat" field comment for the permanent fix: wires Player4's
    // EnemyAI.combat to its own PlayerCombat, so the attack-range decision is recomputed live
    // from the actual AttackData every frame instead of a manually-synced float - eliminates
    // this entire class of desync going forward, superseding the need to keep re-running
    // EnemyAttackRangeSync.cs every time Range/Radius changes (that tool is left in place as a
    // harmless fallback-value updater, just no longer authoritative once this is wired).
    internal static class Player4EffectiveAttackRangeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Wire Player4 Effective Attack Range")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player4 = GameObject.Find("Player4");
            if (player4 == null)
            {
                Debug.LogError("Player4 GameObject not found in " + ScenePath);
                return;
            }

            EnemyAI ai = player4.GetComponent<EnemyAI>();
            PlayerCombat combat = player4.GetComponent<PlayerCombat>();
            if (ai == null || combat == null)
            {
                Debug.LogError("Player4 needs both EnemyAI and PlayerCombat in " + ScenePath);
                return;
            }

            var so = new SerializedObject(ai);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Player4's EnemyAI now derives its attack range live from PlayerCombat's actual AttackData (Range+Radius) - no more manual attackRange syncing needed.");
        }
    }
}
