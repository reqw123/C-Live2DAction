using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Wires a RespawnController into the existing GreyboxTest scene, targeting Player
    // (2026-08-12, real bug report - see RespawnController's own class comment for the full
    // story: Player dying to Player4's attacks left the whole GameObject deactivated with no
    // way back, which read as "the character vanished and the game froze"). Adds a small
    // always-active "GameManager" GameObject to hold this and any future cross-cutting
    // game-state logic, rather than piling it onto Main Camera - the controller can't live on
    // Player itself (see its class comment for why). RespawnController itself was generalized
    // from Player-only (PlayerRespawnController) on 2026-08-13 so Player2RespawnSetup could
    // reuse it without duplicating the component - this tool's own name/menu item stayed
    // Player-specific since it still only ever wires Player.
    internal static class PlayerRespawnSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Player Respawn Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null)
            {
                Debug.LogError("Player has no Health component in " + ScenePath);
                return;
            }

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            // Player's RespawnController specifically: GetComponents (plural) + a search for
            // one already targeting Player, since GameManager can now host a second instance
            // targeting Player2 (see Player2RespawnSetup) - a plain GetComponent<T>() would
            // find whichever one happens to be added first, not necessarily Player's own.
            // Also reclaims an orphaned component whose target is null (real 2026-08-13 bug:
            // renaming RespawnController's fields left Player's already-serialized instance
            // with target/targetHealth reset to null under the new field names - re-running
            // this tool without a "reclaim the orphan" fallback just added ANOTHER component
            // instead of fixing the existing one, leaving a permanently-dead third instance on
            // GameManager. An exact target match still always wins over an orphan.
            RespawnController respawnController = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var candidateSo = new SerializedObject(candidate);
                UnityEngine.Object candidateTarget = candidateSo.FindProperty("target").objectReferenceValue;
                if (candidateTarget == player)
                {
                    respawnController = candidate;
                    break;
                }
                if (candidateTarget == null && orphan == null)
                {
                    orphan = candidate;
                }
            }
            respawnController = respawnController != null ? respawnController : orphan;
            if (respawnController == null)
            {
                respawnController = managerGo.AddComponent<RespawnController>();
            }

            var so = new SerializedObject(respawnController);
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("targetHealth").objectReferenceValue = playerHealth;
            // Explicit rather than relying on the component's own class default landing in the
            // scene correctly - this field already existed when the component was first added
            // here, so its value gets baked into the serialized scene at add-time; changing the
            // C# default later does NOT retroactively update an already-saved scene's value
            // (unlike a brand new field, which does pick up the class default automatically).
            // 2026-08-12: raised from 0.5s to 5s by explicit user request.
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RespawnController on GameManager - Player now respawns in place with full health 5s after dying.");
        }
    }
}
