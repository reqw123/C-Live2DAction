using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Removes Cinemachine's camera rig from GreyboxTest.unity (CinemachineBrain on Main
    // Camera, the "CM Third Person Camera" GameObject, and any leftover "CameraFollowAnchor")
    // and replaces it with ThirdPersonCameraController directly on Main Camera. Also rewires
    // CharacterMovement.cameraYawSource to the new controller. See Docs/KNOWN_ISSUES.md for
    // why Cinemachine's orbital/aim system was abandoned for this camera.
    internal static class FixCameraCustomController
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Replace Camera With Custom Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            GameObject oldVcam = GameObject.Find("CM Third Person Camera");
            if (oldVcam != null)
            {
                Object.DestroyImmediate(oldVcam);
            }

            GameObject oldAnchor = GameObject.Find("CameraFollowAnchor");
            if (oldAnchor != null)
            {
                Object.DestroyImmediate(oldAnchor);
            }

            GameObject mainCameraGo = GameObject.FindWithTag("MainCamera");
            if (mainCameraGo == null)
            {
                Debug.LogError("No MainCamera-tagged GameObject found in " + ScenePath);
                return;
            }

            RemoveComponentIfPresent(mainCameraGo, "Unity.Cinemachine.CinemachineBrain");

            ThirdPersonCameraController controller = mainCameraGo.GetComponent<ThirdPersonCameraController>();
            if (controller == null)
            {
                controller = mainCameraGo.AddComponent<ThirdPersonCameraController>();
            }

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("target").objectReferenceValue = player.transform;
            controllerSo.FindProperty("distance").floatValue = 4f;
            controllerSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo = new SerializedObject(movement);
            movementSo.FindProperty("cameraYawSource").objectReferenceValue = controller;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Replaced Cinemachine camera rig with ThirdPersonCameraController and rewired CharacterMovement.");
        }

        private static void RemoveComponentIfPresent(GameObject go, string typeName)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component != null && component.GetType().FullName == typeName)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }
    }
}
