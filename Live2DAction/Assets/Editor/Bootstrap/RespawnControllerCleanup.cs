using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // One-off repair for a real 2026-08-13 bug: renaming RespawnController's fields
    // (player/playerHealth -> target/targetHealth) orphaned Player's already-serialized
    // instance on GameManager (target/targetHealth reset to null under the new field names -
    // Unity serializes fields by name). Re-running PlayerRespawnSetup.Apply() at the time
    // didn't reclaim that orphan (it only matched by exact target, so it just added ANOTHER
    // component), leaving 3 RespawnControllers on GameManager: 1 permanently-dead orphan +
    // 2 correctly wired ones. PlayerRespawnSetup/Player2RespawnSetup were fixed the same day
    // to reclaim an orphan instead of always adding a new one, so this shouldn't recur - this
    // tool just removes whatever orphan(s) are already sitting in the scene from before that
    // fix existed.
    internal static class RespawnControllerCleanup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Remove Orphaned Respawn Controllers")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                Debug.Log("No GameManager in " + ScenePath + " - nothing to clean up.");
                return;
            }

            int removed = 0;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var so = new SerializedObject(candidate);
                if (so.FindProperty("target").objectReferenceValue == null)
                {
                    Object.DestroyImmediate(candidate, true);
                    removed++;
                }
            }

            if (removed == 0)
            {
                Debug.Log("No orphaned RespawnControllers found on GameManager - nothing to clean up.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Removed {removed} orphaned RespawnController(s) (null target) from GameManager.");
        }
    }
}
