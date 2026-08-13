using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Real 2026-08-13 bug report: user tuned EnemyAttack.asset's Range up to 7.5 (from 1.5) to
    // give Player4 a long-reach attack, then reported "我沒有被敵人隔空打到" (never actually got
    // hit from range). Root cause: EnemyAI.attackRange (the AI's own "am I close enough to
    // start attacking" threshold, decoupled from AttackData.Range) was still at its class
    // default of 2 - Player4 kept walking to within melee distance before ever entering
    // EnemyState.Attacking, so the extra reach in the hit capsule never got exercised; the AI
    // simply never fired from further away. AttackData.Range and EnemyAI.attackRange are two
    // independent fields that happen to need to stay roughly in sync for a ranged-feeling
    // attack to actually read as ranged - this tool reads EnemyAttack.asset's current Range
    // and pushes Player4's attackRange to just under it (a small skin so the AI is standing
    // fully within the hit capsule's reach when it commits to attacking, not right at the
    // edge), rather than hardcoding a number here that would silently drift out of sync the
    // next time someone tunes Range again.
    internal static class EnemyAttackRangeSync
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string EnemyAttackPath = "Assets/_Project/Settings/Combat/EnemyAttack.asset";

        // Kept short relative to typical Range values (7.5) so the AI commits to attacking
        // comfortably inside its own reach rather than right at the boundary, where frame
        // timing/movement could leave it a hair short.
        private const float ReachSkin = 0.5f;

        [MenuItem("Tools/Live2DAction/Sync Player4 Attack Range To EnemyAttack Data")]
        public static void Apply()
        {
            AttackData enemyAttack = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackPath);
            if (enemyAttack == null)
            {
                Debug.LogError("Could not load AttackData at " + EnemyAttackPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player4 = GameObject.Find("Player4");
            if (player4 == null)
            {
                Debug.LogError("Player4 GameObject not found in " + ScenePath);
                return;
            }

            EnemyAI ai = player4.GetComponent<EnemyAI>();
            if (ai == null)
            {
                Debug.LogError("Player4 has no EnemyAI in " + ScenePath);
                return;
            }

            float newAttackRange = Mathf.Max(0.5f, enemyAttack.Range - ReachSkin);

            var aiSo = new SerializedObject(ai);
            float detectionRange = aiSo.FindProperty("detectionRange").floatValue;
            if (newAttackRange > detectionRange)
            {
                // Would leave the AI unable to ever notice the player is within attack
                // distance (Idle forever) - detectionRange must stay >= attackRange for the
                // state machine (EnemyBehaviorUtility.DetermineState) to reach Attacking at
                // all. Raise it to match rather than silently producing a broken enemy.
                aiSo.FindProperty("detectionRange").floatValue = newAttackRange;
                Debug.LogWarning($"Raised Player4's detectionRange to {newAttackRange} to stay >= the new attackRange (was {detectionRange}).");
            }
            aiSo.FindProperty("attackRange").floatValue = newAttackRange;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Player4's EnemyAI.attackRange synced to {newAttackRange} (EnemyAttack.Range={enemyAttack.Range}, skin={ReachSkin}) - it will now commit to attacking from range instead of walking to melee distance first.");
        }
    }
}
