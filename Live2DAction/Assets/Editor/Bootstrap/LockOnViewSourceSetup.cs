using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Rewires TargetLockController.viewOrigin from Player's own transform to Main Camera's
    // (2026-08-13, explicit user request: "目前鎖定目標需要角色去面對敵人，能不能改為鼠標鏡頭面
    // 相來判斷?"). This reverses a 2026-08-12 explicit request that had it the other way
    // around (see GreyboxSceneBuilder.cs's own comment on the same wiring for that history).
    // TargetLockController.FindTarget() already just reads whatever Transform viewOrigin points
    // at - Main Camera's transform.forward already tracks mouse yaw/pitch every LateUpdate
    // (ThirdPersonCameraController), so no code changes were needed there, only which Transform
    // gets assigned. Doesn't touch GreyboxSceneBuilder.Build() (already updated separately for
    // future full rebuilds) - this is the incremental tool for the scene that already exists.
    internal static class LockOnViewSourceSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Use Camera Facing For Lock-On")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            TargetLockController lockController = player.GetComponent<TargetLockController>();
            if (lockController == null)
            {
                Debug.LogError("Player has no TargetLockController in " + ScenePath);
                return;
            }

            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera GameObject not found in " + ScenePath);
                return;
            }

            var so = new SerializedObject(lockController);
            so.FindProperty("viewOrigin").objectReferenceValue = mainCamera.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TargetLockController.viewOrigin now points at Main Camera - lock-on acquires whatever the camera/mouse is facing, not the character's own facing.");
        }
    }
}
