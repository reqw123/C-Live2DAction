using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: applies the new optional auto-center
    // fields (2026-08-12, see ThirdPersonCameraController's field comments) and wires its
    // lockOnSource to Player's TargetLockController, matching GreyboxSceneBuilder.Build's new
    // defaults. Opens the saved scene directly rather than going through Build() (see
    // Docs/KNOWN_ISSUES.md's operating warning about that wiping the whole scene).
    internal static class FixCameraAutoCenter
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Enable Camera Auto-Center")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject cameraGo = GameObject.Find("Main Camera");
            GameObject player = GameObject.Find("Player");
            if (cameraGo == null || player == null)
            {
                Debug.LogError("Main Camera or Player GameObject not found in " + ScenePath);
                return;
            }

            var controller = cameraGo.GetComponent<ThirdPersonCameraController>();
            var lockController = player.GetComponent<TargetLockController>();
            if (controller == null)
            {
                Debug.LogError("Main Camera has no ThirdPersonCameraController in " + ScenePath);
                return;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("enableAutoCenter").boolValue = true;
            so.FindProperty("autoCenterDelay").floatValue = 0.8f;
            so.FindProperty("autoCenterSpeed").floatValue = 2f;
            so.FindProperty("lockOnSource").objectReferenceValue = lockController;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled camera auto-center (delay=0.8, speed=2) and wired lockOnSource.");
        }
    }
}
