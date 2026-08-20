using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: supersedes FixCameraToFirstPerson.cs
    // (kept for history, not deleted - same convention as this project's other superseded
    // Fix*.cs scripts). The user tried true first-person, then explicitly asked instead for a
    // fixed camera over the character's right shoulder that always matches their facing and
    // never swings to their left - see ThirdPersonCameraController's class comment for how
    // that's implemented (yaw driven by target.eulerAngles.y, not mouse). This script just
    // applies GreyboxSceneBuilder.CreateCamera's new reasoned-starting-point numbers (right
    // 0.5, up 1.4, pulled back 2.5) to the already-built scene, the same way
    // FixCameraToFirstPerson did - opens the saved scene directly rather than going through
    // Build() (see Docs/KNOWN_ISSUES.md's operating warning about that wiping the scene).
    internal static class FixCameraToRightShoulder
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Set Camera To Right Shoulder")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject cameraGo = GameObject.Find("Main Camera");
            if (cameraGo == null)
            {
                Debug.LogError("Main Camera GameObject not found in " + ScenePath);
                return;
            }

            var controller = cameraGo.GetComponent<ThirdPersonCameraController>();
            if (controller == null)
            {
                Debug.LogError("Main Camera has no ThirdPersonCameraController in " + ScenePath);
                return;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("distance").floatValue = 2.5f;
            so.FindProperty("targetOffset").vector3Value = new Vector3(0.5f, 1.4f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Set Main Camera to right-shoulder rig: distance=2.5, targetOffset=(0.5, 1.4, 0).");
        }
    }
}
