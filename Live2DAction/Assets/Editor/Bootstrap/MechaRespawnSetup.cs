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
    // Health component and takes damage (see MechaDamageableSetup) but stayed permanently
    // deactivated once killed, same as Player did before RespawnController existed - reuses
    // that same component (generalized from Player-only PlayerRespawnController this same day)
    // rather than duplicating its logic. Same 5s in-place respawn delay as Player, since the
    // user's request didn't specify different parameters.
    internal static class MechaRespawnSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Mecha Respawn Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject mecha = GameObject.Find("Mecha");
            if (mecha == null)
            {
                Debug.LogError("Mecha GameObject not found in " + ScenePath);
                return;
            }

            Health mechaHealth = mecha.GetComponent<Health>();
            if (mechaHealth == null)
            {
                Debug.LogError("Mecha has no Health component in " + ScenePath + " - run Make Mecha Damageable first.");
                return;
            }

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            // GameManager can already host Player's own RespawnController - search for one
            // already targeting Mecha specifically rather than a plain GetComponent<T>(),
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
                if (candidateTarget == mecha)
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
            so.FindProperty("target").objectReferenceValue = mecha;
            so.FindProperty("targetHealth").objectReferenceValue = mechaHealth;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RespawnController on GameManager - Mecha now respawns in place with full health 5s after dying.");
        }
    }
}
