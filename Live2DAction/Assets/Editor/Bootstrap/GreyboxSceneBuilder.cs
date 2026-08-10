using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;

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
            CreateDummy();
            ThirdPersonCameraController yawSource = CreateCamera(player.transform);

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo2 = new SerializedObject(movement);
            movementSo2.FindProperty("cameraYawSource").objectReferenceValue = yawSource;
            movementSo2.ApplyModifiedPropertiesWithoutUndo();

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

            SerializedObject movementSo = new SerializedObject(movement);
            movementSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
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

        private static void CreateDummy()
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "TrainingDummy";
            dummy.transform.position = new Vector3(0f, 1f, 0f);
            dummy.AddComponent<Health>();
        }

        private static ThirdPersonCameraController CreateCamera(Transform followTarget)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 50f;

            // Custom, Cinemachine-free orbit camera: see ThirdPersonCameraController for why
            // Cinemachine's orbital/aim system was removed (Docs/KNOWN_ISSUES.md has the full
            // investigation). CharacterMovement reads YawDegrees from this same component for
            // its camera-relative movement math, so screen orientation and movement direction
            // can never disagree.
            ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("target").objectReferenceValue = followTarget;
            controllerSo.FindProperty("distance").floatValue = 4f;
            controllerSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
            controllerSo.FindProperty("mouseSensitivity").floatValue = 0.15f;
            controllerSo.FindProperty("minPitch").floatValue = -20f;
            controllerSo.FindProperty("maxPitch").floatValue = 60f;
            controllerSo.FindProperty("initialYaw").floatValue = 0f;
            controllerSo.FindProperty("initialPitch").floatValue = 25f;
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
