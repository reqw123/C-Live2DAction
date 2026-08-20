using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;

namespace Live2DAction.EditorTools
{
    // Re-applies ThirdPersonCameraController's fixed-world-axis fields onto the existing
    // GreyboxTest scene's Main Camera, and makes sure Player's "Visual" child is active.
    // Needed one-time because the previous mouse-look/first-person fields (mouseSensitivity,
    // minPitch/maxPitch, firstPersonEyeOffset, visualToHide, lockOnSource) no longer exist on
    // the class - if the scene was last saved mid first-person toggle, the Visual child could
    // otherwise be stuck inactive forever since nothing calls ToggleViewMode() anymore.
    internal static class FixFixedAxisCameraSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Set Fixed-World-Axis Camera")]
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

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("distance").floatValue = 8f;
            controllerSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
            controllerSo.FindProperty("fixedYaw").floatValue = 0f;
            controllerSo.FindProperty("fixedPitch").floatValue = 45f;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            Transform visual = player.transform.Find("Visual");
            if (visual != null && !visual.gameObject.activeSelf)
            {
                visual.gameObject.SetActive(true);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Set fixed-world-axis camera angle (yaw 0 / pitch 45) and ensured Player's Visual is active.");
        }
    }
}
