using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: the user explicitly asked (2026-08-12)
    // for a fixed view that follows the character's own line of sight with no third-person
    // orbit sweep - true first-person, which is distance=0 (see
    // ThirdPersonCameraController's field comment). Opens the saved scene directly rather than
    // going through GreyboxSceneBuilder.Build() (see Docs/KNOWN_ISSUES.md's operating warning
    // about that wiping the whole scene) - GreyboxSceneBuilder.cs's own default was updated to
    // match for any future from-scratch rebuild, but this scene already exists.
    internal static class FixCameraToFirstPerson
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Set Camera To True First-Person")]
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
            float previousDistance = so.FindProperty("distance").floatValue;
            so.FindProperty("distance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Set Main Camera distance from {previousDistance} to 0 (true first-person).");
        }
    }
}
