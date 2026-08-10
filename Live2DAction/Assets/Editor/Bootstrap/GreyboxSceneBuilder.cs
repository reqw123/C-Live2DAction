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
        private const string AttackDataPath = "Assets/_Project/Settings/TestPunch.asset";

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
            player.transform.position = new Vector3(0f, 1f, -2f);

            CapsuleCollider capsuleReference = player.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;

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

            AttackData attackData = CreateOrLoadAttackData();
            SerializedObject combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            combatSo.FindProperty("attackData").objectReferenceValue = attackData;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static AttackData CreateOrLoadAttackData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(AttackDataPath);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(data, AttackDataPath);
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
            controllerSo.FindProperty("yawDegrees").floatValue = 0f;
            controllerSo.FindProperty("pitchDegrees").floatValue = 25f;
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
