using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.Combat;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Converts Enemy (the static "Arisa" standee added by EnemyAnimeVisualSetup.cs) into
    // an AI-driven enemy, reusing the exact same EnemyAI/Health/PlayerCombat wiring
    // GreyboxSceneBuilder.CreateEnemy used for a now-deleted object that was ALSO once named
    // "TrainingDummy" (2026-08-12, explicit user request: "把 Player4 當作敵人開始製作 AI 自主
    // 攻擊模式") - not the same object as today's "TrainingDummy" (that name now belongs to
    // Player3, a deliberately stationary punching bag - see TrainingDummySetup.cs). This
    // comment predates that later reuse of the name; the object described here is long gone.
    //
    // Unlike a stationary target (detectionRange deliberately zeroed), this leaves EnemyAI's
    // detectionRange/attackRange/moveSpeed at the class's own defaults (8/2/2) - the point
    // this time is a real autonomous chase-and-attack enemy, not a punching bag.
    //
    // Standee -> CharacterController swap changes the coordinate frame Enemy's root sits
    // in: as a standee it was placed flat at ground level (root Y = 0, Visual at
    // localPosition.zero touching the ground directly). CharacterController-driven characters
    // in this project are grounded at their CAPSULE CENTER instead (see
    // GreyboxSceneBuilder.CreatePlayer/CreateEnemy's spawn-Y formula), so both the root
    // position AND Visual's local offset need recomputing the same way
    // PlayerMayaVisualSetup.VisualFeetOffset does - this is exactly the "feet not touching the
    // ground" bug class that's bitten Maya and Arisa once each already this project, so it's
    // handled explicitly here rather than left at Vector3.zero.
    internal static class EnemyAISetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string EnemyAttackPath = "Assets/_Project/Settings/Combat/EnemyAttack.asset";

        [MenuItem("Tools/Live2DAction/Convert Enemy Standee To AI Enemy")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("Enemy");
            GameObject player = GameObject.Find("Player");
            if (enemy == null || player == null)
            {
                Debug.LogError("Enemy or Player GameObject not found in " + ScenePath);
                return;
            }

            AttackData enemyAttack = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackPath);
            if (enemyAttack == null)
            {
                Debug.LogError("Could not load AttackData at " + EnemyAttackPath);
                return;
            }

            CapsuleCollider existingCollider = enemy.GetComponent<CapsuleCollider>();
            if (existingCollider != null)
            {
                Object.DestroyImmediate(existingCollider);
            }

            CharacterController controller = enemy.AddComponent<CharacterController>();
            // Same reference-then-destroy trick GreyboxSceneBuilder.CreatePlayer/CreateEnemy
            // use to get Unity's own CapsuleCollider defaults (height 2, radius 0.5) instead
            // of hardcoding them a third time.
            CapsuleCollider capsuleReference = enemy.AddComponent<CapsuleCollider>();
            controller.height = capsuleReference.height;
            controller.radius = capsuleReference.radius;
            controller.center = Vector3.zero;
            Object.DestroyImmediate(capsuleReference);
            // Default (0.001) silently drops sub-threshold Move() calls at high frame rates -
            // see Docs/KNOWN_ISSUES.md's minMoveDistance entry. Every other CharacterController
            // in this scene already has this set to 0.
            controller.minMoveDistance = 0f;
            // Default (0.3) lets one CharacterController climb the rounded top of another
            // it's pushed directly into - see GreyboxSceneBuilder.CreatePlayer's stepOffset=0
            // comment for the full 2026-08-12 bug report this fixes ("很靠近敵人時角色1突然消
            // 失，畫面定格" - Player climbing up onto Player4 and getting stuck oscillating on
            // top of it).
            controller.stepOffset = 0f;

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            enemy.transform.position = new Vector3(
                enemy.transform.position.x,
                groundTopY + controller.center.y + controller.height / 2f,
                enemy.transform.position.z);

            Transform visual = enemy.transform.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, controller.center.y - controller.height / 2f, 0f);
            }

            if (enemy.GetComponent<Health>() == null)
            {
                enemy.AddComponent<Health>();
            }

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai == null)
            {
                ai = enemy.AddComponent<EnemyAI>();
            }
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = player.transform;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombat combat = enemy.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                combat = enemy.AddComponent<PlayerCombat>();
            }
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = ai;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 1;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = enemyAttack;
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            // LockOnTarget was already added by EnemyAnimeVisualSetup.cs - left as-is.

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Converted Enemy into an AI-driven enemy (EnemyAI + Health + PlayerCombat, reusing EnemyAttack.asset).");
        }
    }
}
