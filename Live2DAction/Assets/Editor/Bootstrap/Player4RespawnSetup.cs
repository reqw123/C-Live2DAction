using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Wires a third RespawnController onto "GameManager", targeting Player4 (2026-08-13,
    // explicit user request after noticing "發現敵人死了不會復活" - up to now this was a
    // deliberate choice, see KNOWN_ISSUES.md's history, but the user asked for consistency
    // with Player/Player2 instead). Reuses the same RespawnController component (generalized
    // from Player-only PlayerRespawnController on 2026-08-13) rather than duplicating it -
    // same 5s in-place respawn delay as Player/Player2, no reason given to use a different
    // value. Mirrors Player2RespawnSetup.cs's own reclaim-orphan-before-adding-new logic (see
    // that file's comment for the 2026-08-13 bug this avoids repeating).
    internal static class Player4RespawnSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Player4 Respawn Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player4 = GameObject.Find("Player4");
            if (player4 == null)
            {
                Debug.LogError("Player4 GameObject not found in " + ScenePath);
                return;
            }

            Health player4Health = player4.GetComponent<Health>();
            if (player4Health == null)
            {
                Debug.LogError("Player4 has no Health component in " + ScenePath);
                return;
            }

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            RespawnController respawnController = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var candidateSo = new SerializedObject(candidate);
                UnityEngine.Object candidateTarget = candidateSo.FindProperty("target").objectReferenceValue;
                if (candidateTarget == player4)
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
            so.FindProperty("target").objectReferenceValue = player4;
            so.FindProperty("targetHealth").objectReferenceValue = player4Health;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RespawnController on GameManager - Player4 now respawns in place with full health 5s after dying.");
        }
    }
}
