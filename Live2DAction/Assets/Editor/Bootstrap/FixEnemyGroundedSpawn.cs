using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Fixes the reported "the guy in shorts keeps floating in the air, even after I moved
    // his Y" bug - the same root cause already documented and fixed once for Player (see
    // FixPlayerGroundedSpawn.cs): TrainingDummy's CharacterController.height had been
    // manually changed (2 -> 1 - the scene now serializes m_Height: 1) without the spawn Y
    // being recalculated to match, leaving the capsule floating above (or sunk into) Ground.
    // Manually dragging the root Transform's Y in the Inspector can't fix this by itself -
    // the correct Y depends on height/center, not a value you'd know to compute by eye.
    // Doesn't matter here that EnemyAI never calls CharacterController.Move() while Idle
    // (detectionRange=0) - unlike Player, gravity never even gets a chance to accumulate and
    // correct/crash it into place, so it just sits wherever it was left indefinitely.
    // Spawn Y is derived from Ground's actual world bounds and the CharacterController's
    // current height/center, same formula as FixPlayerGroundedSpawn.cs, so this can't
    // silently break again if either is tuned later.
    internal static class FixEnemyGroundedSpawn
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[Fix] Ground Enemy Spawn Height")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("TrainingDummy");
            GameObject ground = GameObject.Find("Ground");
            if (enemy == null || ground == null)
            {
                Debug.LogError("TrainingDummy or Ground GameObject not found in " + ScenePath);
                return;
            }

            CharacterController controller = enemy.GetComponent<CharacterController>();
            Collider groundCollider = ground.GetComponent<Collider>();
            if (controller == null || groundCollider == null)
            {
                Debug.LogError("Missing CharacterController on TrainingDummy or Collider on Ground.");
                return;
            }

            float groundTopY = groundCollider.bounds.max.y;
            float requiredY = groundTopY + controller.center.y + controller.height / 2f;

            Vector3 position = enemy.transform.position;
            float previousY = position.y;
            position.y = requiredY;
            enemy.transform.position = position;

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Moved TrainingDummy spawn Y from {previousY} to {requiredY} to match CharacterController height ({controller.height}) against Ground top ({groundTopY}).");
        }
    }
}
