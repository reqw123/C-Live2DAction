using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Fixes the reported "takes a huge leap to a far-away spot" bug: the Player's
    // CharacterController.height had been manually changed (2 -> 1) at some point without
    // also adjusting the spawn Y, leaving the capsule floating 0.5 units above the Ground
    // collider. isGrounded was therefore never true, so gravity accumulated unbounded every
    // frame (never reset to the small grounded value) until the character finally clipped
    // something at a very high fall speed, producing what looked like a random big jump.
    // Spawn Y is derived from the Ground collider's actual world bounds and the
    // CharacterController's current height/center, instead of a hardcoded constant, so this
    // can't silently break again if either is tuned later.
    internal static class FixPlayerGroundedSpawn
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Ground Player Spawn Height")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject ground = GameObject.Find("Ground");
            if (player == null || ground == null)
            {
                Debug.LogError("Player or Ground GameObject not found in " + ScenePath);
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            Collider groundCollider = ground.GetComponent<Collider>();
            if (controller == null || groundCollider == null)
            {
                Debug.LogError("Missing CharacterController on Player or Collider on Ground.");
                return;
            }

            float groundTopY = groundCollider.bounds.max.y;
            float requiredY = groundTopY + controller.center.y + controller.height / 2f;

            Vector3 position = player.transform.position;
            float previousY = position.y;
            position.y = requiredY;
            player.transform.position = position;

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Moved Player spawn Y from {previousY} to {requiredY} to match CharacterController height ({controller.height}) against Ground top ({groundTopY}).");
        }
    }
}
