using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // The real fix, superseding two earlier ineffective attempts (BindingMode.WorldSpace,
    // then a position-only CameraFollowAnchor - see OrbitalCameraYawSource and
    // ICameraYawSource for why those didn't work). Also undoes the anchor indirection from
    // the second attempt, restoring a direct Follow/LookAt on the Player, since it added
    // complexity without fixing anything.
    internal static class FixCameraYawSource
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Wire Camera Yaw Source For Movement")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            var vcam = Object.FindFirstObjectByType<CinemachineCamera>();
            var orbitalFollow = Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            if (player == null || vcam == null || orbitalFollow == null)
            {
                Debug.LogError("Could not find Player/CinemachineCamera/CinemachineOrbitalFollow in " + ScenePath);
                return;
            }

            // Undo the CameraFollowAnchor indirection from the previous (ineffective) fix.
            vcam.Follow = player.transform;
            vcam.LookAt = player.transform;
            GameObject anchorGo = GameObject.Find("CameraFollowAnchor");
            if (anchorGo != null)
            {
                Object.DestroyImmediate(anchorGo);
            }

            OrbitalCameraYawSource yawSource = orbitalFollow.GetComponent<OrbitalCameraYawSource>();
            if (yawSource == null)
            {
                yawSource = orbitalFollow.gameObject.AddComponent<OrbitalCameraYawSource>();
            }

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("cameraYawSource").objectReferenceValue = yawSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Restored direct camera Follow/LookAt on Player and wired CharacterMovement.cameraYawSource to OrbitalCameraYawSource.");
        }
    }
}
