using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: reverts the same-day right-shoulder
    // camera detour (FixCameraToRightShoulder.cs, kept for history) back to the free-look
    // mouse-orbit camera + camera-relative WASD strafe scheme, by explicit request ("改回剛剛
    //那樣視角可以左右上下移動，a/d能角色左右移動，參考rgb遊戲原神鳴潮等等"). Restores
    // distance/targetOffset to the user's last-known free-look tuning and re-wires
    // CharacterMovement.cameraYawSource (dropped from the class entirely during the tank-
    // controls detour, so the scene's serialized reference to it is just gone, not merely
    // wrong - has to be set again from scratch). Opens the saved scene directly rather than
    // going through GreyboxSceneBuilder.Build() (see Docs/KNOWN_ISSUES.md's operating warning
    // about that wiping the whole scene).
    internal static class FixCameraToFreeLook
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Set Camera To Free-Look (Revert Right Shoulder)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject cameraGo = GameObject.Find("Main Camera");
            GameObject playerGo = GameObject.Find("Player");
            if (cameraGo == null || playerGo == null)
            {
                Debug.LogError("Main Camera or Player GameObject not found in " + ScenePath);
                return;
            }

            var controller = cameraGo.GetComponent<ThirdPersonCameraController>();
            var movement = playerGo.GetComponent<CharacterMovement>();
            if (controller == null || movement == null)
            {
                Debug.LogError("Main Camera has no ThirdPersonCameraController or Player has no CharacterMovement in " + ScenePath);
                return;
            }

            var cameraSo = new SerializedObject(controller);
            cameraSo.FindProperty("distance").floatValue = 0.8f;
            cameraSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 0.5f, 0f);
            cameraSo.ApplyModifiedPropertiesWithoutUndo();

            var movementSo = new SerializedObject(movement);
            movementSo.FindProperty("cameraYawSource").objectReferenceValue = controller;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Reverted Main Camera to free-look (distance=0.8, targetOffset=(0,0.5,0)) and re-wired CharacterMovement.cameraYawSource.");
        }
    }
}
