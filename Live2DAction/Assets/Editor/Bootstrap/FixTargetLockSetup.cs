using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Adds the enemy lock-on system to the existing GreyboxTest scene: TargetLockController
    // on Player, LockOnTarget on TrainingDummy, and cross-wires CharacterMovement /
    // ThirdPersonCameraController to read the lock via ILockOnSource. One-time because none
    // of these fields/components existed when the scene was last saved.
    internal static class FixTargetLockSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Wire Target Lock-On")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject mainCameraGo = GameObject.Find("Main Camera");
            GameObject dummy = GameObject.Find("TrainingDummy");
            if (player == null || mainCameraGo == null || dummy == null)
            {
                Debug.LogError("Player, Main Camera, or TrainingDummy not found in " + ScenePath);
                return;
            }

            if (dummy.GetComponent<LockOnTarget>() == null)
            {
                dummy.AddComponent<LockOnTarget>();
            }

            TargetLockController lockController = player.GetComponent<TargetLockController>();
            if (lockController == null)
            {
                lockController = player.AddComponent<TargetLockController>();
            }

            PlayerInputProvider inputProvider = player.GetComponent<PlayerInputProvider>();
            var lockSo = new SerializedObject(lockController);
            lockSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            lockSo.FindProperty("viewOrigin").objectReferenceValue = mainCameraGo.transform;
            lockSo.FindProperty("maxLockRange").floatValue = 15f;
            lockSo.FindProperty("maxLockAngleDegrees").floatValue = 60f;
            lockSo.FindProperty("breakRange").floatValue = 20f;
            lockSo.ApplyModifiedPropertiesWithoutUndo();

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo = new SerializedObject(movement);
            movementSo.FindProperty("lockOnSource").objectReferenceValue = lockController;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            ThirdPersonCameraController cameraController = mainCameraGo.GetComponent<ThirdPersonCameraController>();
            var cameraSo = new SerializedObject(cameraController);
            cameraSo.FindProperty("lockOnSource").objectReferenceValue = lockController;
            cameraSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired target lock-on: TargetLockController on Player, LockOnTarget on TrainingDummy, cross-wired to CharacterMovement/ThirdPersonCameraController.");
        }
    }
}
