using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;

namespace Live2DAction.EditorTools
{
    // Wires the new first-person toggle fields onto the existing GreyboxTest scene's camera:
    // firstPersonEyeOffset and visualToHide (Player's "Visual" child, hidden while in
    // first-person mode since Maya has no separate first-person arms rig - see
    // Docs/KNOWN_ISSUES.md). Needed one-time because these fields didn't exist when the
    // scene's ThirdPersonCameraController was last wired.
    internal static class FixFirstPersonToggleSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Wire First Person Toggle")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject mainCameraGo = GameObject.Find("Main Camera");
            if (player == null || mainCameraGo == null)
            {
                Debug.LogError("Player or Main Camera GameObject not found in " + ScenePath);
                return;
            }

            ThirdPersonCameraController controller = mainCameraGo.GetComponent<ThirdPersonCameraController>();
            if (controller == null)
            {
                Debug.LogError("Main Camera has no ThirdPersonCameraController.");
                return;
            }

            Transform visual = player.transform.Find("Visual");
            var so = new SerializedObject(controller);
            so.FindProperty("firstPersonEyeOffset").vector3Value = new Vector3(0f, 1.6f, 0f);
            if (visual != null)
            {
                so.FindProperty("visualToHide").objectReferenceValue = visual.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired first-person toggle (eye offset + visual-to-hide) onto the Main Camera.");
        }
    }
}
