using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Adds "Player3" (2026-08-13, explicit user request: "攻擊、動作判定、機制完全與p1一致，差
    // 別只在於他完全不會動，也不會攻擊") - a stationary training-dummy-style character that
    // shares Player's exact combat setup (same Maya visual, same LightAttack1/2/3 AttackData
    // assets, same CharacterAttackAnimationLink -> Attack1/2/3 animator states) but has no
    // input source and no AI at all, so it can never move or attack - it just stands there and
    // can be hit, same behavior class as Player2 (explicit user confirmation: damageable like
    // Player2, not an untouchable prop).
    //
    // "機制完全一致" is satisfied by literally reusing Player's own assets rather than
    // approximating them: the exact same LightAttack1/2/3.asset references (so any future
    // balance tuning on those automatically applies here too, no separate copies to drift out
    // of sync - see this whole session's repeated "two independent numbers went stale" lessons
    // in KNOWN_ISSUES.md for why that matters), and the same Maya prefab (whose Animator
    // already references the shared AnimatorController CombatAnimatorSetup.cs wired Attack1/
    // Attack2/Attack3 onto - a fresh Maya instance gets those states for free, no re-wiring
    // needed).
    //
    // Never actually attacks: PlayerCombat.inputSource is left unset (null) - Update() resolves
    // InputCommand as "inputSource as IInputCommand", which is null-safe and always false, so
    // ComboAttackState.Tick() never sees attackPressed=true and stays in Idle forever. Never
    // moves: no CharacterMovement/PlayerInputProvider/EnemyAI at all, and uses a plain
    // CapsuleCollider (not CharacterController) since there is no movement to drive - same
    // choice Player2 already made for the same "purely passive" reason.
    internal static class Player3TrainingDummySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MayaPrefabPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab";
        private const string LightAttack1Path = "Assets/_Project/Settings/Combat/LightAttack1.asset";
        private const string LightAttack2Path = "Assets/_Project/Settings/Combat/LightAttack2.asset";
        private const string LightAttack3Path = "Assets/_Project/Settings/Combat/LightAttack3.asset";

        [MenuItem("Tools/Live2DAction/Add Player3 (Stationary Damageable Dummy, Maya Visual)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (GameObject.Find("Player3") != null)
            {
                Debug.LogError("Player3 already exists in " + ScenePath);
                return;
            }

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;

            var player3 = new GameObject("Player3");

            // Same physical size as Player's own CharacterController (radius 0.5, height 1) -
            // it's wearing Player's exact visual model, so it should occupy the same physical
            // footprint. CapsuleCollider (not CharacterController) because there's no movement
            // to drive - matches Player2's own reasoning for the same choice.
            CapsuleCollider collider = player3.AddComponent<CapsuleCollider>();
            collider.center = Vector3.zero;
            collider.radius = 0.5f;
            collider.height = 1f;

            player3.transform.position = new Vector3(5f, groundTopY + collider.center.y + collider.height / 2f, 0f);

            player3.AddComponent<Health>();

            PlayerCombat combat = player3.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 3;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack1Path);
            comboProperty.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack2Path);
            comboProperty.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack3Path);
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.ApplyModifiedPropertiesWithoutUndo();
            // inputSource intentionally left unset - see class comment for why this alone
            // guarantees it can never attack.

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MayaPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("Could not load Maya prefab at " + MayaPrefabPath);
                return;
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, player3.transform);
            visual.name = "Visual";
            // Same feet-offset reasoning as PlayerMayaVisualSetup.VisualFeetOffset - Maya's
            // mesh origin is at her feet, this collider is grounded at its CENTER, so the
            // visual needs to sit half the collider's height below the parent's own origin.
            visual.transform.localPosition = new Vector3(0f, collider.center.y - collider.height / 2f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            PlayerMayaVisualSetup.RemoveEmbeddedCameraRig(visual);
            PlayerMayaVisualSetup.RemoveEmbeddedPhysicsRig(visual);

            CharacterAttackAnimationLink link = player3.AddComponent<CharacterAttackAnimationLink>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("animator").objectReferenceValue = animator;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            // Player2 parity (explicit user confirmation): lockable and shows a health bar,
            // same as any other damageable-but-passive character.
            player3.AddComponent<LockOnTarget>();
            HealthBarSetup.AddHealthBar(player3);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added Player3 - stationary, damageable Maya-visual dummy sharing Player's exact LightAttack1/2/3 combat setup; never moves or attacks (no input source, no AI).");
        }
    }
}
