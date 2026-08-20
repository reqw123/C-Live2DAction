using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Wires a third RespawnController onto "GameManager", targeting Enemy (2026-08-13,
    // explicit user request after noticing "發現敵人死了不會復活" - up to now this was a
    // deliberate choice, see KNOWN_ISSUES.md's history, but the user asked for consistency
    // with Player/Mecha instead). Reuses the same RespawnController component (generalized
    // from Player-only PlayerRespawnController on 2026-08-13) rather than duplicating it -
    // same 5s in-place respawn delay as Player/Mecha, no reason given to use a different
    // value. Mirrors MechaRespawnSetup.cs's own reclaim-orphan-before-adding-new logic (see
    // that file's comment for the 2026-08-13 bug this avoids repeating).
    internal static class EnemyRespawnSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Enemy Respawn Controller")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("Enemy");
            if (enemy == null)
            {
                Debug.LogError("Enemy GameObject not found in " + ScenePath);
                return;
            }

            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null)
            {
                Debug.LogError("Enemy has no Health component in " + ScenePath);
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
                if (candidateTarget == enemy)
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
            so.FindProperty("target").objectReferenceValue = enemy;
            so.FindProperty("targetHealth").objectReferenceValue = enemyHealth;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RespawnController on GameManager - Enemy now respawns in place with full health 5s after dying.");
        }
    }
}
