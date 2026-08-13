using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: sets every character's
    // CharacterController.stepOffset to 0 (2026-08-12 real bug report - "很靠近敵人時角色1突然
    // 消失，畫面定格"). Root cause confirmed via a diagnostic PlayMode test: with the default
    // stepOffset (0.3), walking Player straight into Player4 (also a CharacterController) let
    // Player climb up its rounded capsule top over a few seconds of continued forward input -
    // Y drifted from 0.58 to 1.66 and then got stuck oscillating back and forth at the top,
    // which is what read as "disappeared and the screen froze" (camera has no collision
    // avoidance - see KNOWN_ISSUES.md - so it likely ended up clipped into Player4's head
    // geometry from up there). stepOffset=0 stops the climb entirely (confirmed: Y stayed
    // exactly flat across the same test). GreyboxSceneBuilder.cs/Player4EnemyAISetup.cs's
    // defaults were updated the same way for future rebuilds; this applies the same change to
    // the already-saved scene without going through the destructive Build().
    internal static class FixCharacterControllerStepOffset
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Zero Character Controller Step Offset")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int fixedCount = 0;
            foreach (string name in new[] { "Player", "Player4", "TrainingDummy" })
            {
                GameObject go = GameObject.Find(name);
                CharacterController controller = go != null ? go.GetComponent<CharacterController>() : null;
                if (controller == null)
                {
                    continue;
                }

                controller.stepOffset = 0f;
                fixedCount++;
                Debug.Log($"{name}: CharacterController.stepOffset set to 0.");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Zeroed stepOffset on {fixedCount} CharacterController(s).");
        }
    }
}
