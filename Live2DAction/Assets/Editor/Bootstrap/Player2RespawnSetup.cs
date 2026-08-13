using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Wires a second RespawnController onto the existing "GameManager" GameObject, targeting
    // Player2 (2026-08-13, explicit user request: "設計player2可以復活"). Player2 already has a
    // Health component and takes damage (see Player2DamageableSetup) but stayed permanently
    // deactivated once killed, same as Player did before RespawnController existed - reuses
    // that same component (generalized from Player-only PlayerRespawnController this same day)
    // rather than duplicating its logic. Same 5s in-place respawn delay as Player, since the
    // user's request didn't specify different parameters.
    internal static class Player2RespawnSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Player2 Respawn Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player2 = GameObject.Find("Player2");
            if (player2 == null)
            {
                Debug.LogError("Player2 GameObject not found in " + ScenePath);
                return;
            }

            Health player2Health = player2.GetComponent<Health>();
            if (player2Health == null)
            {
                Debug.LogError("Player2 has no Health component in " + ScenePath + " - run Make Player2 Damageable first.");
                return;
            }

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            // GameManager can already host Player's own RespawnController - search for one
            // already targeting Player2 specifically rather than a plain GetComponent<T>(),
            // which would just find whichever instance happens to be first (see
            // PlayerRespawnSetup's own comment for the same reasoning). Also reclaims an
            // orphaned component whose target is null (a future field rename could leave one
            // behind the same way it did for Player on 2026-08-13 - see PlayerRespawnSetup's
            // own comment for the full story) instead of always adding a new one.
            RespawnController respawnController = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var candidateSo = new SerializedObject(candidate);
                UnityEngine.Object candidateTarget = candidateSo.FindProperty("target").objectReferenceValue;
                if (candidateTarget == player2)
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
            so.FindProperty("target").objectReferenceValue = player2;
            so.FindProperty("targetHealth").objectReferenceValue = player2Health;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RespawnController on GameManager - Player2 now respawns in place with full health 5s after dying.");
        }
    }
}
