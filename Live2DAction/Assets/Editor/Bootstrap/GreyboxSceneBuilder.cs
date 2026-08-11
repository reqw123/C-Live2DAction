using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    internal static class GreyboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ComboAttacksFolder = "Assets/_Project/Settings/Combat";

        [MenuItem("Tools/Live2DAction/Build Greybox Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateGround();
            CreateCoverBlocks();

            GameObject player = CreatePlayer();
            CreateEnemy(player.transform);
            ThirdPersonCameraController cameraController = CreateCamera(player.transform);

            TargetLockController lockController = player.GetComponent<TargetLockController>();
            var lockSo = new SerializedObject(lockController);
            lockSo.FindProperty("viewOrigin").objectReferenceValue = cameraController.transform;
            lockSo.ApplyModifiedPropertiesWithoutUndo();

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo2 = new SerializedObject(movement);
            movementSo2.FindProperty("cameraYawSource").objectReferenceValue = cameraController;
            movementSo2.FindProperty("lockOnSource").objectReferenceValue = lockController;
            movementSo2.ApplyModifiedPropertiesWithoutUndo();

            // The fixed-world-axis camera itself no longer reacts to a lock-on (see
            // ThirdPersonCameraController) - only CharacterMovement's facing does, wired above.

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Live2DAction greybox test scene built at " + ScenePath);
        }

        private static void CreateLight()
        {
            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
        }

        private static void CreateCoverBlocks()
        {
            Vector3[] positions =
            {
                new Vector3(3f, 0.5f, 2f),
                new Vector3(-3f, 0.5f, -2f),
                new Vector3(0f, 0.5f, 5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = "CoverBlock" + (i + 1);
                cover.transform.position = positions[i];
                cover.transform.localScale = Vector3.one;
            }
        }

        private static GameObject CreatePlayer()
        {
            var player = new GameObject("Player");

            CapsuleCollider capsuleReference = player.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;

            // Derived from Ground's actual collider bounds rather than a hardcoded Y so this
            // can't quietly drift into a floating-capsule bug if height/radius are tuned
            // later (see FixPlayerGroundedSpawn.cs for the bug this caused once already).
            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            player.transform.position = new Vector3(0f, groundTopY + controller.center.y + controller.height / 2f, -2f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            PlayerInputProvider inputProvider = player.AddComponent<PlayerInputProvider>();
            CharacterMovement movement = player.AddComponent<CharacterMovement>();
            PlayerCombat combat = player.AddComponent<PlayerCombat>();
            TargetLockController lockController = player.AddComponent<TargetLockController>();
            Health playerHealth = player.AddComponent<Health>();

            var lockControllerSo = new SerializedObject(lockController);
            lockControllerSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            lockControllerSo.FindProperty("maxLockRange").floatValue = 15f;
            lockControllerSo.FindProperty("maxLockAngleDegrees").floatValue = 60f;
            lockControllerSo.FindProperty("breakRange").floatValue = 20f;
            lockControllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject movementSo = new SerializedObject(movement);
            movementSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            movementSo.FindProperty("dodgeData").objectReferenceValue = CreateOrLoadDodgeData();
            movementSo.FindProperty("health").objectReferenceValue = playerHealth;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            AttackData[] comboAttacks = CreateOrLoadComboAttacks();
            SerializedObject combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = comboAttacks.Length;
            for (int i = 0; i < comboAttacks.Length; i++)
            {
                comboProperty.GetArrayElementAtIndex(i).objectReferenceValue = comboAttacks[i];
            }
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        // Default frame data (see AttackData.FramesPerSecond) is a reasoned starting point,
        // not tuned-by-feel numbers - matches common action-game proportions (each hit a bit
        // slower/heavier than the last) and is meant to be adjusted from these assets in the
        // Inspector, never by editing this script.
        private static AttackData[] CreateOrLoadComboAttacks()
        {
            return new[]
            {
                CreateOrLoadAttackData("LightAttack1", damage: 8f, startupFrames: 6, activeFrames: 4, recoveryFrames: 14, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack2", damage: 10f, startupFrames: 7, activeFrames: 4, recoveryFrames: 16, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack3", damage: 16f, startupFrames: 10, activeFrames: 5, recoveryFrames: 22, comboWindowFrames: 0),
            };
        }

        // Reasoned starting point (see DodgeData.FramesPerSecond): a quick 3-unit burst
        // (12 frames = 0.2s), fully invulnerable for its duration, with a 20-frame (~0.33s)
        // cooldown to prevent spamming - meant to be tuned from the asset, not this script.
        private static DodgeData CreateOrLoadDodgeData()
        {
            const string assetPath = "Assets/_Project/Settings/DodgeData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DodgeData>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<DodgeData>();
            var so = new SerializedObject(data);
            so.FindProperty("distance").floatValue = 3f;
            so.FindProperty("durationFrames").intValue = 12;
            so.FindProperty("invulnerabilityFrames").intValue = 12;
            so.FindProperty("cooldownFrames").intValue = 20;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, assetPath);
            return data;
        }

        private static AttackData CreateOrLoadAttackData(string assetName, float damage, int startupFrames, int activeFrames, int recoveryFrames, int comboWindowFrames)
        {
            string path = $"{ComboAttacksFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ComboAttacksFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Combat");
            }

            var data = ScriptableObject.CreateInstance<AttackData>();
            var so = new SerializedObject(data);
            so.FindProperty("attackId").stringValue = assetName;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("startupFrames").intValue = startupFrames;
            so.FindProperty("activeFrames").intValue = activeFrames;
            so.FindProperty("recoveryFrames").intValue = recoveryFrames;
            so.FindProperty("comboWindowFrames").intValue = comboWindowFrames;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // TrainingDummy is now an AI-driven enemy rather than a static target - it reuses
        // PlayerCombat for its attack (see EnemyAI: it implements IInputCommand purely so the
        // same frame-data combo pipeline the player uses can be shared, per the project rule
        // that player and AI input share one interface). It has no distinct visual yet
        // (function before art, per the established pattern with the player's combo attacks).
        private static GameObject CreateEnemy(Transform playerTarget)
        {
            var enemy = new GameObject("TrainingDummy");

            CapsuleCollider capsuleReference = enemy.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            enemy.transform.position = new Vector3(0f, groundTopY + controller.center.y + controller.height / 2f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(enemy.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            enemy.AddComponent<Health>();
            enemy.AddComponent<LockOnTarget>();

            EnemyAI ai = enemy.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = playerTarget;
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

            return enemy;
        }

        // Lower damage than the player's combo (see CreateOrLoadComboAttacks) since the
        // player is expected to generally win a straight fight - a reasoned starting point,
        // not a balanced/tuned value.
        private static AttackData CreateOrLoadEnemyAttack()
        {
            return CreateOrLoadAttackData("EnemyAttack", damage: 5f, startupFrames: 10, activeFrames: 4, recoveryFrames: 20, comboWindowFrames: 0);
        }

        private static ThirdPersonCameraController CreateCamera(Transform followTarget)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 50f;

            // Custom, Cinemachine-free camera: see ThirdPersonCameraController for why
            // Cinemachine's orbital/aim system was removed (Docs/KNOWN_ISSUES.md has the full
            // investigation). Fixed world-axis angle (yaw/pitch never change, not even on
            // enemy lock-on) - CharacterMovement reads YawDegrees from this same component for
            // its camera-relative movement math, so screen orientation and movement direction
            // can never disagree.
            ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("target").objectReferenceValue = followTarget;
            controllerSo.FindProperty("distance").floatValue = 8f;
            controllerSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
            controllerSo.FindProperty("fixedYaw").floatValue = 0f;
            controllerSo.FindProperty("fixedPitch").floatValue = 45f;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = scenes.Exists(s => s.path == scenePath);
            if (!alreadyPresent)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
