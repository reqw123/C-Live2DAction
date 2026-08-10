using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Upgrades the existing GreyboxTest scene for Step 5 (melee enemy AI):
    // - Adds Health to Player and wires CharacterMovement.health so dodge invulnerability
    //   (already implemented in Step 3) actually blocks damage now that something can deal it.
    // - Replaces the static TrainingDummy (a capsule with just Health/LockOnTarget) with an
    //   AI-driven enemy: CharacterController + EnemyAI + a reused PlayerCombat so it attacks
    //   through the same frame-data combo pipeline the player uses. Rebuilt from scratch
    //   rather than retrofitted in place since the old TrainingDummy's mesh/collider were on
    //   its own root, not a separate "Visual" child, matching GreyboxSceneBuilder.CreateEnemy.
    internal static class FixEnemyAISetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string EnemyAttackPath = "Assets/_Project/Settings/Combat/EnemyAttack.asset";

        [MenuItem("Tools/Live2DAction/[Fix] Add Enemy AI")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject ground = GameObject.Find("Ground");
            GameObject oldDummy = GameObject.Find("TrainingDummy");
            if (player == null || ground == null || oldDummy == null)
            {
                Debug.LogError("Player, Ground, or TrainingDummy not found in " + ScenePath);
                return;
            }

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null)
            {
                playerHealth = player.AddComponent<Health>();
            }

            var movementSo = new SerializedObject(movement);
            movementSo.FindProperty("health").objectReferenceValue = playerHealth;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            Object.DestroyImmediate(oldDummy);

            var enemy = new GameObject("TrainingDummy");

            CapsuleCollider capsuleReference = enemy.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;

            float groundTopY = ground.GetComponent<Collider>().bounds.max.y;
            enemy.transform.position = new Vector3(0f, groundTopY + controller.center.y + controller.height / 2f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(enemy.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            enemy.AddComponent<Health>();
            enemy.AddComponent<LockOnTarget>();

            EnemyAI ai = enemy.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = player.transform;
            aiSo.FindProperty("detectionRange").floatValue = 8f;
            aiSo.FindProperty("attackRange").floatValue = 2f;
            aiSo.FindProperty("moveSpeed").floatValue = 2f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombat combat = enemy.AddComponent<PlayerCombat>();
            AttackData enemyAttack = CreateOrLoadEnemyAttack();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = ai;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 1;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = enemyAttack;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added Player Health (wired to dodge invulnerability) and replaced TrainingDummy with an AI-driven enemy.");
        }

        private static AttackData CreateOrLoadEnemyAttack()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackPath);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<AttackData>();
            var so = new SerializedObject(data);
            so.FindProperty("attackId").stringValue = "EnemyAttack";
            so.FindProperty("damage").floatValue = 5f;
            so.FindProperty("startupFrames").intValue = 10;
            so.FindProperty("activeFrames").intValue = 4;
            so.FindProperty("recoveryFrames").intValue = 20;
            so.FindProperty("comboWindowFrames").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, EnemyAttackPath);
            return data;
        }
    }
}
