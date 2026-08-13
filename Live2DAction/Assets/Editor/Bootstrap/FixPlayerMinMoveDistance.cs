using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene: GreyboxSceneBuilder.cs and the
    // PlayMode tests were fixed to set CharacterController.minMoveDistance=0 (see
    // Docs/KNOWN_ISSUES.md's minMoveDistance trap writeup), but that only affects freshly
    // built/tested CharacterControllers - the live scene's own already-existing Player
    // CharacterController was never patched, so it was still silently dropping small
    // Move() calls. Opens the saved scene directly rather than going through
    // GreyboxSceneBuilder.Build() (see Docs/KNOWN_ISSUES.md's operating warning about that
    // wiping the whole scene).
    internal static class FixPlayerMinMoveDistance
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Set Player MinMoveDistance To Zero")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                Debug.LogError("Player has no CharacterController in " + ScenePath);
                return;
            }

            float previous = controller.minMoveDistance;
            controller.minMoveDistance = 0f;

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Set Player CharacterController.minMoveDistance from {previous} to 0.");
        }
    }
}
